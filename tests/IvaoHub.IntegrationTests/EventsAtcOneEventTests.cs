using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The heads of the events with a roster, for the staff of the ATC, read with a grant of <c>EventAtc.View</c> on one event (issue #311,
/// note 2026-11-01-l-atc-di-un-solo-evento): the list holds that event and no other, and the head of another event is refused — until the
/// fix, the endpoints asked the one handler without a row, which answers «held somewhere», and the member read the heads of every event
/// with a roster still to end. The same permission held without a scope still reads them all, as since E11a.
/// <para>⚠️ The members are seeded <b>without an address</b> and no position, and every event is written by the installation, as in
/// <see cref="EventsScopedGrantTests"/>: what each member holds is the grant of the test, or the chief's position on a FIR of the test's own.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsAtcOneEventTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The events module's range handed to the fix of #311: 761330–761339.
    private const int OneEventVid = 761330;
    private const int DepartmentVid = 761331;
    private const int ChiefVid = 761332;

    // A FIR nobody has, the one this host's directory knows: the chief's position is in it.
    private const string Fir = "XXAA";

    private const string SlugStem = "evt-test-fix311";

    private const string AtcPath = "/staff/events/atc";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly int[] Vids = [OneEventVid, DepartmentVid, ChiefVid];

    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddScoped<IFirDirectory, OneFir>()));
        var token = TestContext.Current.CancellationToken;

        foreach (var vid in Vids)
        {
            await SeedMemberAsync(vid, vid == ChiefVid ? $"{Fir}-CH" : null, token);
        }

        await ForgetAsync(token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows and the members' grants.</summary>
    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);
        await ForgetPositionAsync(CancellationToken.None);
        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    [Fact]
    public async Task AGrantOnOneEventReadsTheHeadOfThatEventAndOfNoOther()
    {
        var token = TestContext.Current.CancellationToken;
        var theirs = await EventRowAsync("theirs", token);
        var other = await EventRowAsync("other", token);

        await GrantAsync(OneEventVid, Event.ScopeOf(theirs), token);
        using var member = await SignedInAsync(OneEventVid, token);

        // The list: their event alone, counted alone.
        var heads = await OkAsync(await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}?q={SlugStem}&pageSize=100", token), token);
        var head = Assert.Single(heads.GetProperty("items").EnumerateArray());
        Assert.Equal(theirs, head.GetProperty("id").GetInt64());
        Assert.Equal(1, heads.GetProperty("total").GetInt32());

        // The head of their event, read; of the other, refused.
        var read = await OkAsync(await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{theirs}", token), token);
        Assert.Equal(Slug("theirs"), read.GetProperty("slug").GetString());

        using (var refused = await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{other}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // The menu leads to the list, which shows their event.
        Assert.Contains(AtcPath, StaffPaths(await member.GetFromJsonAsync<JsonElement>("/api/me", token)));
    }

    [Fact]
    public async Task HeldWithoutAScopeTheHeadsOfEveryEventAreRead()
    {
        var token = TestContext.Current.CancellationToken;
        var first = await EventRowAsync("first", token);
        var second = await EventRowAsync("second", token);

        await GrantAsync(DepartmentVid, scope: null, token);
        using var member = await SignedInAsync(DepartmentVid, token);

        var heads = await OkAsync(await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}?q={SlugStem}&pageSize=100", token), token);
        Assert.Equal(
            [first, second],
            heads.GetProperty("items").EnumerateArray().Select(head => head.GetProperty("id").GetInt64()).Order());

        foreach (var id in new[] { first, second })
        {
            await OkAsync(await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{id}", token), token);
        }
    }

    [Fact]
    public async Task AGrantOnOneEventFindsNoHeadOfAnEventThatIsNotThere()
    {
        var token = TestContext.Current.CancellationToken;
        var theirs = await EventRowAsync("theirs", token);
        var noRoster = await EventRowAsync("no-roster", token, hasRoster: false);
        var gone = await EventRowAsync("gone", token);
        await EventsTestRows.ForgetAsync(_factory.Services, Slug("gone"), token);

        await GrantAsync(OneEventVid, Event.ScopeOf(theirs), token);
        using var member = await SignedInAsync(OneEventVid, token);

        // Not there, or with no roster: 404 before any question of who reads it, as for whoever holds the permission without a scope.
        foreach (var id in new[] { noRoster, gone })
        {
            using var missing = await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{id}", token);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }

    [Fact]
    public async Task AChiefOfAFirReadsTheHeadsOfEveryEventThroughTheTeamOfTheirFir()
    {
        var token = TestContext.Current.CancellationToken;
        var first = await EventRowAsync("first", token);
        var second = await EventRowAsync("second", token);

        // The team of a FIR holds EventAtc.View from the installation's position grants, without a scope: the events carry no FIR.
        using var chief = await SignedInAsync(ChiefVid, token);

        var heads = await OkAsync(await chief.GetAsync($"{AtcPositionEndpoints.EventsPattern}?q={SlugStem}&pageSize=100", token), token);
        Assert.Equal(
            [first, second],
            heads.GetProperty("items").EnumerateArray().Select(head => head.GetProperty("id").GetInt64()).Order());

        foreach (var id in new[] { first, second })
        {
            await OkAsync(await chief.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{id}", token), token);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static List<string> StaffPaths(JsonElement me) =>
        [.. me.GetProperty("navigation").GetProperty("staff").EnumerateArray().Select(entry => entry.GetProperty("path").GetString()!)];

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
    }

    /// <summary>A draft with a roster (or none) some days from today, in the care of the base department of the module, written by the installation.</summary>
    private async Task<long> EventRowAsync(string name, CancellationToken cancellationToken, bool hasRoster = true)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var starts = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(25).AddHours(17), DateTimeKind.Utc);

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfo",
            HasRoster = hasRoster,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = starts.AddHours(4),
            Visibility = Visibility.Public,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    /// <summary>
    /// <c>EventAtc.View</c> granted to the member on the base department of the module, on one event or — with no scope — on all of it,
    /// written straight to the table. The member's other grants go first: each test holds one.
    /// </summary>
    private async Task GrantAsync(int vid, string? scope, CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        await database.UserGrants.Where(grant => grant.Vid == vid).ExecuteDeleteAsync(cancellationToken);
        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = EventsPermissions.AtcView,
            Department = Department.ED,
            ResourceScope = scope,
            Effect = GrantEffect.Grant,
            Reason = SlugStem,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Signed in after the grant: the effective permissions are computed at login.</summary>
    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");

        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>A member of the staff the grants are given to, with one position or none, and never an address.</summary>
    private async Task SeedMemberAsync(int vid, string? position, CancellationToken cancellationToken)
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
        user.LastName = "Events";
        user.Email = null;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.UserStaffPositions.Where(row => row.Vid == vid).ExecuteDeleteAsync(cancellationToken);
        if (position is not null)
        {
            var division = scope.ServiceProvider.GetRequiredService<IOptions<DivisionOptions>>().Value;
            var parsed = StaffRoleMap.Parse(position, division.Code, OneFir.Ids);
            Assert.NotNull(parsed);
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = parsed.Raw,
                Department = parsed.Department,
                Level = parsed.Level,
                Fir = parsed.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The events of this class with their rows, and the members' grants, whatever a stopped run left.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants
            .Where(grant => grant.Vid == OneEventVid || grant.Vid == DepartmentVid)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>The chief's position, gone with the class.</summary>
    private async Task ForgetPositionAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserStaffPositions
            .Where(row => row.Vid == ChiefVid)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>The airspace of this host: one FIR of the test's own, and no airport.</summary>
    private sealed class OneFir : IFirDirectory
    {
        public static readonly IReadOnlySet<string> Ids = new HashSet<string>([Fir], StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetFirIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Ids);

        public Task<IvaoAirspace> GetAirspaceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new IvaoAirspace(Ids, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        public void Invalidate()
        {
        }
    }
}
