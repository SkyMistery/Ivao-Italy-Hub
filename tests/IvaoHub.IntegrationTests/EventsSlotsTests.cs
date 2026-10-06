using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Export;
using IvaoHub.Modules.Events.Public;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The public slots of an event and their export (M4, E5), through the real host with the division file of this repository: a
/// table is loaded all or nothing, every refusal on its row and its column, the rotations checked and placed, added to the slots
/// there or replacing the free ones; the slots are the bookings' area — the flight operations neither read nor write them —, one
/// slot's form is held to the rules of a load, and the page of the event lists them free; the gate manager reads them with a
/// personal token of <c>events.bookings</c> — not without one, not with one of another audience, never from a draft.
/// <para>⚠️ Everybody here is seeded **without an address**, as in <see cref="EventsStaffTests"/>: the tests of the contacts assert who
/// of the events and of the flight operations receives a message. What each holds comes from the grants of their position.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsSlotsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // E5's in the range the events module owns in the shared database (CONTRIBUTING.md): 761012–761016 and 761062–761067.
    private const int CoordinatorVid = 761012;
    private const int FlightOperationsVid = 761013;
    private const int MemberVid = 761014;

    /// <summary>Two airports of the events of this class, and two away from them: of no country the network has.</summary>
    private const string First = "XED1";
    private const string Second = "XED2";
    private const string Away = "XED3";
    private const string Further = "XED4";

    /// <summary>Two aircraft types the core knows here, and one it does not.</summary>
    private const string TypeA = "XE5A";
    private const string TypeB = "XE5B";
    private const string UnknownType = "XE5Z";

    private const string SlugStem = "evt-test-e5";

    private const string Header = "callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly string[] Airports = [First, Second, Away, Further];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        // The recorded answers of the network: a host started without them asks the network for a token while the reference data
        // are empty (E10b's warning in HANDOFF-M4.md), and no test calls the network.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-EC", token);
        await SeedUserAsync(FlightOperationsVid, "IT-FOAC", token);
        await SeedUserAsync(MemberVid, position: null, token);
        await SeedReferenceAsync(token);
        await ForgetAsync(token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows, its tokens, its airports and types, its positions.</summary>
    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            int[] vids = [CoordinatorVid, FlightOperationsVid, MemberVid];

            await database.UserStaffPositions.Where(position => vids.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.IvaoAirports.Where(airport => Airports.Contains(airport.Icao)).ExecuteDeleteAsync();
            await database.IvaoAircraftTypes.Where(type => type.IcaoCode == TypeA || type.IcaoCode == TypeB).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ATableIsLoadedAllOrNothingWithEveryRefusalOnItsRowAndColumn()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var id = await EventWithAirportsAsync(coordinator, "load", token);
        var day = Day(days: 30);

        // Every row wrong in its own way, and two right: nothing is written, and each refusal is on its row and its column.
        var wrong = Table(
            Line("XEA101", "XA101", TypeA, First, day.AddHours(17), Away, day.AddHours(18), "B1"),
            Line("XEA102", string.Empty, $"{TypeA}/{UnknownType}", First, day.AddHours(17), Away, day.AddHours(18)),
            Line("XEA103", string.Empty, TypeA, Away, day.AddHours(17), Further, day.AddHours(18)),
            Line("XEA104", string.Empty, TypeA, First, "21/11/2026 17:00", Away, day.AddHours(18)),
            Line("XEA105", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18), rotation: "R9", leg: "1"),
            Line("XEA106", string.Empty, TypeA, Further, day.AddHours(19), First, day.AddHours(20), rotation: "R9", leg: "2"),
            Line("XEA101", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18)),
            Line("XEA108", string.Empty, TypeB, Away, day.AddHours(19), Second, day.AddHours(20), stand: "C3"));

        var errors = await RefusedAsync(await LoadAsync(coordinator, id, wrong, SlotLoadMode.Add, token), token);
        Assert.Equal(["events:errors.aircraftUnknown"], errors["rows[3].aircraft_types"]);
        Assert.Equal(["events:errors.slotAwayFromEvent"], errors["rows[4].departure_icao"]);
        Assert.Equal(["events:errors.instantFormat"], errors["rows[5].off_block_utc"]);
        Assert.Equal(["events:errors.chainBroken"], errors["rows[7].departure_icao"]);
        Assert.Equal(["events:errors.slotTwice"], errors["rows[8].callsign"]);
        Assert.Equal(5, errors.Count);
        Assert.Empty(await SlotsAsync(id, token));

        // Right: a rotation of three legs without their places, a slot that arrives, one between the two airports of the event.
        var right = Table(
            Line("XEA201", "XA201", $"{TypeA}/{TypeB}", First, day.AddHours(17), Away, day.AddHours(18), "B12", rotation: "R1"),
            Line("XEA202", "XA202", TypeA, Away, day.AddHours(19), First, day.AddHours(20), rotation: "R1"),
            Line("XEA203", string.Empty, TypeA, First, day.AddHours(21), Further, day.AddHours(22), rotation: "R1"),
            Line("XEA204", string.Empty, TypeB, Away, day.AddHours(16), Second, day.AddHours(17)),
            Line("XEA205", string.Empty, TypeA, First, day.AddHours(20), Second, day.AddHours(21)));

        var loaded = await OkAsync(await LoadAsync(coordinator, id, right, SlotLoadMode.Add, token), token);
        Assert.Equal((5, 0), (loaded.GetProperty("added").GetInt32(), loaded.GetProperty("removed").GetInt32()));

        // The legs placed by their times; each slot at an airport of the event, which it leaves or — the second leg — reaches.
        var slots = (await SlotsAsync(id, token)).ToDictionary(slot => slot.Callsign!);
        Assert.Equal(
            [(First, false, "R1", 1), (First, true, "R1", 2), (First, false, "R1", 3)],
            new[] { "XEA201", "XEA202", "XEA203" }.Select(callsign => slots[callsign]).Select(Shape));
        Assert.Equal((Second, true), (slots["XEA204"].EventAirportIcao, slots["XEA204"].IsArrival));
        Assert.Equal((First, false), (slots["XEA205"].EventAirportIcao, slots["XEA205"].IsArrival));
        Assert.Equal([TypeA, TypeB], slots["XEA201"].AircraftTypes);
        Assert.Equal(("XA201", "B12"), (slots["XEA201"].FlightNumber, slots["XEA201"].Stand));
        Assert.All(slots.Values, slot =>
        {
            Assert.Equal(SlotKind.Public, slot.Kind);
            Assert.Equal(Department.ED, slot.OwnerDepartment);
            Assert.False(slot.Generated);
        });

        // Added again on top: the same flight at the same off block is one slot of the event already; a fourth leg of R1, with its
        // place, joins the three stored (CSV with semicolons and quotes, as a spreadsheet saves it).
        var more = string.Join(
            '\n',
            "callsign;flight_number;aircraft_types;departure_icao;off_block_utc;arrival_icao;on_block_utc;stand;rotation;leg",
            $"XEA205;;{TypeA};{First};{Stamp(day.AddHours(20))};{Second};{Stamp(day.AddHours(21))};;;",
            $"XEA206;\"XA;206\";{TypeA};{Further};{Stamp(day.AddHours(23))};{First};{Stamp(day.AddHours(24))};;R1;4");

        var twice = await RefusedAsync(await LoadAsync(coordinator, id, more, SlotLoadMode.Add, token), token);
        Assert.Equal(["events:errors.slotTwice"], twice["rows[2].callsign"]);
        Assert.Single(twice);

        var added = await OkAsync(
            await LoadAsync(coordinator, id, string.Join('\n', more.Split('\n').Where((_, index) => index != 1)), SlotLoadMode.Add, token),
            token);
        Assert.Equal(1, added.GetProperty("added").GetInt32());
        var fourth = (await SlotsAsync(id, token)).Single(slot => slot.Callsign == "XEA206");
        Assert.Equal(("R1", 4, "XA;206", First, true), (fourth.RotationCode, fourth.RotationLeg, fourth.FlightNumber, fourth.EventAirportIcao, fourth.IsArrival));

        // A leg that breaks the stored chain is refused on its own row, and the chain stays as it was.
        var broken = string.Join(
            '\n',
            "callsign;flight_number;aircraft_types;departure_icao;off_block_utc;arrival_icao;on_block_utc;stand;rotation;leg",
            $"XEA207;;{TypeA};{Away};{Stamp(day.AddHours(25))};{First};{Stamp(day.AddHours(26))};;R1;5");
        Assert.Equal(["events:errors.chainBroken"], (await RefusedAsync(await LoadAsync(coordinator, id, broken, SlotLoadMode.Add, token), token))["rows[2].departure_icao"]);

        // Replacing the free ones: every public slot there goes, the table's take their place.
        var replaced = await OkAsync(
            await LoadAsync(
                coordinator,
                id,
                Table(Line("XEA301", string.Empty, TypeA, First, day.AddHours(18), Away, day.AddHours(19))),
                SlotLoadMode.ReplaceFree,
                token),
            token);
        Assert.Equal((1, 6), (replaced.GetProperty("added").GetInt32(), replaced.GetProperty("removed").GetInt32()));
        Assert.Equal(["XEA301"], (await SlotsAsync(id, token)).Select(slot => slot.Callsign));

        // The corrected sheet of the same flight, loaded again in its place: the slot that goes and the one that comes have the same
        // callsign and off block, the key of the unique index, in one save (the review of #228, point 3).
        var again = await OkAsync(
            await LoadAsync(
                coordinator,
                id,
                Table(Line("XEA301", "XA301", TypeA, First, day.AddHours(18), Away, day.AddHours(19), "C7")),
                SlotLoadMode.ReplaceFree,
                token),
            token);
        Assert.Equal((1, 1), (again.GetProperty("added").GetInt32(), again.GetProperty("removed").GetInt32()));
        var reloaded = Assert.Single(await SlotsAsync(id, token));
        Assert.Equal(("XEA301", "XA301", "C7"), (reloaded.Callsign, reloaded.FlightNumber, reloaded.Stand));

        // Each slot written and deleted left its row in the audit, by its own area's table.
        await using var scope = _factory.Services.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog;
        Assert.True(await audit.CountAsync(entry => entry.Entity == "evt_slots" && entry.Action == "created" && entry.Vid == CoordinatorVid, token) >= 8);
        Assert.True(await audit.CountAsync(entry => entry.Entity == "evt_slots" && entry.Action == "deleted" && entry.Vid == CoordinatorVid, token) >= 7);
    }

    [Fact]
    public async Task ASlotFallsInsideTheWindowOfItsEventWithSixHoursEachWay()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // An event from 17:00 to 22:00: a slot's time at its airport falls from 11:00 to 04:00 the next day (point 10 on #228).
        var id = await EventWithAirportsAsync(coordinator, "window", token);
        var day = Day(days: 30);

        // A departure three days later, and an arrival seven hours after the end: each refused on its time at the event.
        var refused = await RefusedAsync(
            await LoadAsync(
                coordinator,
                id,
                Table(
                    Line("XEA601", string.Empty, TypeA, First, day.AddDays(3).AddHours(17), Away, day.AddDays(3).AddHours(18)),
                    Line("XEA602", string.Empty, TypeA, Away, day.AddHours(26), First, day.AddHours(29))),
                SlotLoadMode.Add,
                token),
            token);
        Assert.Equal([SlotWindow.OutsideKey], refused["rows[2].off_block_utc"]);
        Assert.Equal([SlotWindow.OutsideKey], refused["rows[3].on_block_utc"]);
        Assert.Equal(2, refused.Count);

        // An arrival landing five hours after the end, after a flight of eleven: in — only the time at the event counts.
        var loaded = await OkAsync(
            await LoadAsync(coordinator, id, Table(Line("XEA603", string.Empty, TypeA, Away, day.AddHours(16), First, day.AddHours(27))), SlotLoadMode.Add, token),
            token);
        Assert.Equal(1, loaded.GetProperty("added").GetInt32());

        // The form the same way: a departure the day before, refused on its off block.
        var form = await RefusedAsync(
            await coordinator.PostAsJsonAsync(EventSlotEndpoints.Pattern, Slot(id, "XEA604", First, day.AddHours(-7), Away, day.AddHours(-6)), token),
            token);
        Assert.Equal([SlotWindow.OutsideKey], form["offBlockUtc"]);
    }

    [Fact]
    public async Task ATableIsRefusedAsAWholeWhenItCannotBeReadOrTheEventHasNoPublicSlots()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var id = await EventWithAirportsAsync(coordinator, "whole", token);

        // No text; a header without its columns; a header and no rows.
        Assert.Contains("errors.required", (await RefusedAsync(await LoadAsync(coordinator, id, string.Empty, SlotLoadMode.Add, token), token))["text"]);
        Assert.Contains(
            "events:errors.sheetColumnMissing",
            (await RefusedAsync(await LoadAsync(coordinator, id, "callsign\tstand\nXEA1\t", SlotLoadMode.Add, token), token))["rows[1].aircraft_types"]);
        Assert.Contains("events:errors.sheetEmpty", (await RefusedAsync(await LoadAsync(coordinator, id, $"{Header}\n\n", SlotLoadMode.Add, token), token))["text"]);

        // An event without public slots takes none: the switch says what it has.
        var none = Id(await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("none", Starts(days: 30)) with { PublicSlots = false }, token));
        await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, Airport(none, First, 1), token);
        var day = Day(days: 30);
        Assert.Contains(
            "events:errors.noPublicSlots",
            (await RefusedAsync(await LoadAsync(coordinator, none, Table(Line("XEA1", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), SlotLoadMode.Add, token), token))["text"]);

        // Nor does an event without airports of its own.
        var bare = Id(await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("bare", Starts(days: 30)), token));
        Assert.Contains(
            EventPublishing.SlotsNeedAirportsKey,
            (await RefusedAsync(await LoadAsync(coordinator, bare, Table(Line("XEA1", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), SlotLoadMode.Add, token), token))["text"]);

        // An event that does not exist.
        using var missing = await LoadAsync(coordinator, long.MaxValue, Table(), SlotLoadMode.Add, token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task TheSlotsAreTheBookingsAreaAndOneSlotsFormIsHeldToTheRulesOfALoad()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var id = await EventWithAirportsAsync(coordinator, "form", token);
        var day = Day(days: 30);

        // The flight operations write the routes, not the slots; a member nothing of either (§6.1, §6.2).
        using var flightOperations = await SignedInAsync(FlightOperationsVid, token);
        using var member = await SignedInAsync(MemberVid, token);
        foreach (var client in new[] { flightOperations, member })
        {
            using (var load = await LoadAsync(client, id, Table(Line("XEA1", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), SlotLoadMode.Add, token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, load.StatusCode);
            }

            using (var list = await client.GetAsync($"{EventSlotEndpoints.Pattern}?filter[eventId]={id}", token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
            }

            using (var create = await client.PostAsJsonAsync(EventSlotEndpoints.Pattern, Slot(id, "XEA1", First, day.AddHours(17), Away, day.AddHours(18)), token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
            }
        }

        // One slot written in its form: in the event's care, its airport and its direction read off its airports, in upper case.
        var arrival = await CreatedAsync(
            coordinator,
            EventSlotEndpoints.Pattern,
            Slot(id, "xea401", Away, day.AddHours(16), "xed2", day.AddHours(17)) with { AircraftTypes = $" {TypeA} / {TypeB} " },
            token);
        Assert.Equal(("XEA401", Second, true), (arrival.GetProperty("callsign").GetString(), arrival.GetProperty("eventAirportIcao").GetString(), arrival.GetProperty("isArrival").GetBoolean()));
        Assert.Equal([TypeA, TypeB], arrival.GetProperty("aircraftTypes").EnumerateArray().Select(type => type.GetString()));
        Assert.Equal(nameof(Department.ED), arrival.GetProperty("ownerDepartment").GetString());

        // The first leg of a new rotation, written without its place, takes the first.
        var first = await CreatedAsync(coordinator, EventSlotEndpoints.Pattern, Slot(id, "XEA402", First, day.AddHours(17), Away, day.AddHours(18)) with { RotationCode = "R5" }, token);
        Assert.Equal(1, first.GetProperty("rotationLeg").GetInt32());

        // What a load refuses, the form refuses: the same flight at the same off block; a flight away from the event; a type the
        // core does not know; a second leg without its place, and one too close to the leg before it.
        var refused = await RefusedAsync(await coordinator.PostAsJsonAsync(EventSlotEndpoints.Pattern, Slot(id, "XEA402", First, day.AddHours(17), Away, day.AddHours(18)), token), token);
        Assert.Equal(["events:errors.slotTwice"], refused["callsign"]);
        refused = await RefusedAsync(await coordinator.PostAsJsonAsync(EventSlotEndpoints.Pattern, Slot(id, "XEA403", Away, day.AddHours(17), Further, day.AddHours(18)) with { AircraftTypes = UnknownType }, token), token);
        Assert.Equal(["events:errors.slotAwayFromEvent"], refused["departureIcao"]);
        Assert.Equal(["events:errors.aircraftUnknown"], refused["aircraftTypes"]);
        refused = await RefusedAsync(await coordinator.PostAsJsonAsync(EventSlotEndpoints.Pattern, Slot(id, "XEA404", Away, day.AddHours(18).AddMinutes(30), First, day.AddHours(19)) with { RotationCode = "R5" }, token), token);
        Assert.Equal(["events:errors.legMissing"], refused["rotationLeg"]);
        refused = await RefusedAsync(await coordinator.PostAsJsonAsync(EventSlotEndpoints.Pattern, Slot(id, "XEA404", Away, day.AddHours(18).AddMinutes(5), First, day.AddHours(19)) with { RotationCode = "R5", RotationLeg = 2 }, token), token);
        Assert.Equal(["events:errors.chainTooClose"], refused["offBlockUtc"]);

        // What a payload says by itself: no callsign, an on block before the off block, a place without a rotation.
        refused = await RefusedAsync(await coordinator.PostAsJsonAsync(EventSlotEndpoints.Pattern, Slot(id, string.Empty, First, day.AddHours(18), Away, day.AddHours(17)) with { RotationLeg = 3 }, token), token);
        Assert.Contains("errors.required", refused["callsign"]);
        Assert.Contains("events:errors.onBlockBeforeOffBlock", refused["onBlockUtc"]);
        Assert.Contains("events:errors.legWithoutRotation", refused["rotationLeg"]);

        // The second leg, right; then moved too close to the first by a correction: refused on its field.
        var second = await CreatedAsync(coordinator, EventSlotEndpoints.Pattern, Slot(id, "XEA404", Away, day.AddHours(18).AddMinutes(30), First, day.AddHours(19)) with { RotationCode = "R5", RotationLeg = 2 }, token);
        refused = await RefusedAsync(
            await coordinator.PutAsJsonAsync(
                $"{EventSlotEndpoints.Pattern}/{Id(second)}",
                Slot(id, "XEA404", Away, day.AddHours(18), First, day.AddHours(19)) with { RotationCode = "R5", RotationLeg = 2, RowVersion = RowVersion(second) },
                token),
            token);
        Assert.Equal(["events:errors.chainTooClose"], refused["offBlockUtc"]);

        // The page of the event lists them, free, by their off block, with the rotation and its places — and to whoever reads the page,
        // nothing of whoever took them.
        var page = await OkAsync(await coordinator.GetAsync($"{PublicEventEndpoints.Pattern}/{Slug("form")}", token), token);
        var listed = page.GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal(["XEA401", "XEA402", "XEA404"], listed.Select(slot => slot.GetProperty("callsign").GetString()));
        Assert.All(listed, slot => Assert.False(slot.GetProperty("taken").GetBoolean()));
        Assert.Equal(("R5", 2), (listed[2].GetProperty("rotation").GetString(), listed[2].GetProperty("leg").GetInt32()));
        Assert.Equal($"evt-test {Away}", listed[0].GetProperty("departure").GetProperty("name").GetString());
        Assert.DoesNotContain(listed[0].EnumerateObject(), property => property.Name.Contains("book", StringComparison.OrdinalIgnoreCase));

        // An airport some slots are at stays, and keeps its code; the public slots stay switched on while there are some.
        var airports = await OkAsync(await coordinator.GetAsync($"{EventAirportEndpoints.Pattern}?filter[eventId]={id}", token), token);
        var withSlots = airports.GetProperty("items").EnumerateArray().Single(airport => airport.GetProperty("icao").GetString() == First);
        Assert.Equal(["events:errors.airportHasSlots"], (await RefusedAsync(await coordinator.DeleteAsync($"{EventAirportEndpoints.Pattern}/{Id(withSlots)}", token), token))["id"]);
        Assert.Equal(
            ["events:errors.airportHasSlots"],
            (await RefusedAsync(
                await coordinator.PutAsJsonAsync($"{EventAirportEndpoints.Pattern}/{Id(withSlots)}", Airport(id, Further, 1) with { RowVersion = RowVersion(withSlots) }, token),
                token))["icao"]);

        var stored = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
        Assert.Equal(
            ["events:errors.hasPublicSlots"],
            (await RefusedAsync(
                await coordinator.PutAsJsonAsync($"{EventEndpoints.Pattern}/{id}", Payload("form", Starts(days: 30)) with { PublicSlots = false, RowVersion = RowVersion(stored) }, token),
                token))["publicSlots"]);

        // A slot deleted alone; then «delete the free ones», which takes every slot nobody booked.
        using (var deleted = await coordinator.DeleteAsync($"{EventSlotEndpoints.Pattern}/{Id(arrival)}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        using (var refusedFree = await flightOperations.PostAsync(new Uri($"{EventEndpoints.Pattern}/{id}/slots/delete-free", UriKind.Relative), null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refusedFree.StatusCode);
        }

        var free = await OkAsync(await coordinator.PostAsync(new Uri($"{EventEndpoints.Pattern}/{id}/slots/delete-free", UriKind.Relative), null, token), token);
        Assert.Equal(2, free.GetProperty("removed").GetInt32());
        Assert.Empty(await SlotsAsync(id, token));

        // A private slot (the generator's, E7) is not corrected as a flight in this form.
        var generated = await PrivateSlotAsync(id, day.AddHours(17), token);
        refused = await RefusedAsync(
            await coordinator.PutAsJsonAsync($"{EventSlotEndpoints.Pattern}/{generated}", Slot(id, "XEA405", First, day.AddHours(17), Away, day.AddHours(18)), token),
            token);
        Assert.Equal(["events:errors.slotNotPublic"], refused["callsign"]);

        // Deleted with its event, each slot with its row in the audit.
        using (var deletedEvent = await coordinator.DeleteAsync($"{EventEndpoints.Pattern}/{id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, deletedEvent.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Slots.AnyAsync(slot => slot.EventId == id, token));
        var generatedId = generated.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(entry => entry.Entity == "evt_slots" && entry.EntityId == generatedId && entry.Action == "deleted", token));
    }

    [Fact]
    public async Task TheGateManagerReadsTheSlotsWithATokenOfItsAudienceAndNeverOfADraft()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var id = await EventWithAirportsAsync(coordinator, "export", token);
        var day = Day(days: 30);

        await OkAsync(
            await LoadAsync(
                coordinator,
                id,
                Table(
                    Line("XEA502", "XA502", TypeA, Away, day.AddHours(18).AddMinutes(40), First, day.AddHours(19).AddMinutes(40), rotation: "R1", leg: "2"),
                    Line("XEA501", "XA501", $"{TypeA}/{TypeB}", First, day.AddHours(17), Away, day.AddHours(18).AddMinutes(10), "B12", rotation: "R1", leg: "1"),
                    Line("XEA503", string.Empty, TypeB, Further, day.AddHours(16), Second, day.AddHours(17))),
                SlotLoadMode.Add,
                token),
            token);
        var generated = await PrivateSlotAsync(id, day.AddHours(20), token);

        // The member's own program, with a token of the gate manager's audience made from their page.
        await TouchLoginAsync(CoordinatorVid, token);
        using var gateManager = await ProgramAsync(await TokenAsync(coordinator, BookingsExport.Audience, token));
        var uri = ExportUri("export");

        // A draft is never exported: 409, with a word the program can act on.
        using (var draft = await gateManager.GetAsync(uri, token))
        {
            Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
            Assert.Equal(BookingsExport.DraftCode, (await draft.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("code").GetString());
        }

        await OkAsync(
            await coordinator.PutAsJsonAsync(
                $"{EventEndpoints.Pattern}/{id}",
                Payload("export", Starts(days: 30)) with { BookingOpensAtUtc = Starts(days: 20), RowVersion = RowVersion(await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token)) },
                token),
            token);
        await OkAsync(await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/publish", new EventPublishRequest(default), token), token);

        // Published: every slot, by its time at the airport of the event, with the gate manager's names and the times in UTC.
        var flights = await ExportedAsync(gateManager, uri, token);
        Assert.Equal(["XEA503", "XEA501", "XEA502", null], flights.Select(flight => flight.GetProperty("callsign").GetString()));

        var leg = flights[1];
        Assert.Equal(
            ["slot_id", "callsign", "flight_number", "booked_by", "aircraft_icao", "gate", "eobt", "eat", "origin_icao", "destination_icao", "rotation", "leg", "paired_slot_id"],
            leg.EnumerateObject().Select(property => property.Name));
        Assert.Equal(("XA501", "B12", First, Away, "R1", 1), (
            leg.GetProperty("flight_number").GetString(),
            leg.GetProperty("gate").GetString(),
            leg.GetProperty("origin_icao").GetString(),
            leg.GetProperty("destination_icao").GetString(),
            leg.GetProperty("rotation").GetString(),
            leg.GetProperty("leg").GetInt32()));
        Assert.Equal(day.AddHours(17).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture), leg.GetProperty("eobt").GetString());
        Assert.EndsWith("Z", leg.GetProperty("eat").GetString(), StringComparison.Ordinal);

        // Nobody booked yet: no one, no aircraft chosen (E6a writes them).
        Assert.All(flights, flight =>
        {
            Assert.Equal(JsonValueKind.Null, flight.GetProperty("booked_by").ValueKind);
            Assert.Equal(JsonValueKind.Null, flight.GetProperty("aircraft_icao").ValueKind);
        });

        // A private slot is its airport and its time: a departure from the event, with no gate yet.
        var privateOne = flights[3];
        Assert.Equal(generated, privateOne.GetProperty("slot_id").GetInt64());
        Assert.Equal((First, JsonValueKind.Null, JsonValueKind.Null), (
            privateOne.GetProperty("origin_icao").GetString(),
            privateOne.GetProperty("destination_icao").ValueKind,
            privateOne.GetProperty("gate").ValueKind));

        // Without a token, or with the cookie of the back office: 401. With a token of another audience: 403.
        using (var anonymous = _factory.CreateApiClient())
        using (var nobody = await anonymous.GetAsync(uri, token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, nobody.StatusCode);
        }

        using (var withCookie = await coordinator.GetAsync(uri, token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, withCookie.StatusCode);
        }

        using var otherProgram = await ProgramAsync(await TokenOfAnotherAudienceAsync(CoordinatorVid, token));
        using (var other = await otherProgram.GetAsync(uri, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        }

        // An address no event has.
        using (var nowhere = await gateManager.GetAsync(ExportUri("nowhere"), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, nowhere.StatusCode);
        }

        // Whoever does not read the bookings — the flight operations — cannot make a token for it.
        using var flightOperations = await SignedInAsync(FlightOperationsVid, token);
        using (var refused = await flightOperations.PostAsJsonAsync(
            PersonalTokenEndpoints.Pattern,
            new { name = "evt-test-e5 gate", audience = BookingsExport.Audience, days = 1 },
            token))
        {
            Assert.Contains("errors.token.audienceNotAllowed", (await RefusedAsync(refused, token))["audience"]);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>An instant some days from today, on the hour: what the database stores is what was sent.</summary>
    private static DateTime Starts(int days) => DateTime.UtcNow.Date.AddDays(days).AddHours(17);

    /// <summary>Midnight of a day some days from today, in UTC: the slots are at hours of it.</summary>
    private static DateTime Day(int days) => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days), DateTimeKind.Utc);

    /// <summary>An RFO with public slots, lasting five hours, called after its address in both languages of the division.</summary>
    private static EventWriteDto Payload(string name, DateTime starts) => new(
        Kind: "rfo",
        PublicSlots: true,
        PrivateSlots: false,
        WholeDivision: false,
        Organizer: EventOrganizer.Division,
        ExternalUrl: null,
        Title: Text(Slug(name)),
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

    private static EventAirportWriteDto Airport(long eventId, string icao, int ordinal) =>
        new(eventId, icao, ordinal, MaxMovementsPerHour: null, MaxArrivalsPerHour: null, MaxDeparturesPerHour: null, RowVersion: default);

    private static EventSlotWriteDto Slot(long eventId, string callsign, string departure, DateTime offBlock, string arrival, DateTime onBlock) =>
        new(eventId, callsign, FlightNumber: null, TypeA, departure, offBlock, arrival, onBlock, Stand: null, RotationCode: null, RotationLeg: null, RowVersion: default);

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>A table as a spreadsheet copies it: the header, then a row per line, separated by tabs.</summary>
    private static string Table(params string[] lines) => string.Join("\r\n", [Header, .. lines]) + "\r\n";

    private static string Line(
        string callsign,
        string flightNumber,
        string types,
        string departure,
        object offBlock,
        string arrival,
        object onBlock,
        string stand = "",
        string rotation = "",
        string leg = "") =>
        string.Join('\t', callsign, flightNumber, types, departure, Cell(offBlock), arrival, Cell(onBlock), stand, rotation, leg);

    private static string Cell(object instant) => instant is DateTime at ? Stamp(at) : (string)instant;

    /// <summary>An instant as the table writes it: in UTC, to the minute.</summary>
    private static string Stamp(DateTime at) => at.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static (string, bool, string?, int?) Shape(EventSlot slot) =>
        (slot.EventAirportIcao, slot.IsArrival, slot.RotationCode, slot.RotationLeg);

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private static DateTime RowVersion(JsonElement row) => row.GetProperty("rowVersion").GetDateTime();

    private static Uri ExportUri(string name) =>
        new(BookingsExport.Pattern.Replace("{slug}", Slug(name), StringComparison.Ordinal), UriKind.Relative);

    private static Task<HttpResponseMessage> LoadAsync(HttpClient client, long id, string text, SlotLoadMode mode, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/slots/load", new SlotLoadRequest(text, mode), cancellationToken);

    /// <summary>An RFO with public slots and its two airports, as the event's page makes it.</summary>
    private static async Task<long> EventWithAirportsAsync(HttpClient client, string name, CancellationToken cancellationToken)
    {
        var id = Id(await CreatedAsync(client, EventEndpoints.Pattern, Payload(name, Starts(days: 30)), cancellationToken));
        await CreatedAsync(client, EventAirportEndpoints.Pattern, Airport(id, First, 1), cancellationToken);
        await CreatedAsync(client, EventAirportEndpoints.Pattern, Airport(id, Second, 2), cancellationToken);
        return id;
    }

    private static async Task<List<JsonElement>> ExportedAsync(HttpClient program, Uri uri, CancellationToken cancellationToken) =>
        [.. (await OkAsync(await program.GetAsync(uri, cancellationToken), cancellationToken)).EnumerateArray()];

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

    /// <summary>The keys of a 400, field by field.</summary>
    private static async Task<Dictionary<string, string[]>> RefusedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

            return JsonDocument.Parse(text).RootElement.GetProperty("errors").EnumerateObject()
                .ToDictionary(field => field.Name, field => field.Value.EnumerateArray().Select(key => key.GetString()!).ToArray());
        }
    }

    /// <summary>A personal token made from the member's own page, as the gate manager's keeper makes it.</summary>
    private static async Task<string> TokenAsync(HttpClient member, string audience, CancellationToken cancellationToken)
    {
        using var created = await member.PostAsJsonAsync(
            PersonalTokenEndpoints.Pattern,
            new { name = "evt-test-e5 gate", audience, days = 1 },
            cancellationToken);
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync(cancellationToken));
        return (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("token").GetString()!;
    }

    /// <summary>A program that sends a token and nothing else: no cookie.</summary>
    private Task<HttpClient> ProgramAsync(string text)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", text);
        return Task.FromResult(client);
    }

    /// <summary>
    /// A live token of the same member for another audience the hub declares — the test module's agent, which the coordinator of the
    /// events could not make from the page: written in the database as the page would write it.
    /// </summary>
    private async Task<string> TokenOfAnotherAudienceAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var text = PersonalTokens.TokenPrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

        database.PersonalTokens.Add(new PersonalToken
        {
            Vid = vid,
            Name = "evt-test-e5 other",
            Audience = SampleModule.AgentAudience,
            TokenHash = PersonalTokens.Hash(text),
            Prefix = text[..11],
            ExpiresAt = clock.UtcNow.AddDays(1),
        });
        await database.SaveChangesAsync(cancellationToken);
        return text;
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

    /// <summary>
    /// A private slot, a departure from the first airport of the event, as the generator of E7 will write it — a job, nobody signed
    /// in —, in the event's care: nothing makes one yet.
    /// </summary>
    private async Task<long> PrivateSlotAsync(long eventId, DateTime at, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var parent = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == eventId, cancellationToken);

        var slot = new EventSlot
        {
            EventId = eventId,
            Kind = SlotKind.Private,
            EventAirportIcao = First,
            IsArrival = false,
            OffBlockUtc = at,
            Generated = true,
            OwnerDepartment = parent.OwnerDepartment,
            OwnerDepartmentMask = parent.OwnerDepartmentMask,
        };

        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);
        return slot.Id;
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>The integration sign-in does not write the last login (CONTRIBUTING.md), and a personal token needs a recent one.</summary>
    private async Task TouchLoginAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var user = await database.Users.SingleAsync(row => row.Vid == vid, cancellationToken);
        user.LastLoginAt = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A member with one position of the staff — or none, a member of the division and nothing else —, and never an address.</summary>
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
        user.LastName = "Events";
        user.Email = null;
        user.IsStaff = position is not null;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null && !await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
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

    /// <summary>The airports and the aircraft types of the class, in the core's snapshots, where a slot's are checked.</summary>
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

        foreach (var code in new[] { TypeA, TypeB })
        {
            if (!await database.IvaoAircraftTypes.AnyAsync(type => type.IcaoCode == code, cancellationToken))
            {
                database.IvaoAircraftTypes.Add(new IvaoAircraftType { IcaoCode = code, Model = "evt-test", Manufacturer = "evt-test", SyncedAt = clock.UtcNow });
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The events of this class, with their rows and what they projected, and the tokens of its VIDs, whatever a stopped run left.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        int[] vids = [CoordinatorVid, FlightOperationsVid, MemberVid];
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().PersonalTokens
            .Where(row => vids.Contains(row.Vid))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
