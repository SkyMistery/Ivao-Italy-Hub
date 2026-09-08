using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The vocabulary of the calendar: the words the whole division files its entries under.
/// <para>It exists because Carmine asked for it after running the demo of M1 — "the list is decided
/// by headquarters, by the web team or from the admin section, and it is the same for everybody" —
/// and the note that weighed the three ways of building it is
/// <c>decisions/2026-09-08-tipi-di-evento-di-divisione.md</c>.</para>
/// <para>What is worth testing is not that a CRUD works: the engine's own tests cover that. It is
/// the three things this resource does that no other does — a resource with <b>no department</b>
/// whose read and write policies are different, an entry that is now <b>refused</b> when it names a
/// word nobody declared, and the vocabulary travelling in the bootstrap so that a visitor's chip
/// can say the word and the colour.</para>
/// <para>⚠️ VID range 720001+, which is this class's own: the suite shares one database across the
/// whole collection, so two classes on the same VID are one row (handoff §23).</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class CalendarKindsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int CoordinatorVid = 720001;
    private const int WebCoordinatorVid = 720002;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheVocabularyIsReadByTheStaffAndWrittenByTheDivision()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(CoordinatorVid, "IT-EC", token);
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var web = await SignedInAsync(WebCoordinatorVid, token);

        // Reading is `Calendar.View`, which a coordinator of any department holds: the select they
        // write an entry with would be empty otherwise.
        var listed = await coordinator.GetFromJsonAsync<JsonElement>(CalendarKindEndpoints.Pattern, token);
        Assert.True(listed.GetProperty("total").GetInt32() >= 5);

        // Writing is `Calendar.ManageKinds`, which is global — so a coordinator of one department
        // may not, whatever they may do inside their own.
        using var refused = await SendAsync(coordinator, HttpMethod.Post, CalendarKindEndpoints.Pattern, Word("briefing"), token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // The web team reaches every department, so it holds every global permission.
        using var created = await SendAsync(web, HttpMethod.Post, CalendarKindEndpoints.Pattern, Word("briefing"), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // And a colour the design system does not have is refused, which is what keeps the palette
        // in `web/src/shared/ui/calendar.ts` and the one in the validator agreeing by hand.
        using var wrongColour = await SendAsync(
            web,
            HttpMethod.Post,
            CalendarKindEndpoints.Pattern,
            Word("chartreuse-thing", colour: "chartreuse"),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, wrongColour.StatusCode);
        var problem = await wrongColour.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(
            "errors.calendar.colourUnknown",
            problem.GetProperty("errors").GetProperty("colour")[0].GetString());
    }

    [Fact]
    public async Task AnEntryNamesAWordTheDivisionDeclared()
    {
        // ⚠️ The half that makes the vocabulary a vocabulary rather than a suggestion. Until G13 the
        // kind was free text, and two departments writing `Training` and `training` had two kinds in
        // a calendar that is supposed to be one.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(CoordinatorVid, "IT-EC", token);

        using var client = await SignedInAsync(CoordinatorVid, token);

        using var refused = await SendAsync(
            client,
            HttpMethod.Post,
            CalendarEndpoints.Pattern,
            Entry("whatever-somebody-typed"),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(
            "errors.calendar.kindUnknown",
            problem.GetProperty("errors").GetProperty("kind")[0].GetString());

        // A seeded word goes through, which is also what says the seed ran at all.
        using var accepted = await SendAsync(client, HttpMethod.Post, CalendarEndpoints.Pattern, Entry("meeting"), token);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task ARetiredWordStopsBeingOfferedAndLeavesTheEntriesAlone()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(CoordinatorVid, "IT-EC", token);
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var web = await SignedInAsync(WebCoordinatorVid, token);

        // A word, an entry written with it, and then the word retired.
        using var created = await SendAsync(web, HttpMethod.Post, CalendarKindEndpoints.Pattern, Word("hangar-talk"), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var word = await created.Content.ReadFromJsonAsync<JsonElement>(token);
        var id = word.GetProperty("id").GetInt64();

        using var written = await SendAsync(coordinator, HttpMethod.Post, CalendarEndpoints.Pattern, Entry("hangar-talk"), token);
        Assert.Equal(HttpStatusCode.Created, written.StatusCode);
        var entryId = (await written.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        using var retired = await SendAsync(
            web,
            HttpMethod.Put,
            $"{CalendarKindEndpoints.Pattern}/{id}",
            Word("hangar-talk", active: false, rowVersion: word.GetProperty("rowVersion").GetString()),
            token);

        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);

        // The entry keeps the word it was written with: there is no foreign key, on purpose, and
        // nothing rewrites rows that are already out there.
        var entry = await coordinator.GetFromJsonAsync<JsonElement>(
            $"{CalendarEndpoints.Pattern}/{entryId}",
            token);

        Assert.Equal("hangar-talk", entry.GetProperty("kind").GetString());

        // But nobody may file a new one under it, which is the whole difference between retiring a
        // word and deleting it.
        using var tooLate = await SendAsync(coordinator, HttpMethod.Post, CalendarEndpoints.Pattern, Entry("hangar-talk"), token);
        Assert.Equal(HttpStatusCode.BadRequest, tooLate.StatusCode);

        // And it is gone from the bootstrap, which is where every chip and every select reads it.
        var me = await coordinator.GetFromJsonAsync<JsonElement>("/api/me", token);
        var offered = me.GetProperty("calendarKinds").EnumerateArray()
            .Select(kind => kind.GetProperty("key").GetString())
            .ToArray();

        Assert.DoesNotContain("hangar-talk", offered);
        Assert.Contains("meeting", offered);

        // The bootstrap carries the colour too, because a visitor draws the chip and may not read
        // `/api/calendar-kinds`.
        var meeting = me.GetProperty("calendarKinds").EnumerateArray()
            .First(kind => kind.GetProperty("key").GetString() == "meeting");

        Assert.False(string.IsNullOrWhiteSpace(meeting.GetProperty("colour").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(meeting.GetProperty("label").GetProperty("en").GetString()));
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static object Word(string key, string colour = "blue", bool active = true, string? rowVersion = null) => new
    {
        key,
        label = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = key, ["en"] = key },
        colour,
        sort = 90,
        isActive = active,
        rowVersion = rowVersion ?? "0001-01-01T00:00:00",
    };

    private static object Entry(string kind) => new
    {
        ownerDepartment = nameof(Department.ED),
        visibility = nameof(Visibility.Public),
        kind,
        title = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Prova", ["en"] = "Test" },
        description = (Dictionary<string, string>?)null,
        startsAtUtc = DateTime.UtcNow.AddDays(3).ToString("O"),
        endsAtUtc = (string?)null,
        allDay = false,
        url = (string?)null,
    };

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(payload),
        };

        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task SeedUserAsync(int vid, string position, CancellationToken cancellationToken)
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
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (!await database.UserStaffPositions.AnyAsync(
                row => row.Vid == vid && row.Position == position,
                cancellationToken))
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
