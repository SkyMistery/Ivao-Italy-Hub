using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A grant on one row, in a generated list (note 2026-10-21-il-grant-su-una-riga-elenca-le-sue-righe): it gives no department, and
/// the list holds the rows of its scope — those the single handler lets the holder read — and no other row of the department. Proved
/// on the real endpoints of the events, whose rows answer with the scope of their event, through the real host and the real cookie.
/// <para>⚠️ The member is seeded <b>without a position and without an address</b>, and every row is written by the installation, as
/// in <see cref="EventsScopedGrantTests"/>: what the member holds is the grant of the test, and nothing else.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ScopedGrantListTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    /// <summary>Of no module's range (CONTRIBUTING.md), and grepped free.</summary>
    private const int CollaboratorVid = 773001;

    /// <summary>The pilots of the bookings: nobody signs in as them, and no row of theirs is seeded but the bookings.</summary>
    private const int FirstPilotVid = 773002;
    private const int SecondPilotVid = 773003;

    private const string Airport = "XGL1";
    private const string Away = "XGL2";

    private const string SlugStem = "evt-test-grant-list";

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedCollaboratorAsync(token);
        await ForgetAsync(token);
    }

    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AGrantOnOneEventListsTheBookingsOfThatEventAndNoneOfAnother()
    {
        var token = TestContext.Current.CancellationToken;
        var theirs = await EventAsync("theirs", token);
        var other = await EventAsync("other", token);
        var mine = await BookingAsync(theirs, "XGL101", FirstPilotVid, token);
        var notMine = await BookingAsync(other, "XGL201", SecondPilotVid, token);

        await GrantAsync(EventsPermissions.BookingsView, Event.ScopeOf(theirs), token);
        using var collaborator = await SignedInAsync(token);

        // Their event: its booking, asked for the event as the page asks.
        Assert.Equal([mine], await IdsAsync(collaborator, $"{BookingEndpoints.StaffPattern}?filter[eventId]={theirs}&pageSize=100", token));

        // The other event: nothing, asked for it or for every event.
        Assert.Empty(await IdsAsync(collaborator, $"{BookingEndpoints.StaffPattern}?filter[eventId]={other}&pageSize=100", token));
        var everything = await IdsAsync(collaborator, $"{BookingEndpoints.StaffPattern}?pageSize=100", token);
        Assert.Contains(mine, everything);
        Assert.DoesNotContain(notMine, everything);

        // The detail, as before: theirs read, the other refused.
        using (var read = await collaborator.GetAsync($"{BookingEndpoints.StaffPattern}/{mine}", token))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        using (var refused = await collaborator.GetAsync($"{BookingEndpoints.StaffPattern}/{notMine}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }
    }

    [Fact]
    public async Task AGrantOnOneEventListsThatEventOnlyAndGivesNoDepartment()
    {
        var token = TestContext.Current.CancellationToken;
        var theirs = await EventAsync("event", token);
        var other = await EventAsync("event-other", token);

        await GrantAsync(EventsPermissions.Edit, Event.ScopeOf(theirs), token);
        using var collaborator = await SignedInAsync(token);

        // The list of the events: theirs, a draft, and not the other one — drafts of the department included.
        Assert.Equal([theirs], await IdsAsync(collaborator, $"{EventEndpoints.Pattern}?q={SlugStem}&pageSize=100", token));

        // The detail, as before.
        using (var read = await collaborator.GetAsync($"{EventEndpoints.Pattern}/{theirs}", token))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        using (var refused = await collaborator.GetAsync($"{EventEndpoints.Pattern}/{other}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // No department in the bootstrap: the screens of the department as a whole are not theirs. The permission is, with its scope.
        var me = await collaborator.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Empty(me.GetProperty("user").GetProperty("departments").EnumerateArray());
        Assert.Contains(
            me.GetProperty("permissions").EnumerateArray(),
            permission => permission.GetProperty("name").GetString() == EventsPermissions.Edit
                && permission.GetProperty("resourceScope").GetString() == Event.ScopeOf(theirs));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static async Task<List<long>> IdsAsync(HttpClient client, string uri, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(uri, cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{uri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");

        var page = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64())];
    }

    /// <summary>A draft RFO in the care of the events' department, with its airport, written by the installation.</summary>
    private async Task<long> EventAsync(string name, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var starts = DateTime.UtcNow.Date.AddDays(30).AddHours(17);

        var row = new Event
        {
            Slug = $"{SlugStem}-{name}",
            Kind = "rfo",
            PublicSlots = true,
            Organizer = EventOrganizer.Division,
            Title = Text($"{SlugStem}-{name}"),
            Summary = Text($"{SlugStem}-{name}"),
            StartsAtUtc = starts,
            EndsAtUtc = starts.AddHours(5),
            Visibility = Visibility.Public,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);

        database.Airports.Add(new EventAirport
        {
            EventId = row.Id,
            Icao = Airport,
            Ordinal = 1,
            OwnerDepartment = row.OwnerDepartment,
            OwnerDepartmentMask = row.OwnerDepartmentMask,
        });
        await database.SaveChangesAsync(cancellationToken);

        return row.Id;
    }

    /// <summary>A public slot of an event and a pilot's booking of it, written by the installation.</summary>
    private async Task<long> BookingAsync(long eventId, string callsign, int pilotVid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var day = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(30), DateTimeKind.Utc);

        var slot = new EventSlot
        {
            EventId = eventId,
            Kind = SlotKind.Public,
            EventAirportIcao = Airport,
            IsArrival = false,
            Callsign = callsign,
            AircraftTypes = ["XGLA"],
            DepartureIcao = Airport,
            ArrivalIcao = Away,
            OffBlockUtc = day.AddHours(17),
            OnBlockUtc = day.AddHours(18),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);

        var booking = new EventBooking
        {
            EventId = eventId,
            SlotId = slot.Id,
            BookerVid = pilotVid,
            AircraftIcao = "XGLA",
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Bookings.Add(booking);
        await database.SaveChangesAsync(cancellationToken);
        return booking.Id;
    }

    /// <summary>
    /// A permission of the events granted to the member on one event alone, written straight to the table as a screen of the module
    /// writes it. The member's other grants go first: each test holds one.
    /// </summary>
    private async Task GrantAsync(string permission, string scope, CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        await database.UserGrants.Where(grant => grant.Vid == CollaboratorVid).ExecuteDeleteAsync(cancellationToken);
        database.UserGrants.Add(new UserGrant
        {
            Vid = CollaboratorVid,
            Kind = GrantKind.Permission,
            Value = permission,
            Department = Department.ED,
            ResourceScope = scope,
            Effect = GrantEffect.Grant,
            Reason = SlugStem,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Signed in after the grant: the effective permissions are computed at login.</summary>
    private async Task<HttpClient> SignedInAsync(CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, CollaboratorVid, cancellationToken);
        return client;
    }

    /// <summary>The member the grants are given to: staff, no position and never an address.</summary>
    private async Task SeedCollaboratorAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == CollaboratorVid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = CollaboratorVid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "Grant list";
        user.Email = null;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The events of this class with their rows, and the member's grants, whatever a stopped run left.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants
            .Where(grant => grant.Vid == CollaboratorVid)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
