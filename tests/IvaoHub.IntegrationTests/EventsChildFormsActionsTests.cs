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
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Reports;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The child forms of an event — its routes, airports, slots, questions, activities and rules of award — draw «Save» and «Delete» from
/// the event's <c>actions</c> (issue #444, after #427): <c>canWriteRoutes</c> for a route, <c>canEdit</c> for an airport, a question and a
/// rule, <c>canWriteBookings</c> for a slot and an activity. A row of an event is judged on the event's care and scope as the event is now
/// (<c>EventChildren.AdoptAsync</c>, note 2026-11-05-la-cura-che-una-riga-prende-dal-genitore), so the event's answer is the row's. Here,
/// through the real host, each reader's flag on the event is what the server does on each of its rows: a writer deletes it, and whoever the
/// flag says no is refused 403.
/// <para>The readers: the staff of the department (grants without a scope), a grant on one event — on that event and on another one —, the
/// chief of a FIR (the team of their FIR, from the installation's position grants, who reads the events from a grant), and whoever only
/// reads.</para>
/// <para>⚠️ Green on the code before #444 too: the server did not change, the pages did. What this class proves is the fact the pages now
/// rest on; <c>childFormsFromServer.test.tsx</c> is the test that was red on the old pages.</para>
/// <para>⚠️ The members are seeded <b>without an address</b>, and every row is written by the installation, as in
/// <see cref="EventsActionsTests"/>.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsChildFormsActionsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The events module's range handed to #444: 761415–761429.
    private const int DepartmentVid = 761415;
    private const int OneEventVid = 761416;
    private const int ChiefVid = 761417;
    private const int ReaderVid = 761418;

    // A FIR nobody has, the one this host's directory knows: the chief's position is in it.
    private const string Fir = "XXAA";

    private const string SlugStem = "evt-test-fix444";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly int[] Vids = [DepartmentVid, OneEventVid, ChiefVid, ReaderVid];

    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IFirDirectory, OneFir>()));
        var token = TestContext.Current.CancellationToken;

        await SeedMemberAsync(DepartmentVid, position: null, token);
        await SeedMemberAsync(OneEventVid, position: null, token);
        await SeedMemberAsync(ChiefVid, $"{Fir}-CH", token);
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
    public async Task TheStaffOfTheDepartmentWriteEveryRowOfTheEvent()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await EventRowAsync("department", token);
        var children = await ChildrenAsync(row, token);

        await GrantAsync(
            DepartmentVid,
            scope: null,
            token,
            EventsPermissions.View,
            EventsPermissions.Edit,
            EventsPermissions.RoutesView,
            EventsPermissions.RoutesEdit,
            EventsPermissions.BookingsView,
            EventsPermissions.BookingsEdit);
        using var staff = await SignedInAsync(DepartmentVid, token);

        var actions = await ActionsAsync(staff, row, token);
        Assert.True(actions.GetProperty("canEdit").GetBoolean());
        Assert.True(actions.GetProperty("canWriteRoutes").GetBoolean());
        Assert.True(actions.GetProperty("canWriteBookings").GetBoolean());

        await AssertTheFlagsAreTheServersAsync(staff, actions, children, token);
    }

    [Fact]
    public async Task AGrantOnOneEventWritesTheRowsOfThatEventAndOfNoOther()
    {
        var token = TestContext.Current.CancellationToken;
        var theirs = await EventRowAsync("theirs", token);
        var other = await EventRowAsync("other", token);
        var ofTheirs = await ChildrenAsync(theirs, token);
        var ofOther = await ChildrenAsync(other, token);

        await GrantAsync(
            OneEventVid,
            scope: null,
            token,
            EventsPermissions.View,
            EventsPermissions.RoutesView,
            EventsPermissions.BookingsView);
        await GrantAsync(
            OneEventVid,
            Event.ScopeOf(theirs),
            token,
            EventsPermissions.Edit,
            EventsPermissions.RoutesEdit,
            EventsPermissions.BookingsEdit);
        using var member = await SignedInAsync(OneEventVid, token);

        // Another event of the same department: the bootstrap names Events.Edit there, the server says no to every row of it.
        var onOther = await ActionsAsync(member, other, token);
        Assert.False(onOther.GetProperty("canEdit").GetBoolean());
        Assert.False(onOther.GetProperty("canWriteRoutes").GetBoolean());
        Assert.False(onOther.GetProperty("canWriteBookings").GetBoolean());
        await AssertTheFlagsAreTheServersAsync(member, onOther, ofOther, token);

        // Their event: every row of it.
        var onTheirs = await ActionsAsync(member, theirs, token);
        Assert.True(onTheirs.GetProperty("canEdit").GetBoolean());
        Assert.True(onTheirs.GetProperty("canWriteRoutes").GetBoolean());
        Assert.True(onTheirs.GetProperty("canWriteBookings").GetBoolean());
        await AssertTheFlagsAreTheServersAsync(member, onTheirs, ofTheirs, token);
    }

    [Fact]
    public async Task TheChiefOfAFirWritesNoRowOfAnEvent()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await EventRowAsync("fir", token);
        var children = await ChildrenAsync(row, token);

        // The team of a FIR holds the ATC of their FIR from the installation's position grants; the rows of an event have no FIR.
        await GrantAsync(ChiefVid, scope: null, token, EventsPermissions.View);
        using var chief = await SignedInAsync(ChiefVid, token);

        var actions = await ActionsAsync(chief, row, token);
        Assert.False(actions.GetProperty("canEdit").GetBoolean());
        Assert.False(actions.GetProperty("canWriteRoutes").GetBoolean());
        Assert.False(actions.GetProperty("canWriteBookings").GetBoolean());

        await AssertTheFlagsAreTheServersAsync(chief, actions, children, token);
    }

    [Fact]
    public async Task WhoeverOnlyReadsTheEventWritesNoRowOfIt()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await EventRowAsync("read", token);
        var children = await ChildrenAsync(row, token);

        await GrantAsync(
            ReaderVid,
            scope: null,
            token,
            EventsPermissions.View,
            EventsPermissions.RoutesView,
            EventsPermissions.BookingsView);
        using var reader = await SignedInAsync(ReaderVid, token);

        var actions = await ActionsAsync(reader, row, token);
        Assert.False(actions.GetProperty("canEdit").GetBoolean());
        Assert.False(actions.GetProperty("canWriteRoutes").GetBoolean());
        Assert.False(actions.GetProperty("canWriteBookings").GetBoolean());

        await AssertTheFlagsAreTheServersAsync(reader, actions, children, token);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    /// <summary>The rows of one event, each with the address the engine deletes it at and the flag of the event its form reads.</summary>
    private sealed record Child(string Pattern, long Id, string Flag);

    /// <summary>
    /// Each row deleted by the reader: a writer deletes it (204), whoever the flag says no is refused (403). The slot goes before the
    /// airport, which keeps a slot at it.
    /// </summary>
    private static async Task AssertTheFlagsAreTheServersAsync(
        HttpClient client,
        JsonElement actions,
        IReadOnlyList<Child> children,
        CancellationToken cancellationToken)
    {
        foreach (var child in children)
        {
            var writes = actions.GetProperty(child.Flag).GetBoolean();
            using var deleted = await client.DeleteAsync($"{child.Pattern}/{child.Id}", cancellationToken);

            Assert.True(
                deleted.StatusCode == (writes ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden),
                $"{child.Pattern}/{child.Id} with {child.Flag} = {writes}: {deleted.StatusCode}: "
                    + await deleted.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static async Task<JsonElement> ActionsAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"{EventEndpoints.Pattern}/{id}", cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("actions");
    }

    /// <summary>A draft with its own airports and public slots some days from today, in the care of the base department, written by the installation.</summary>
    private async Task<long> EventRowAsync(string name, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var starts = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(25).AddHours(17), DateTimeKind.Utc);

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfo",
            PublicSlots = true,
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

    /// <summary>One row of each kind on the event, in its care, written by the installation: the slot is listed before the airport.</summary>
    private async Task<IReadOnlyList<Child>> ChildrenAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var parent = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == eventId, cancellationToken);
        var owner = parent.OwnerDepartment;
        var mask = parent.OwnerDepartmentMask;

        var route = new EventRoute
        {
            EventId = eventId,
            DepartureIcao = "XXAA",
            ArrivalIcao = "XXBB",
            Route = "DCT",
            OwnerDepartment = owner,
            OwnerDepartmentMask = mask,
        };
        var slot = new EventSlot
        {
            EventId = eventId,
            Kind = SlotKind.Public,
            EventAirportIcao = "XXAA",
            Callsign = "XXX444",
            AircraftTypes = ["A320"],
            DepartureIcao = "XXAA",
            ArrivalIcao = "XXBB",
            OffBlockUtc = parent.StartsAtUtc,
            OnBlockUtc = parent.StartsAtUtc.AddHours(1),
            OwnerDepartment = owner,
            OwnerDepartmentMask = mask,
        };
        // Not the slot's airport: an airport some slots are at keeps them, and is not deleted.
        var airport = new EventAirport { EventId = eventId, Icao = "XXCC", Ordinal = 2, OwnerDepartment = owner, OwnerDepartmentMask = mask };
        var question = new EventQuestion
        {
            EventId = eventId,
            Ordinal = 1,
            Type = QuestionType.YesNo,
            Label = Text(SlugStem),
            OwnerDepartment = owner,
            OwnerDepartmentMask = mask,
        };
        var activity = new EventActivity
        {
            EventId = eventId,
            Name = Text(SlugStem),
            FromUtc = parent.StartsAtUtc,
            ToUtc = parent.StartsAtUtc.AddHours(2),
            SlotMinutes = 30,
            SeatsPerSlot = 4,
            OwnerDepartment = owner,
            OwnerDepartmentMask = mask,
        };
        var rule = new EventAwardRule
        {
            EventId = eventId,
            AwardId = 1,
            Criterion = AwardCriterion.AnyFlight,
            Kind = AwardRules.KindOf(AwardCriterion.AnyFlight),
            OwnerDepartment = owner,
            OwnerDepartmentMask = mask,
        };

        database.AddRange(route, slot, airport, question, activity, rule);
        await database.SaveChangesAsync(cancellationToken);

        return
        [
            new(EventRouteEndpoints.Pattern, route.Id, "canWriteRoutes"),
            new(EventSlotEndpoints.Pattern, slot.Id, "canWriteBookings"),
            new(EventAirportEndpoints.Pattern, airport.Id, "canEdit"),
            new(EventQuestionEndpoints.Pattern, question.Id, "canEdit"),
            new(EventActivityEndpoints.Pattern, activity.Id, "canWriteBookings"),
            new(EventAwardRuleEndpoints.Pattern, rule.Id, "canEdit"),
        ];
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
            .Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value))
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
