using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
/// The acceptance of G6: the one calendar of the division, over the wire.
/// <para>Two kinds of row share the table and are read the same way — the entries the staff writes
/// and the entries a module projects — and the only place they differ is that nobody may write the
/// second kind. That is the thing worth testing, because the rule is not a permission and there is
/// no permission that lifts it (design M1 section 4).</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class CalendarEndToEndTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // A range of this class's own: the suite shares one database, and two classes on the same VID
    // are one row (HANDOFF section 19).
    private const int EventsCoordinatorVid = 660001;
    private const int SuperadminVid = 660002;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ProjectedEntriesAreReadOnly()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        var mine = await SeedEntryAsync(Department.ED, ProjectionSource.Core, Visibility.Public, token);
        var mirrored = await SeedEntryAsync(Department.ED, "events", Visibility.Public, token);

        using var staff = _factory.CreateApiClient();
        await _factory.SignInAsync(staff, EventsCoordinatorVid, token);

        // Both rows are in the list: a projection is shown, and being able to see it is the whole
        // point of one calendar.
        var listed = await staff.GetFromJsonAsync<JsonElement>(CalendarEndpoints.Pattern, token);
        var rows = listed.GetProperty("items").EnumerateArray().ToArray();

        Assert.Contains(rows, row => row.GetProperty("id").GetInt64() == mine);
        Assert.Contains(rows, row => row.GetProperty("id").GetInt64() == mirrored);

        // And the list says which is which, on the same answer the engine refuses the write on.
        Assert.False(Row(rows, mine).GetProperty("isProjection").GetBoolean());
        Assert.True(Row(rows, mirrored).GetProperty("isProjection").GetBoolean());

        // The entry the staff wrote is theirs to change...
        using var updated = await SendAsync(staff, HttpMethod.Put, $"{CalendarEndpoints.Pattern}/{mine}", Payload(Department.ED), token);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        // ...and the mirror is nobody's, held by the very same permission.
        using var refused = await SendAsync(staff, HttpMethod.Put, $"{CalendarEndpoints.Pattern}/{mirrored}", Payload(Department.ED), token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var undeletable = await SendAsync(staff, HttpMethod.Delete, $"{CalendarEndpoints.Pattern}/{mirrored}", payload: null, token);
        Assert.Equal(HttpStatusCode.Forbidden, undeletable.StatusCode);

        // ⚠️ Not even a super administrator, and that is the difference between this and a
        // permission: there is nothing to hold. A change here would be undone at the next save of
        // the row it mirrors, so being allowed to make it would only be a way of losing work.
        using var superadmin = _factory.CreateApiClient();
        await _factory.SignInAsync(superadmin, SuperadminVid, token);

        using var refusedToo = await SendAsync(superadmin, HttpMethod.Put, $"{CalendarEndpoints.Pattern}/{mirrored}", Payload(Department.ED), token);
        Assert.Equal(HttpStatusCode.Forbidden, refusedToo.StatusCode);
    }

    [Fact]
    public async Task TwoEntriesOfOneDepartmentCanBothExist()
    {
        // `(source_module, source_id)` is unique, and every entry the staff writes belongs to the
        // core: without an identifier of its own the second one collides with the first. Nothing
        // had ever created a staff entry before this phase, so this is the first time it is asked.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var staff = _factory.CreateApiClient();
        await _factory.SignInAsync(staff, EventsCoordinatorVid, token);

        using var first = await SendAsync(staff, HttpMethod.Post, CalendarEndpoints.Pattern, Payload(Department.ED), token);
        using var second = await SendAsync(staff, HttpMethod.Post, CalendarEndpoints.Pattern, Payload(Department.ED), token);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task CalendarPublicHidesDepartmentEntries()
    {
        var token = TestContext.Current.CancellationToken;

        var open = await SeedEntryAsync(Department.ED, ProjectionSource.Core, Visibility.Public, token);
        var members = await SeedEntryAsync(Department.ED, ProjectionSource.Core, Visibility.Members, token);
        var ownDepartment = await SeedEntryAsync(Department.ED, ProjectionSource.Core, Visibility.Department, token);

        using var anonymous = _factory.CreateApiClient();

        // What `/calendar` reads is the data block, exactly as a page would: there is no second
        // reader of the table, so there is no second place to get visibility wrong.
        var answered = await anonymous.GetFromJsonAsync<JsonElement>(BlockDataUri(), token);
        var ids = Ids(answered);

        Assert.Contains(open, ids);
        Assert.DoesNotContain(members, ids);
        Assert.DoesNotContain(ownDepartment, ids);
    }

    [Fact]
    public async Task AWindowAnswersForTheDaysAGridDraws()
    {
        // What a screen asks that a block does not: a month grid showing September is showing
        // September, not "the next thirty-one days". An entry in the past is inside the window of a
        // grid drawn around it and outside every relative range there is.
        var token = TestContext.Current.CancellationToken;

        var lastWeek = DateTime.UtcNow.AddDays(-7);
        var past = await SeedEntryAsync(
            Department.ED, ProjectionSource.Core, Visibility.Public, token, startsAt: lastWeek);

        using var anonymous = _factory.CreateApiClient();

        // "Coming up" cannot see it, and that is right: it is in the past.
        var upcoming = await anonymous.GetFromJsonAsync<JsonElement>(BlockDataUri(), token);
        Assert.DoesNotContain(past, Ids(upcoming));

        // A window that contains it does.
        var window = new JsonObject
        {
            ["from"] = BlockProps.Instant(lastWeek.AddDays(-1)),
            ["to"] = BlockProps.Instant(lastWeek.AddDays(1)),
            ["limit"] = 50,
        };

        var framed = await anonymous.GetFromJsonAsync<JsonElement>(BlockDataUri(window), token);
        Assert.Contains(past, Ids(framed));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static JsonElement Row(JsonElement[] rows, long id) =>
        rows.Single(row => row.GetProperty("id").GetInt64() == id);

    private static long[] Ids(JsonElement answer) =>
        [.. answer.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64())];

    /// <summary>The calendar block, asked the way the browser asks it.</summary>
    private static Uri BlockDataUri(JsonNode? props = null)
    {
        var body = (props ?? new JsonObject { ["limit"] = 50 }).ToJsonString();
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(body))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new Uri($"/api/blocks/data/{CoreBlocks.Calendar}?props={encoded}", UriKind.Relative);
    }

    private static object Payload(Department department, Visibility visibility = Visibility.Public) => new
    {
        ownerDepartment = department.ToString(),
        visibility = visibility.ToString(),
        kind = "meeting",
        title = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Riunione", ["en"] = "Meeting" },
        description = (Dictionary<string, string>?)null,
        startsAtUtc = DateTime.UtcNow.AddDays(1).ToString("O"),
        endsAtUtc = (string?)null,
        allDay = false,
        url = (string?)null,
    };

    /// <summary>
    /// One row straight into the table, so that a projection can exist without a module to write
    /// it: what is under test is how the resource treats a row it does not own, not how the
    /// interceptor produces one.
    /// </summary>
    private async Task<long> SeedEntryAsync(
        Department department,
        string sourceModule,
        Visibility visibility,
        CancellationToken cancellationToken,
        DateTime? startsAt = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var entry = new CalendarEntry
        {
            OwnerDepartment = department,
            Visibility = visibility,
            Kind = "meeting",
            SourceModule = sourceModule,
            SourceId = $"{sourceModule}:{Guid.NewGuid():N}",
            StartsAtUtc = startsAt ?? DateTime.UtcNow.AddDays(2),
            Url = string.Empty,
            Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Voce"),
                new KeyValuePair<string, string>("en", "Entry"),
            ]),
        };

        database.CalendarEntries.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task SeedUserAsync(
        int vid,
        bool isSuperadmin = false,
        string? position = null,
        CancellationToken cancellationToken = default)
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
        user.IsSuperadmin = isSuperadmin;
        user.IsStaff = position is not null;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(
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
