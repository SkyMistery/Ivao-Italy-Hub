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
using IvaoHub.Modules.Events.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A grant on one event, through the real host and the real endpoints of the events (M4, E10i, issue #235, note
/// 2026-10-09-il-grant-su-una-riga-scrive-la-sua-riga): the member the design grants a permission «on one event alone» (§6.2,
/// <c>events:event:{id}</c>) writes that event and its slots — loads them, creates one, corrects it, takes it away — and nothing of
/// another event, and creates no event. Until E10i the endpoint's authorization passed and the interceptor's guard refused the save:
/// it asked <c>{Area}.Edit</c> without the row's scope.
/// <para>⚠️ The member is seeded <b>without an address</b>, and every event of the class is written by the installation, as in
/// <see cref="EventsSlotsTests"/>: no staff of the events with an address, and no position at all — what the member holds is the
/// grant of the test.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsScopedGrantTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The one VID of the events module's range (CONTRIBUTING.md) left to E10i.
    private const int CollaboratorVid = 761097;

    /// <summary>Two airports of the events of this class, and one away from them: of no country the network has.</summary>
    private const string First = "XEI1";
    private const string Second = "XEI2";
    private const string Away = "XEI3";

    /// <summary>An aircraft type the core knows here.</summary>
    private const string Type = "XEIA";

    private const string SlugStem = "evt-test-e10i";

    private const string Header = "callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly string[] Airports = [First, Second, Away];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        // The recorded answers of the network, as in EventsSlotsTests: no test calls the network.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedCollaboratorAsync(token);
        await SeedReferenceAsync(token);
        await ForgetAsync(token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows, the member's grants, its airports and its type.</summary>
    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.IvaoAirports.Where(airport => Airports.Contains(airport.Icao)).ExecuteDeleteAsync();
            await database.IvaoAircraftTypes.Where(type => type.IcaoCode == Type).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AGrantOnOneEventWritesTheSlotsOfThatEventAndNoneOfAnother()
    {
        var token = TestContext.Current.CancellationToken;
        var day = Day(days: 30);
        var theirs = await EventAsync("theirs", token);
        var other = await EventAsync("other", token);
        var otherSlot = await SlotAsync(other, "XEI201", day, token);

        // EventBookings.Edit on the one event, the way a member outside the positions is enabled on it (design M4 §6.2).
        await GrantAsync(EventsPermissions.BookingsEdit, Event.ScopeOf(theirs), token);
        using var collaborator = await SignedInAsync(token);

        // Their event: a table loaded, a slot created, corrected and taken away.
        var loaded = await OkAsync(
            await collaborator.PostAsJsonAsync(
                $"{EventEndpoints.Pattern}/{theirs}/slots/load",
                new SlotLoadRequest(Table(Line("XEI101", First, day.AddHours(17), Away, day.AddHours(18))), SlotLoadMode.Add),
                token),
            token);
        Assert.Equal(1, loaded.GetProperty("added").GetInt32());

        var created = await CreatedAsync(collaborator, EventSlotEndpoints.Pattern, Slot(theirs, "XEI102", day, stand: null), token);
        var slot = created.GetProperty("id").GetInt64();

        using (var corrected = await collaborator.PutAsJsonAsync($"{EventSlotEndpoints.Pattern}/{slot}", Slot(theirs, "XEI102", day, stand: "B7"), token))
        {
            Assert.Equal("B7", (await OkAsync(corrected, token)).GetProperty("stand").GetString());
        }

        using (var removed = await collaborator.DeleteAsync(new Uri($"{EventSlotEndpoints.Pattern}/{slot}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        Assert.Equal(["XEI101"], (await SlotsAsync(theirs, token)).Select(row => row.Callsign));

        // The other event: nothing, whichever way in.
        using (var load = await collaborator.PostAsJsonAsync(
            $"{EventEndpoints.Pattern}/{other}/slots/load",
            new SlotLoadRequest(Table(Line("XEI202", First, day.AddHours(17), Away, day.AddHours(18))), SlotLoadMode.Add),
            token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, load.StatusCode);
        }

        using (var create = await collaborator.PostAsJsonAsync(EventSlotEndpoints.Pattern, Slot(other, "XEI203", day, stand: null), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        }

        using (var correct = await collaborator.PutAsJsonAsync($"{EventSlotEndpoints.Pattern}/{otherSlot}", Slot(other, "XEI201", day, stand: "C3"), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, correct.StatusCode);
        }

        using (var remove = await collaborator.DeleteAsync(new Uri($"{EventSlotEndpoints.Pattern}/{otherSlot}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);
        }

        var untouched = Assert.Single(await SlotsAsync(other, token));
        Assert.Equal("XEI201", untouched.Callsign);
        Assert.Null(untouched.Stand);
    }

    [Fact]
    public async Task AGrantOnOneEventChangesThatEventAndBringsNoOtherIntoExistence()
    {
        var token = TestContext.Current.CancellationToken;
        var theirs = await EventAsync("event", token);
        var other = await EventAsync("event-other", token);

        await GrantAsync(EventsPermissions.Edit, Event.ScopeOf(theirs), token);
        using var collaborator = await SignedInAsync(token);

        // Their event, changed: the text of the event they collaborate on.
        using (var changed = await collaborator.PutAsJsonAsync($"{EventEndpoints.Pattern}/{theirs}", Payload("event", "changed"), token))
        {
            Assert.Equal(Slug("event") + " changed", (await OkAsync(changed, token)).GetProperty("title").GetProperty("en").GetString());
        }

        // Another event, not changed; and no new event: a new row answers with its own scope, which no grant names.
        using (var refused = await collaborator.PutAsJsonAsync($"{EventEndpoints.Pattern}/{other}", Payload("event-other", "changed"), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using (var created = await collaborator.PostAsJsonAsync(EventEndpoints.Pattern, Payload("event-new", title: null), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
        }

        Assert.Equal(Slug("event-other"), (await EventRowAsync(other, token)).Title.Get("en"));
        Assert.False(await EventExistsAsync(Slug("event-new"), token));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>An instant some days from today, on the hour.</summary>
    private static DateTime Starts(int days) => DateTime.UtcNow.Date.AddDays(days).AddHours(17);

    /// <summary>Midnight of a day some days from today, in UTC: the slots are at hours of it.</summary>
    private static DateTime Day(int days) => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days), DateTimeKind.Utc);

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>The form of one of the class's events, as it was written, with its title changed when one is given.</summary>
    private static EventWriteDto Payload(string name, string? title)
    {
        var starts = Starts(days: 30);
        return new(
            Kind: "rfo",
            PublicSlots: true,
            PrivateSlots: false,
            WholeDivision: false,
            Organizer: EventOrganizer.Division,
            ExternalUrl: null,
            Title: Text(title is null ? Slug(name) : $"{Slug(name)} {title}"),
            Slug: Slug(name),
            Summary: Text(Slug(name)),
            Body: null,
            BannerMediaId: null,
            VisibleFromUtc: null,
            BookingOpensAtUtc: null,
            StartsAtUtc: starts,
            EndsAtUtc: starts.AddHours(5),
            Visibility: Visibility.Public,
            RowVersion: default);
    }

    /// <summary>A public slot leaving the first airport of the event at 17:00 of the day, for an hour; no version, the row as it is.</summary>
    private static EventSlotWriteDto Slot(long eventId, string callsign, DateTime day, string? stand) =>
        new(eventId, callsign, FlightNumber: null, MainAircraftType: Type, OtherAircraftTypes: null, First, day.AddHours(17), Away, day.AddHours(18), stand, RotationCode: null, RotationLeg: null, RowVersion: default);

    /// <summary>A table as a spreadsheet copies it: the header, then a row per line, separated by tabs.</summary>
    private static string Table(params string[] lines) => string.Join("\r\n", [Header, .. lines]) + "\r\n";

    private static string Line(string callsign, string departure, DateTime offBlock, string arrival, DateTime onBlock) =>
        string.Join('\t', callsign, string.Empty, Type, departure, Stamp(offBlock), arrival, Stamp(onBlock), string.Empty, string.Empty, string.Empty);

    /// <summary>An instant as the table writes it: in UTC, to the minute.</summary>
    private static string Stamp(DateTime at) => at.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<JsonElement> CreatedAsync<T>(HttpClient client, string uri, T payload, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(uri, payload, cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

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

    /// <summary>
    /// An RFO with public slots and its two airports, written by the installation — nobody of the events is signed in here —, in
    /// the care of the base department of the module, which the interceptor writes.
    /// </summary>
    private async Task<long> EventAsync(string name, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var payload = Payload(name, title: null);

        var row = new Event
        {
            Slug = payload.Slug,
            Kind = payload.Kind,
            PublicSlots = payload.PublicSlots,
            Organizer = payload.Organizer,
            Title = payload.Title,
            Summary = payload.Summary,
            StartsAtUtc = payload.StartsAtUtc!.Value,
            EndsAtUtc = payload.EndsAtUtc!.Value,
            Visibility = payload.Visibility,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);

        database.Airports.AddRange(
            new EventAirport { EventId = row.Id, Icao = First, Ordinal = 1, OwnerDepartment = row.OwnerDepartment, OwnerDepartmentMask = row.OwnerDepartmentMask },
            new EventAirport { EventId = row.Id, Icao = Second, Ordinal = 2, OwnerDepartment = row.OwnerDepartment, OwnerDepartmentMask = row.OwnerDepartmentMask });
        await database.SaveChangesAsync(cancellationToken);

        return row.Id;
    }

    /// <summary>A public slot of an event, written by the installation, in the event's care.</summary>
    private async Task<long> SlotAsync(long eventId, string callsign, DateTime day, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var slot = new EventSlot
        {
            EventId = eventId,
            Kind = SlotKind.Public,
            EventAirportIcao = First,
            IsArrival = false,
            Callsign = callsign,
            AircraftTypes = [Type],
            DepartureIcao = First,
            ArrivalIcao = Away,
            OffBlockUtc = day.AddHours(17),
            OnBlockUtc = day.AddHours(18),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);
        return slot.Id;
    }

    /// <summary>The slots of an event as stored, read past every filter.</summary>
    private async Task<List<EventSlot>> SlotsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Slots.AsNoTracking()
            .Where(slot => slot.EventId == eventId)
            .OrderBy(slot => slot.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<Event> EventRowAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    private async Task<bool> EventExistsAsync(string slug, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters()
            .AnyAsync(row => row.Slug == slug, cancellationToken);
    }

    /// <summary>
    /// A permission of the events granted to the member on one event alone, written straight to the table as a screen of the module
    /// will write it (the permissions screen never writes a scope). The member's other grants go first: each test holds one.
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
            Reason = "evt-test-e10i",
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Signed in after the grant: the effective permissions are computed at login, and a grant written signs its holder out.</summary>
    private async Task<HttpClient> SignedInAsync(CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, CollaboratorVid, cancellationToken);
        return client;
    }

    /// <summary>The member of the staff the grants are given to: no position and never an address.</summary>
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
        user.LastName = "Events";
        user.Email = null;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The airports and the aircraft type of the class, in the core's snapshots, where a slot's are checked.</summary>
    private async Task SeedReferenceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var icao in Airports)
        {
            if (!await database.IvaoAirports.AnyAsync(airport => airport.Icao == icao, cancellationToken))
            {
                database.IvaoAirports.Add(new IvaoAirport
                {
                    Icao = icao,
                    Name = $"evt-test {icao}",
                    CountryId = "XX",
                    Latitude = 41.8,
                    Longitude = 12.24,
                    SyncedAt = clock.UtcNow,
                });
            }
        }

        if (!await database.IvaoAircraftTypes.AnyAsync(type => type.IcaoCode == Type, cancellationToken))
        {
            database.IvaoAircraftTypes.Add(new IvaoAircraftType { IcaoCode = Type, Model = "evt-test", Manufacturer = "evt-test", SyncedAt = clock.UtcNow });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The events of this class with their rows and what they projected, and the member's grants, whatever a stopped run left.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants
            .Where(grant => grant.Vid == CollaboratorVid)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
