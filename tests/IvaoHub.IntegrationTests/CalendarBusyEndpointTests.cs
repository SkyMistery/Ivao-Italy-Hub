using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The free and the busy of the calendar, over the wire (note <c>decisions/2026-11-18-il-calendario-libero-e-occupato.md</c>):
/// what <c>/api/calendar/busy</c> answers to whom — each person sees busy only what they may read, so a member never sees the
/// staff's commitments (Carmine's answer 2) —, the time each entry takes, the row being written left out, the kind that blocks,
/// and the check a module's validator asks, which reads as its writer.
/// <para>⚠️ VID range 947101+, which is this class's own; and the entries are in March 2031 under kinds of this class, because
/// the suite shares one database and another class's entries may be in any window of now.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class CalendarBusyEndpointTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int MemberVid = 947101;
    private const int EventsCoordinatorVid = 947102;
    private const int TrainingCoordinatorVid = 947103;
    private const int WebCoordinatorVid = 947104;

    private static readonly DateTime Day = new(2031, 3, 10, 0, 0, 0, DateTimeKind.Utc);

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task EachReaderSeesBusyOnlyWhatTheyMayRead()
    {
        var token = TestContext.Current.CancellationToken;
        var seeded = await SeedAsync(token);

        using var anonymous = _factory.CreateApiClient();
        using var refused = await anonymous.GetAsync(Busy(Day, Day.AddDays(3)), token);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // A member: the public entry and the members' one, never the staff's nor a department's.
        using var member = await SignedInAsync(MemberVid, position: null, token);
        var forMember = await ReadAsync(member, seeded, token);
        // In the order of the time they take: the members' entry at 09:00 before the public one at 14:00.
        Assert.Equal(["members", "public", "all-day"], forMember.Select(entry => entry.Title).ToArray());

        // The coordinator of another department: the staff's entry too, but not the events department's own.
        using var training = await SignedInAsync(TrainingCoordinatorVid, "IT-TC", token);
        var forTraining = await ReadAsync(training, seeded, token);
        Assert.Equal(["members", "public", "staff", "all-day"], forTraining.Select(entry => entry.Title).ToArray());

        // The events department reads its own entry as well.
        using var events = await SignedInAsync(EventsCoordinatorVid, "IT-EC", token);
        var forEvents = await ReadAsync(events, seeded, token);
        Assert.Equal(["members", "public", "department", "staff", "all-day"], forEvents.Select(entry => entry.Title).ToArray());

        // The time each takes (answer 4): the entry with an end its span, the one with none its hour, the all-day its day.
        var byTitle = forEvents.ToDictionary(entry => entry.Title);
        Assert.Equal((Day.AddHours(14), Day.AddHours(16)), (byTitle["public"].StartsAtUtc, byTitle["public"].EndsAtUtc));
        Assert.Equal((Day.AddHours(9), Day.AddHours(10)), (byTitle["members"].StartsAtUtc, byTitle["members"].EndsAtUtc));
        Assert.Equal((Day.AddDays(1), Day.AddDays(2)), (byTitle["staff"].StartsAtUtc, byTitle["staff"].EndsAtUtc));
        Assert.True(byTitle["staff"].AllDay);

        // An all-day entry stored at 10:00 takes its day from midnight, and a window that ends before 10:00 still finds it
        // (the review of #538, finding 1).
        Assert.Equal((Day.AddDays(2), Day.AddDays(3)), (byTitle["all-day"].StartsAtUtc, byTitle["all-day"].EndsAtUtc));
        var early = Mine(await member.GetFromJsonAsync<JsonElement>(Busy(Day.AddDays(2), Day.AddDays(2).AddHours(4)), token), seeded);
        Assert.Equal(["all-day"], early.Select(entry => entry.Title).ToArray());

        // And whether its kind blocks, from the vocabulary of the division.
        Assert.True(byTitle["public"].Blocks);
        Assert.False(byTitle["members"].Blocks);
        Assert.True(byTitle["department"].Blocks);
    }

    [Fact]
    public async Task TheRowBeingWrittenDoesNotOccupyItsOwnDate()
    {
        var token = TestContext.Current.CancellationToken;
        var seeded = await SeedAsync(token);

        using var events = await SignedInAsync(EventsCoordinatorVid, "IT-EC", token);
        var answer = await events.GetFromJsonAsync<JsonElement>(
            Busy(Day, Day.AddDays(3), except: ("events", seeded.PublicSourceId)),
            token);

        var titles = Mine(answer, seeded).Select(entry => entry.Title).ToArray();
        Assert.DoesNotContain("public", titles);
        Assert.Contains("members", titles);
    }

    [Fact]
    public async Task AWindowThatIsNotOneIsRefusedOnItsField()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(MemberVid, position: null, token);
        using var member = await SignedInAsync(MemberVid, position: null, token);

        foreach (var (from, to) in new[] { (Day, Day.AddDays(-1)), (Day, Day.AddDays(CalendarBusy.MaxWindowDays + 1)) })
        {
            using var response = await member.GetAsync(Busy(from, to), token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal(
                CalendarBusy.WindowKey,
                problem.GetProperty("errors").GetProperty("toUtc")[0].GetString());
        }

        using var nothing = await member.GetAsync(new Uri("/api/calendar/busy", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.BadRequest, nothing.StatusCode);
        var missing = await nothing.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("errors.required", missing.GetProperty("errors").GetProperty("fromUtc")[0].GetString());
    }

    [Fact]
    public async Task TheCheckRefusesOnlyTheTimeABlockingKindTakesAsItsWriterSeesIt()
    {
        var token = TestContext.Current.CancellationToken;
        var seeded = await SeedAsync(token);

        // A scope with nobody signed in reads what anybody may read: the public entry, not the department's.
        await using var scope = _factory.Services.CreateAsyncScope();
        var busy = scope.ServiceProvider.GetRequiredService<CalendarBusy>();

        async Task<string[]> BlockingAsync(DateTime start, DateTime? end, CalendarSource? except = null, bool allDay = false) =>
            [.. (await busy.BlockingAsync(start, end, allDay, except, token))
                .Where(entry => entry.Kind == seeded.BlockingKind)
                .Select(entry => entry.Title.Get("en") ?? string.Empty)];

        // A date with no end takes its hour: the hour of 14:30 meets the blocked span; the hours of 13:00 and 16:00 touch it
        // only at its edges, and are free.
        Assert.Equal(["public"], await BlockingAsync(Day.AddHours(14).AddMinutes(30), end: null));
        Assert.Empty(await BlockingAsync(Day.AddHours(13), end: null));
        Assert.Empty(await BlockingAsync(Day.AddHours(16), end: null));

        // A span that crosses the blocked one is refused even if both its ends are free.
        Assert.Equal(["public"], await BlockingAsync(Day.AddHours(13), Day.AddHours(17)));

        // A kind that does not block refuses nothing, however busy.
        Assert.Empty(await BlockingAsync(Day.AddHours(9), end: null));

        // The department's blocking entry is not seen by this writer, so it refuses nothing.
        Assert.Empty(await BlockingAsync(Day.AddHours(18), end: null));

        // An all-day blocking entry stored at 10:00 refuses 03:00 of its day, as the picker switches it off (finding 1).
        Assert.Equal(["all-day"], await BlockingAsync(Day.AddDays(2).AddHours(3), end: null));

        // A date without a time takes its whole UTC day: refused by any blocking entry that touches it (note 3 of the review).
        Assert.Equal(["public"], await BlockingAsync(Day, end: null, allDay: true));
        Assert.Empty(await BlockingAsync(Day.AddDays(1), end: null, allDay: true));

        // And the row being written does not refuse itself.
        Assert.Empty(await BlockingAsync(Day.AddHours(14).AddMinutes(30), end: null, new CalendarSource("events", seeded.PublicSourceId)));
    }

    [Fact]
    public async Task ARetiredKindKeepsBlockingAndTheAnswerNamesIt()
    {
        // Carmine's answer on #538 ("Sì, finché si toglie"): a retired kind that blocks keeps blocking until its box is
        // unticked; the bootstrap does not carry it, so the answer brings its name and colour from the kinds table.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(MemberVid, position: null, token);

        var retired = $"gone-{Guid.NewGuid():N}"[..20];
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            database.CalendarKinds.Add(new CalendarKind
            {
                Key = retired,
                Label = "Fly-in di una volta".L("Fly-in of old"),
                Colour = "pink",
                Sort = 97,
                Blocks = true,
                IsActive = false,
            });
            database.CalendarEntries.Add(Entry(
                retired, Visibility.Public, "core", $"staff:{retired}", Day.AddDays(1).AddHours(20), Day.AddDays(1).AddHours(21), allDay: false, "retired"));
            await database.SaveChangesAsync(token);
        }

        using var member = await SignedInAsync(MemberVid, position: null, token);

        var answer = await member.GetFromJsonAsync<JsonElement>(Busy(Day, Day.AddDays(3)), token);
        var entry = answer.EnumerateArray().Single(row => row.GetProperty("kind").GetString() == retired);
        Assert.True(entry.GetProperty("blocks").GetBoolean());
        Assert.Equal("Fly-in of old", entry.GetProperty("kindLabel").GetProperty("en").GetString());
        Assert.Equal("pink", entry.GetProperty("kindColour").GetString());

        var me = await member.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.DoesNotContain(
            me.GetProperty("calendarKinds").EnumerateArray(),
            kind => kind.GetProperty("key").GetString() == retired);

        await using var check = _factory.Services.CreateAsyncScope();
        var busy = check.ServiceProvider.GetRequiredService<CalendarBusy>();
        var refused = await busy.BlockingAsync(Day.AddDays(1).AddHours(20).AddMinutes(30), endsAtUtc: null, allDay: false, except: null, token);
        Assert.Contains(refused, row => row.Kind == retired);
    }

    [Fact]
    public async Task TheDivisionSaysOnTheKindWhetherItBlocks()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);
        using var web = await SignedInAsync(WebCoordinatorVid, "IT-WM", token);

        var key = $"busy-{Guid.NewGuid():N}"[..20];
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(CalendarKindEndpoints.Pattern, UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                key,
                label = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = key, ["en"] = key },
                colour = "red",
                sort = 99,
                isActive = true,
                blocks = true,
                rowVersion = "0001-01-01T00:00:00",
            }),
        };

        // What the browser sends on every write, and the hub asks of one.
        request.Headers.Add("X-Requested-With", "hub");
        using var created = await web.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True((await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("blocks").GetBoolean());

        // The legend of every picker reads it from the bootstrap.
        var me = await web.GetFromJsonAsync<JsonElement>("/api/me", token);
        var kind = me.GetProperty("calendarKinds").EnumerateArray().Single(word => word.GetProperty("key").GetString() == key);
        Assert.True(kind.GetProperty("blocks").GetBoolean());
    }

    // ---- helpers -----------------------------------------------------------------------------

    private sealed record Seeded(string BlockingKind, string FreeKind, string PublicSourceId);

    private sealed record Taken(DateTime StartsAtUtc, DateTime EndsAtUtc, bool AllDay, string Kind, bool Blocks, string Title);

    private static Uri Busy(DateTime from, DateTime to, (string Module, string Id)? except = null)
    {
        var query = $"fromUtc={Uri.EscapeDataString(from.ToString("O"))}&toUtc={Uri.EscapeDataString(to.ToString("O"))}";
        if (except is { } row)
        {
            query += $"&exceptModule={Uri.EscapeDataString(row.Module)}&exceptId={Uri.EscapeDataString(row.Id)}";
        }

        return new Uri($"/api/calendar/busy?{query}", UriKind.Relative);
    }

    private static async Task<Taken[]> ReadAsync(HttpClient client, Seeded seeded, CancellationToken cancellationToken) =>
        Mine(await client.GetFromJsonAsync<JsonElement>(Busy(Day, Day.AddDays(3)), cancellationToken), seeded);

    /// <summary>The answer's entries of this class's kinds, in the order the server gave them.</summary>
    private static Taken[] Mine(JsonElement answer, Seeded seeded) =>
    [
        .. answer.EnumerateArray()
            .Where(entry => entry.GetProperty("kind").GetString() is { } kind && (kind == seeded.BlockingKind || kind == seeded.FreeKind))
            .Select(entry => new Taken(
                entry.GetProperty("startsAtUtc").GetDateTime().ToUniversalTime(),
                entry.GetProperty("endsAtUtc").GetDateTime().ToUniversalTime(),
                entry.GetProperty("allDay").GetBoolean(),
                entry.GetProperty("kind").GetString()!,
                entry.GetProperty("blocks").GetBoolean(),
                entry.GetProperty("title").GetProperty("en").GetString()!)),
    ];

    /// <summary>
    /// Two kinds of this run, one that blocks; four entries of the events department, one per visibility: public and blocking
    /// from 14:00 to 16:00, members' with no end at 09:00, the staff's all the next day, the department's blocking at 18:00; and a
    /// public blocking entry of the whole day after, stored at 10:00 as the calendar's form may store one.
    /// </summary>
    private async Task<Seeded> SeedAsync(CancellationToken cancellationToken)
    {
        await SeedUserAsync(MemberVid, position: null, cancellationToken);
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", cancellationToken);
        await SeedUserAsync(TrainingCoordinatorVid, "IT-TC", cancellationToken);

        var run = Guid.NewGuid().ToString("N")[..10];
        var blocking = $"block-{run}";
        var free = $"free-{run}";
        var publicSource = $"event:{run}";

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.CalendarKinds.Add(new CalendarKind { Key = blocking, Label = blocking.L(blocking), Colour = "red", Sort = 98, Blocks = true });
        database.CalendarKinds.Add(new CalendarKind { Key = free, Label = free.L(free), Colour = "green", Sort = 99 });

        database.CalendarEntries.AddRange(
            Entry(blocking, Visibility.Public, "events", publicSource, Day.AddHours(14), Day.AddHours(16), allDay: false, "public"),
            Entry(free, Visibility.Members, "core", $"staff:{run}-m", Day.AddHours(9), end: null, allDay: false, "members"),
            Entry(free, Visibility.Staff, "core", $"staff:{run}-s", Day.AddDays(1), end: null, allDay: true, "staff"),
            Entry(blocking, Visibility.Department, "core", $"staff:{run}-d", Day.AddHours(18), Day.AddHours(19), allDay: false, "department"),
            Entry(blocking, Visibility.Public, "core", $"staff:{run}-a", Day.AddDays(2).AddHours(10), end: null, allDay: true, "all-day"));

        await database.SaveChangesAsync(cancellationToken);

        return new Seeded(blocking, free, publicSource);
    }

    private static CalendarEntry Entry(
        string kind,
        Visibility visibility,
        string sourceModule,
        string sourceId,
        DateTime start,
        DateTime? end,
        bool allDay,
        string title) => new()
        {
            OwnerDepartment = Department.ED,
            Visibility = visibility,
            Kind = kind,
            SourceModule = sourceModule,
            SourceId = sourceId,
            StartsAtUtc = start,
            EndsAtUtc = end,
            AllDay = allDay,
            Url = string.Empty,
            Title = title.L(title),
        };

    private async Task<HttpClient> SignedInAsync(int vid, string? position, CancellationToken cancellationToken)
    {
        await SeedUserAsync(vid, position, cancellationToken);
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task SeedUserAsync(int vid, string? position, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = vid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "User";
        user.IsStaff = position is not null;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
        {
            var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = position,
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
