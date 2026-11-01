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
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What the reader may do on an event and on its positions, said by the server (issue #427, note
/// 2026-11-01-le-azioni-di-un-evento-dal-server): the page of an event reads its buttons and its tabs from <c>actions</c>, the ATC its
/// «New position» from the head's <c>canAddPositions</c> and «Save» and «Delete» from the position's own <c>editable</c> — each the one
/// handler's answer on the row, through the real host. Until #427 the page asked the bootstrap by name and department: a grant on one event
/// drew the buttons on every event, the staff of a FIR «Save» on the positions of every FIR, and a position's page always said
/// <c>editable: false</c>.
/// <para>The readers: the staff of the department (grants without a scope), a grant on one event, the chief of a FIR (the team of their
/// FIR, from the installation's position grants), an advisor of the ATC operations (the events read and their ATC written from outside, from
/// the same grants), and whoever only reads the events or their ATC.</para>
/// <para>⚠️ The members are seeded <b>without an address</b>, and every event is written by the installation, as in
/// <see cref="EventsAtcOneEventTests"/>: what each holds is the grants of the test and the position it is given.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsActionsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The events module's range handed to #427: 761410–761429.
    private const int DepartmentVid = 761410;
    private const int OneEventVid = 761411;
    private const int ChiefVid = 761412;
    private const int OutsideVid = 761413;
    private const int ReaderVid = 761414;

    // Two FIRs nobody has, the ones this host's directory knows: the chief's position is in the first.
    private const string Fir = "XXAA";
    private const string OtherFir = "XXBB";
    private const string Tower = "XXAA_TWR";
    private const string Approach = "XXBB_APP";

    private const string SlugStem = "evt-test-fix427";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly int[] Vids = [DepartmentVid, OneEventVid, ChiefVid, OutsideVid, ReaderVid];

    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IFirDirectory, TwoFirs>();
            services.AddScoped<IAtcPositionDirectory, TwoPositions>();
        }));
        var token = TestContext.Current.CancellationToken;
        var code = _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value.Code;

        await SeedMemberAsync(DepartmentVid, position: null, token);
        await SeedMemberAsync(OneEventVid, position: null, token);
        await SeedMemberAsync(ChiefVid, $"{Fir}-CH", token);
        await SeedMemberAsync(OutsideVid, $"{code}-AOA7", token);
        await SeedMemberAsync(ReaderVid, position: null, token);

        await ForgetAsync(token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows, the members' grants and their positions.</summary>
    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserStaffPositions
                .Where(row => Vids.Contains(row.Vid))
                .ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    [Fact]
    public async Task TheStaffOfTheDepartmentHaveEveryActionTheStateOfTheEventLeaves()
    {
        var token = TestContext.Current.CancellationToken;
        var draft = await EventRowAsync("draft", token);
        var published = await EventRowAsync("published", token, row => row.Status = PublishStatus.Published);
        var cancelled = await EventRowAsync("cancelled", token, row => row.CancelledAt = DateTime.UtcNow.AddDays(-1));
        var over = await EventRowAsync("over", token, row =>
        {
            row.StartsAtUtc = DateTime.UtcNow.AddDays(-3);
            row.EndsAtUtc = DateTime.UtcNow.AddDays(-3).AddHours(4);
        });

        await GrantAsync(
            DepartmentVid,
            scope: null,
            token,
            EventsPermissions.View,
            EventsPermissions.Edit,
            EventsPermissions.Delete,
            EventsPermissions.RoutesView,
            EventsPermissions.RoutesEdit,
            EventsPermissions.BookingsView,
            EventsPermissions.BookingsEdit,
            EventsPermissions.AtcView,
            EventsPermissions.ReportsView);
        using var staff = await SignedInAsync(DepartmentVid, token);

        var actions = await ActionsAsync(staff, draft, token);
        Assert.Equal(
            ["canCancel", "canDelete", "canDuplicate", "canEdit", "canPublish", "canReadAtc", "canReadBookings", "canReadReports", "canReadRoutes",
                "canWriteBookings", "canWriteRoutes"],
            Granted(actions));

        // Published once; cancelled, or over, never cancelled again — the state the verbs refuse, said with the rest.
        Assert.DoesNotContain("canPublish", Granted(await ActionsAsync(staff, published, token)));
        Assert.Contains("canCancel", Granted(await ActionsAsync(staff, published, token)));

        var ofCancelled = Granted(await ActionsAsync(staff, cancelled, token));
        Assert.DoesNotContain("canPublish", ofCancelled);
        Assert.DoesNotContain("canCancel", ofCancelled);
        Assert.Contains("canEdit", ofCancelled);
        Assert.Contains("canDuplicate", ofCancelled);

        var ofOver = Granted(await ActionsAsync(staff, over, token));
        Assert.Contains("canPublish", ofOver);
        Assert.DoesNotContain("canCancel", ofOver);

        // What «Cancel» answers is the event's detail with the reader's actions on it as it now is.
        using var cancel = await staff.PostAsJsonAsync(
            $"{EventEndpoints.Pattern}/{draft}/cancel",
            new EventCancelRequest(Text("weather"), await RowVersionAsync(draft, token)),
            token);
        var answered = await OkAsync(cancel, token);
        Assert.DoesNotContain("canCancel", Granted(answered.GetProperty("actions")));
        Assert.Contains("canEdit", Granted(answered.GetProperty("actions")));
    }

    [Fact]
    public async Task AGrantOnOneEventDrawsItsActionsOnThatEventAndOnNoOther()
    {
        var token = TestContext.Current.CancellationToken;
        var theirs = await EventRowAsync("theirs", token);
        var other = await EventRowAsync("other", token);

        await GrantAsync(OneEventVid, scope: null, token, EventsPermissions.View);
        await GrantAsync(
            OneEventVid,
            Event.ScopeOf(theirs),
            token,
            EventsPermissions.Edit,
            EventsPermissions.RoutesView,
            EventsPermissions.RoutesEdit,
            EventsPermissions.AtcView,
            EventsPermissions.AtcEdit);
        using var member = await SignedInAsync(OneEventVid, token);

        // Their event: what the grant names. A new event is never theirs (E10i), so no «Duplicate»; nor «Delete», which it does not name.
        Assert.Equal(
            ["canCancel", "canEdit", "canPublish", "canReadAtc", "canReadRoutes", "canWriteRoutes"],
            Granted(await ActionsAsync(member, theirs, token)));

        // Another event of the same department: read, and nothing else.
        Assert.Empty(Granted(await ActionsAsync(member, other, token)));

        // «New position» on their event's ATC; the other one's head is not theirs to read (#311).
        Assert.True((await AtcHeadAsync(member, theirs, token)).GetProperty("canAddPositions").GetBoolean());
        using var refused = await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{other}", token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task TheChiefOfAFirWritesThePositionsOfTheirFirAndReadsTheOthers()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await EventRowAsync("fir", token);
        var tower = await PositionAsync(row, Tower, Fir, token);
        var approach = await PositionAsync(row, Approach, OtherFir, token);

        // The team of a FIR holds the ATC from the installation's position grants, on their FIR.
        using var chief = await SignedInAsync(ChiefVid, token);

        Assert.True((await AtcHeadAsync(chief, row, token)).GetProperty("canAddPositions").GetBoolean());

        // The position's own page says what its row in the list says.
        Assert.True(await EditableAsync(chief, tower, token));
        Assert.Equal(HttpStatusCode.Forbidden, (await chief.GetAsync($"{AtcPositionEndpoints.Pattern}/{approach}", token)).StatusCode);

        var listed = await OkAsync(await chief.GetAsync($"{AtcPositionEndpoints.Pattern}?filter[eventId]={row}&pageSize=100", token), token);
        var only = Assert.Single(listed.GetProperty("items").EnumerateArray());
        Assert.Equal(tower, only.GetProperty("id").GetInt64());
        Assert.True(only.GetProperty("editable").GetBoolean());
    }

    [Fact]
    public async Task TheAtcFromOutsideWritesEveryPositionAndNothingOfTheEvent()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await EventRowAsync("outside", token);
        var tower = await PositionAsync(row, Tower, Fir, token);
        var approach = await PositionAsync(row, Approach, OtherFir, token);

        // The ATC operations read the events and write their ATC from outside: the installation's position grants, on the events' department.
        using var advisor = await SignedInAsync(OutsideVid, token);

        Assert.Equal(["canReadAtc"], Granted(await ActionsAsync(advisor, row, token)));
        Assert.True((await AtcHeadAsync(advisor, row, token)).GetProperty("canAddPositions").GetBoolean());
        Assert.True(await EditableAsync(advisor, tower, token));
        Assert.True(await EditableAsync(advisor, approach, token));
    }

    [Fact]
    public async Task WhoeverOnlyReadsTheEventsHasNoActionOnOne()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await EventRowAsync("read", token);

        await GrantAsync(ReaderVid, scope: null, token, EventsPermissions.View);
        using var reader = await SignedInAsync(ReaderVid, token);

        Assert.Empty(Granted(await ActionsAsync(reader, row, token)));
    }

    [Fact]
    public async Task WhoeverReadsTheAtcOfAnEventAndWritesNoneOfItOpensNoPosition()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await EventRowAsync("atc-read", token);
        var tower = await PositionAsync(row, Tower, Fir, token);

        await GrantAsync(ReaderVid, scope: null, token, EventsPermissions.AtcView);
        using var reader = await SignedInAsync(ReaderVid, token);

        Assert.False((await AtcHeadAsync(reader, row, token)).GetProperty("canAddPositions").GetBoolean());
        Assert.False(await EditableAsync(reader, tower, token));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>The names of the actions the server says yes to, in order.</summary>
    private static List<string> Granted(JsonElement actions) =>
        [.. actions.EnumerateObject().Where(action => action.Value.GetBoolean()).Select(action => action.Name).Order(StringComparer.Ordinal)];

    private static async Task<JsonElement> ActionsAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        (await OkAsync(await client.GetAsync($"{EventEndpoints.Pattern}/{id}", cancellationToken), cancellationToken)).GetProperty("actions");

    private static async Task<JsonElement> AtcHeadAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        await OkAsync(await client.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{id}", cancellationToken), cancellationToken);

    private static async Task<bool> EditableAsync(HttpClient client, long position, CancellationToken cancellationToken) =>
        (await OkAsync(await client.GetAsync($"{AtcPositionEndpoints.Pattern}/{position}", cancellationToken), cancellationToken))
            .GetProperty("editable").GetBoolean();

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

    /// <summary>A draft with a roster some days from today, in the care of the base department of the module, written by the installation.</summary>
    private async Task<long> EventRowAsync(string name, CancellationToken cancellationToken, Action<Event>? change = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var starts = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(25).AddHours(17), DateTimeKind.Utc);

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfo",
            HasRoster = true,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = starts.AddHours(4),
            Visibility = Visibility.Public,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        change?.Invoke(row);

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    private async Task<DateTime> RowVersionAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => row.RowVersion)
            .SingleAsync(cancellationToken);
    }

    /// <summary>A position of the event in its window, with the FIR of its callsign, written by the installation.</summary>
    private async Task<long> PositionAsync(long eventId, string callsign, string fir, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var parent = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == eventId, cancellationToken);

        var position = new EventAtcPosition
        {
            EventId = eventId,
            Callsign = callsign,
            Fir = fir,
            FromUtc = parent.StartsAtUtc,
            ToUtc = parent.EndsAtUtc,
            OwnerDepartment = parent.OwnerDepartment,
            OwnerDepartmentMask = parent.OwnerDepartmentMask,
        };

        database.AtcPositions.Add(position);
        await database.SaveChangesAsync(cancellationToken);
        return position.Id;
    }

    /// <summary>The permissions granted to the member on the base department of the module, on one event or — with no scope — on all of it.</summary>
    private async Task GrantAsync(int vid, string? scope, CancellationToken cancellationToken, params string[] permissions)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        foreach (var permission in permissions)
        {
            database.UserGrants.Add(new UserGrant
            {
                Vid = vid,
                Kind = GrantKind.Permission,
                Value = permission,
                Department = Department.ED,
                ResourceScope = scope,
                Effect = GrantEffect.Grant,
                Reason = SlugStem,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Signed in after the grants: the effective permissions are computed at login.</summary>
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
            var parsed = StaffRoleMap.Parse(position, division.Code, TwoFirs.Ids);
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
            .Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>The airspace of this host: two FIRs of the test's own, and no airport.</summary>
    private sealed class TwoFirs : IFirDirectory
    {
        public static readonly IReadOnlySet<string> Ids = new HashSet<string>([Fir, OtherFir], StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetFirIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Ids);

        public Task<IvaoAirspace> GetAirspaceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new IvaoAirspace(Ids, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        public void Invalidate()
        {
        }
    }

    /// <summary>The positions of this host's division: a tower in one FIR and an approach in the other.</summary>
    private sealed class TwoPositions : IAtcPositionDirectory
    {
        private static readonly AtcPositionDto[] All =
        [
            new(Tower, "TWR", "Test Tower", AirportIcao: null, Fir),
            new(Approach, "APP", "Test Approach", AirportIcao: null, OtherFir),
        ];

        public Task<IReadOnlyList<AtcPositionDto>> ForRatingAsync(Rating rating, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AtcPositionDto>>([]);

        public Task<IReadOnlyList<AtcPositionDto>> OfDivisionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AtcPositionDto>>(All);

        public Task<IReadOnlyDictionary<string, AtcPositionDto>> FindAsync(
            IReadOnlyCollection<string> callsigns,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, AtcPositionDto>>(
                All.Where(position => callsigns.Contains(position.Callsign, StringComparer.OrdinalIgnoreCase))
                    .ToDictionary(position => position.Callsign, StringComparer.OrdinalIgnoreCase));

        public Task<IReadOnlyDictionary<string, AtcPositionMinimum>> MinimaAsync(
            IReadOnlyCollection<string> callsigns,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, AtcPositionMinimum>>(new Dictionary<string, AtcPositionMinimum>());
    }
}
