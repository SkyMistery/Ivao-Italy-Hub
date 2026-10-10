using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Export;
using IvaoHub.Modules.Events.Settings;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Quartz;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The private slots (M4, E7), through the real host with the division file of this repository: the staff generate them from the
/// capacity of the airports, less what the other flights already hold, and generating again replaces the free ones and keeps the
/// booked ones; a pilot books one with the flight they fly, checked like any other booking of theirs, and an arrival with its linked
/// departure — both or neither, also when the database answers in the same instant; the export carries the private flights with the
/// slot each is paired with; one of a pair going leaves the other, unlinked; the reminder goes by the off block of the flight.
/// <para>⚠️ The coordinator is seeded **without an address**, as in <see cref="EventsStaffTests"/>: the tests of the contacts assert who
/// of the events receives a message. The two pilots have one each: they are told by mail. The job of the reminders is paused in the
/// host's scheduler, so the only runs are the test's.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsPrivateSlotsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // E7's in the range the events module owns in the shared database (CONTRIBUTING.md): 761087, 761088, 761095 and 761096, which the
    // merged phases left unused (no code of main or of an open branch names them on 10 October 2026).
    private const int CoordinatorVid = 761087;
    private const int PilotVid = 761088;
    private const int OtherPilotVid = 761095;
    private const int MemberVid = 761096;

    /// <summary>Two airports of the events of this class, and one away from them: of no country the network has.</summary>
    private const string First = "XEI1";
    private const string Second = "XEI2";
    private const string Away = "XEI3";

    /// <summary>Two aircraft types the core knows here.</summary>
    private const string TypeA = "XE8A";
    private const string TypeB = "XE8B";

    private const string SlugStem = "evt-test-e7";

    private const string Header = "callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly string[] Airports = [First, Second, Away];

    private static readonly int[] People = [CoordinatorVid, PilotVid, OtherPilotVid, MemberVid];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        // The host runs the reminders every quarter of an hour by itself: paused, the only runs are the test's.
        var scheduler = await _factory.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(token);
        await scheduler.PauseJob(new JobKey(BookingRemindersJob.JobName), token);

        await SeedUserAsync(CoordinatorVid, "IT-EC", email: null, token);
        await SeedUserAsync(PilotVid, position: null, "e7-pilot@example.org", token);
        await SeedUserAsync(OtherPilotVid, position: null, "e7-other@example.org", token);
        await SeedUserAsync(MemberVid, position: null, email: null, token);
        await SeedReferenceAsync(token);
        await ForgetAsync(token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows, its mails and tokens, its airports and types, its positions.</summary>
    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.Users.Where(user => People.Contains(user.Vid))
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.Email, (string?)null));
            await database.IvaoAirports.Where(airport => Airports.Contains(airport.Icao)).ExecuteDeleteAsync();
            await database.IvaoAircraftTypes.Where(type => type.IcaoCode == TypeA || type.IcaoCode == TypeB).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TheStaffGenerateThePrivateSlotsFromTheCapacityTheOtherFlightsLeave()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);

        // Two hours; the first airport takes four arrivals and two departures an hour, the second six movements.
        var id = await EventAsync(
            coordinator,
            "generate",
            publicSlots: true,
            day.AddHours(17),
            day.AddHours(19),
            token,
            Capacity(First, arrivals: 4, departures: 2),
            Capacity(Second, movements: 6));

        // An arrival at the first airport at 17:07, a departure from it at 17:20, and a flight from the first to the second, which
        // leaves one at 18:00 and lands at the other at 18:40.
        await OkAsync(
            await LoadAsync(
                coordinator,
                id,
                Table(
                    Line("XEI101", TypeA, Away, day.AddHours(16), First, day.AddHours(17).AddMinutes(7)),
                    Line("XEI102", TypeA, First, day.AddHours(17).AddMinutes(20), Away, day.AddHours(18).AddMinutes(20)),
                    Line("XEI103", TypeA, First, day.AddHours(18), Second, day.AddHours(18).AddMinutes(40))),
                token),
            token);

        var generated = await OkAsync(await GenerateAsync(coordinator, id, token), token);
        Assert.Equal((20, 0, 0), (generated.GetProperty("generated").GetInt32(), generated.GetProperty("removed").GetInt32(), generated.GetProperty("kept").GetInt32()));

        var slots = await PrivateSlotsAsync(id, token);

        // The first airport: four arrivals an hour less the one at 17:07, which takes 17:00; two departures an hour less the one at
        // 17:20, which takes 17:30, and the one at 18:00.
        Assert.Equal(
            [Clock(17, 15), Clock(17, 30), Clock(17, 45), Clock(18, 0), Clock(18, 15), Clock(18, 30), Clock(18, 45)],
            Times(slots, First, arrivals: true));
        Assert.Equal([Clock(17, 0), Clock(18, 30)], Times(slots, First, arrivals: false));

        // The second, in movements: six steps an hour, the directions alternating from an arrival; in the second hour the flight
        // from the first airport takes 18:40, and the directions are evened out around it.
        Assert.Equal([Clock(17, 0), Clock(17, 20), Clock(17, 40), Clock(18, 10), Clock(18, 30)], Times(slots, Second, arrivals: true));
        Assert.Equal(
            [Clock(17, 10), Clock(17, 30), Clock(17, 50), Clock(18, 0), Clock(18, 20), Clock(18, 50)],
            Times(slots, Second, arrivals: false));

        // Generated, private, in the event's care, as every row of its staff.
        var parent = await EventRowAsync(id, token);
        Assert.All(slots, slot =>
        {
            Assert.True(slot is { Kind: SlotKind.Private, Generated: true, Callsign: null });
            Assert.Equal((parent.OwnerDepartment, parent.OwnerDepartmentMask), (slot.OwnerDepartment, slot.OwnerDepartmentMask));
        });

        // The page offers them by airport, direction and time, free — the staff read it before it is published.
        var page = await OkAsync(await coordinator.GetAsync($"/api/events/public/{Slug("generate")}", token), token);
        var offered = page.GetProperty("privateSlots").EnumerateArray().ToList();
        Assert.Equal(20, offered.Count);
        Assert.All(offered, slot => Assert.False(slot.GetProperty("taken").GetBoolean()));
        Assert.Contains(offered, slot =>
            slot.GetProperty("airportIcao").GetString() == First
            && slot.GetProperty("isArrival").GetBoolean()
            && slot.GetProperty("timeUtc").GetDateTime() == day.AddHours(17).AddMinutes(15));
        Assert.Equal(3, page.GetProperty("slots").GetArrayLength());

        // A member does not generate them, a visitor neither.
        using var member = await SignedInAsync(MemberVid, token);
        using (var refused = await GenerateAsync(member, id, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using (var anonymous = _factory.CreateApiClient())
        {
            // As the browser sends it: without the header, the guard of the session's writes answers first.
            anonymous.DefaultRequestHeaders.Add("X-Requested-With", "hub");
            using var nobody = await GenerateAsync(anonymous, id, token);
            Assert.Equal(HttpStatusCode.Unauthorized, nobody.StatusCode);
        }

        // An event without private slots has none to generate; one whose airports say no capacity has nothing to generate them from.
        var onlyPublic = await EventAsync(coordinator, "public", publicSlots: true, day.AddHours(17), day.AddHours(19), token, Capacity(First, arrivals: 4, departures: 2), privateSlots: false);
        Assert.Equal(["events:errors.noPrivateSlots"], (await RefusedAsync(await GenerateAsync(coordinator, onlyPublic, token), token))[PrivateSlotGeneration.Field]);

        var noCapacity = await EventAsync(coordinator, "nocapacity", publicSlots: false, day.AddHours(17), day.AddHours(19), token, Capacity(First));
        Assert.Equal(["events:errors.noCapacity"], (await RefusedAsync(await GenerateAsync(coordinator, noCapacity, token), token))[PrivateSlotGeneration.Field]);
        Assert.Empty(await PrivateSlotsAsync(noCapacity, token));
    }

    [Fact]
    public async Task GeneratingAgainReplacesTheFreeOnesAndKeepsTheBookedOnesInTheirHour()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);

        // Private slots and no public ones, as a division presets an MSE: the whole capacity is private.
        var id = await EventAsync(coordinator, "again", publicSlots: false, day.AddHours(17), day.AddHours(18), token, Capacity(First, arrivals: 4));
        await OkAsync(await GenerateAsync(coordinator, id, token), token);
        Assert.Equal([Clock(17, 0), Clock(17, 15), Clock(17, 30), Clock(17, 45)], Times(await PrivateSlotsAsync(id, token), First, arrivals: true));
        await PublishAsync(coordinator, id, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        var booked = (await PrivateSlotsAsync(id, token)).Single(slot => slot.OnBlockUtc == day.AddHours(17).AddMinutes(15));
        await CreatedAsync(await BookPrivateAsync(pilot, Arrival(booked.Id, "XEI201", Away, day.AddHours(16)), token), token);

        // Generated again: the three free ones replaced, the booked one kept with its row and its booking.
        var again = await OkAsync(await GenerateAsync(coordinator, id, token), token);
        Assert.Equal((3, 3, 1), (again.GetProperty("generated").GetInt32(), again.GetProperty("removed").GetInt32(), again.GetProperty("kept").GetInt32()));
        var slots = await PrivateSlotsAsync(id, token);
        Assert.Contains(slots, slot => slot.Id == booked.Id);
        Assert.Equal([Clock(17, 0), Clock(17, 15), Clock(17, 30), Clock(17, 45)], Times(slots, First, arrivals: true));

        // Two an hour now: the booked one at 17:15 takes the step at 17:00, as near as the one at 17:30, and one free slot is left.
        await SetCapacityAsync(coordinator, id, First, arrivals: 2, token);
        var halved = await OkAsync(await GenerateAsync(coordinator, id, token), token);
        Assert.Equal((1, 3, 1), (halved.GetProperty("generated").GetInt32(), halved.GetProperty("removed").GetInt32(), halved.GetProperty("kept").GetInt32()));
        Assert.Equal([Clock(17, 15), Clock(17, 30)], Times(await PrivateSlotsAsync(id, token), First, arrivals: true));
        Assert.Single(await BookingsAsync(id, token));
    }

    [Fact]
    public async Task APrivateSlotIsBookedWithTheFlightItsPilotWritesAndCheckedLikeAnyOtherBooking()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);

        var id = await EventAsync(coordinator, "flight", publicSlots: true, day.AddHours(17), day.AddHours(21), token, Capacity(First, arrivals: 2, departures: 2), Capacity(Second));
        await OkAsync(await LoadAsync(coordinator, id, Table(Line("XEI301", TypeA, First, day.AddHours(17), Away, day.AddHours(18).AddMinutes(10))), token), token);
        await OkAsync(await GenerateAsync(coordinator, id, token), token);
        await PublishAsync(coordinator, id, token);

        var slots = await PrivateSlotsAsync(id, token);
        var arrival = slots.Single(slot => slot.IsArrival && slot.OnBlockUtc == day.AddHours(19));
        var later = slots.Single(slot => slot.IsArrival && slot.OnBlockUtc == day.AddHours(20));
        var departure = slots.Single(slot => !slot.IsArrival && slot.OffBlockUtc == day.AddHours(20));
        var publicSlot = (await SlotIdsAsync(id, token))["XEI301"];

        // The pilot flies the public flight to 18:10; a private arrival leaving the other airport at 18:15 is five minutes too close.
        using var pilot = await SignedInAsync(PilotVid, token);
        await CreatedAsync(await pilot.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(publicSlot, TypeA), token), token);
        Assert.Equal(
            ["events:errors.bookingIncompatible"],
            (await RefusedAsync(await BookPrivateAsync(pilot, Arrival(arrival.Id, "XEI302", Away, day.AddHours(18).AddMinutes(15)), token), token))["slotId"]);

        // Leaving at 18:20, ten minutes after the public flight lands: booked, with the flight the pilot wrote.
        var made = await CreatedAsync(await BookPrivateAsync(pilot, Arrival(arrival.Id, " xei302 ", " xei3 ", day.AddHours(18).AddMinutes(20)), token), token);
        var booking = made.GetProperty("booking");
        Assert.Equal(JsonValueKind.Null, made.GetProperty("departure").ValueKind);
        Assert.Equal(
            ("XEI302", Away, First, nameof(SlotKind.Private), TypeA),
            (booking.GetProperty("callsign").GetString(), booking.GetProperty("departureIcao").GetString(), booking.GetProperty("arrivalIcao").GetString(), booking.GetProperty("kind").GetString(), booking.GetProperty("aircraftIcao").GetString()));
        Assert.Equal(day.AddHours(18).AddMinutes(20), booking.GetProperty("offBlockUtc").GetDateTime());
        Assert.Equal(day.AddHours(19), booking.GetProperty("onBlockUtc").GetDateTime());
        Assert.True(booking.GetProperty("withdrawable").GetBoolean());

        var stored = (await BookingsAsync(id, token)).Single(row => row.SlotId == arrival.Id);
        Assert.Equal(("XEI302", Away, day.AddHours(18).AddMinutes(20), PilotVid), (stored.Callsign, stored.OtherIcao, stored.OtherTimeUtc, stored.BookerVid));

        // Its own list says the flight the same way, by its off block, after the public one.
        var mine = (await OkAsync(await pilot.GetAsync(BookingEndpoints.MinePattern, token), token)).EnumerateArray()
            .Where(row => row.GetProperty("eventId").GetInt64() == id)
            .Select(row => row.GetProperty("callsign").GetString())
            .ToList();
        Assert.Equal(["XEI301", "XEI302"], mine);

        // What a flight through a private slot may not be, each on its field.
        using var other = await SignedInAsync(OtherPilotVid, token);
        Assert.Equal(["events:errors.otherIsTheSlots"], (await RefusedAsync(await BookPrivateAsync(other, Arrival(later.Id, "XEI303", First, day.AddHours(19)), token), token))["otherIcao"]);
        Assert.Equal(["events:errors.airportUnknown"], (await RefusedAsync(await BookPrivateAsync(other, Arrival(later.Id, "XEI303", "XEI9", day.AddHours(19)), token), token))["otherIcao"]);
        Assert.Equal(["events:errors.aircraftUnknown"], (await RefusedAsync(await BookPrivateAsync(other, Arrival(later.Id, "XEI303", Away, day.AddHours(19)) with { AircraftIcao = "XE8Z" }, token), token))["aircraftIcao"]);
        Assert.Equal(["events:errors.onBlockBeforeOffBlock"], (await RefusedAsync(await BookPrivateAsync(other, Arrival(later.Id, "XEI303", Away, day.AddHours(20).AddMinutes(5)), token), token))["otherTimeUtc"]);
        Assert.Equal(["events:errors.offBlockPassed"], (await RefusedAsync(await BookPrivateAsync(other, Arrival(later.Id, "XEI303", Away, DateTime.UtcNow.AddHours(-1)), token), token))["otherTimeUtc"]);
        Assert.Equal(["events:errors.onBlockBeforeOffBlock"], (await RefusedAsync(await BookPrivateAsync(other, Arrival(departure.Id, "XEI303", Away, day.AddHours(19)), token), token))["otherTimeUtc"]);
        Assert.Equal(["errors.required"], (await RefusedAsync(await BookPrivateAsync(other, Arrival(later.Id, " ", Away, day.AddHours(19)), token), token))["callsign"]);

        // A public slot is booked with «Book», a private one with its flight; only an arrival brings a departure.
        Assert.Equal(["events:errors.slotNotPrivate"], (await RefusedAsync(await BookPrivateAsync(other, Arrival(publicSlot, "XEI303", Away, day.AddHours(19)), token), token))["slotId"]);
        Assert.Equal(["events:errors.bookingPrivateSlot"], (await RefusedAsync(await other.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(later.Id, TypeA), token), token))["slotId"]);
        Assert.Equal(
            ["events:errors.pairedOnlyForArrival"],
            (await RefusedAsync(await BookPrivateAsync(other, Arrival(departure.Id, "XEI303", Away, day.AddHours(21)) with { Departure = Linked(later.Id, "XEI304", Away, day.AddHours(22)) }, token), token))["departure.slotId"]);

        // The slot booked is the pilot's: taken for everybody else.
        Assert.Equal(["events:errors.slotJustTaken"], (await RefusedAsync(await BookPrivateAsync(other, Arrival(arrival.Id, "XEI303", Away, day.AddHours(18)), token), token))["slotId"]);
        Assert.Single(await BookingsAsync(id, token), row => row.SlotId == arrival.Id);
    }

    [Fact]
    public async Task AnArrivalAndItsLinkedDepartureAreBornTogetherOrNotAtAll()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);

        // Two arrivals and two departures an hour at the first airport, from 17:00 to 20:00; one an hour at the second.
        var id = await EventAsync(coordinator, "pair", publicSlots: false, day.AddHours(17), day.AddHours(20), token, Capacity(First, arrivals: 2, departures: 2), Capacity(Second, arrivals: 1, departures: 1));
        await OkAsync(await GenerateAsync(coordinator, id, token), token);
        await PublishAsync(coordinator, id, token);
        var slots = await PrivateSlotsAsync(id, token);
        long At(string airport, bool arrival, int hour, int minute = 0) =>
            slots.Single(slot => slot.EventAirportIcao == airport && slot.IsArrival == arrival && (arrival ? slot.OnBlockUtc : slot.OffBlockUtc) == day.AddHours(hour).AddMinutes(minute)).Id;

        // Landing at 17:30, leaving again at 18:30 from the same airport: born together, each naming the other.
        using var pilot = await SignedInAsync(PilotVid, token);
        var made = await CreatedAsync(
            await BookPrivateAsync(
                pilot,
                Arrival(At(First, true, 17, 30), "XEI401", Away, day.AddHours(16).AddMinutes(20)) with { Departure = Linked(At(First, false, 18, 30), "XEI402", Away, day.AddHours(19).AddMinutes(40)) },
                token),
            token);
        var arrival = made.GetProperty("booking");
        var departure = made.GetProperty("departure");
        Assert.Equal(departure.GetProperty("id").GetInt64(), arrival.GetProperty("pairedBookingId").GetInt64());
        Assert.Equal(arrival.GetProperty("id").GetInt64(), departure.GetProperty("pairedBookingId").GetInt64());
        Assert.Equal(("XEI402", First, Away), (departure.GetProperty("callsign").GetString(), departure.GetProperty("departureIcao").GetString(), departure.GetProperty("arrivalIcao").GetString()));
        Assert.Equal(TypeA, departure.GetProperty("aircraftIcao").GetString());

        var stored = await BookingsAsync(id, token);
        Assert.Equal(stored.Single(row => row.Callsign == "XEI402").Id, stored.Single(row => row.Callsign == "XEI401").PairedBookingId);
        Assert.Null(stored.Single(row => row.Callsign == "XEI402").PairedBookingId);

        // Its own list says the pair both ways.
        var mine = (await OkAsync(await pilot.GetAsync(BookingEndpoints.MinePattern, token), token)).EnumerateArray()
            .Where(row => row.GetProperty("eventId").GetInt64() == id)
            .ToDictionary(row => row.GetProperty("callsign").GetString()!, row => row.GetProperty("pairedBookingId").GetInt64());
        Assert.Equal(departure.GetProperty("id").GetInt64(), mine["XEI401"]);
        Assert.Equal(arrival.GetProperty("id").GetInt64(), mine["XEI402"]);

        // Another pilot asks for a departure already taken with a free arrival: neither is booked.
        using var other = await SignedInAsync(OtherPilotVid, token);
        Assert.Equal(
            ["events:errors.slotJustTaken"],
            (await RefusedAsync(await BookPrivateAsync(other, Arrival(At(First, true, 18), "XEI411", Away, day.AddHours(17)) with { Departure = Linked(At(First, false, 18, 30), "XEI412", Away, day.AddHours(20)) }, token), token))["departure.slotId"]);

        // A departure before the arrival has landed and the gap gone by, or one from another airport, or an arrival as a departure.
        Assert.Equal(
            ["events:errors.pairedTooSoon"],
            (await RefusedAsync(await BookPrivateAsync(other, Arrival(At(First, true, 18), "XEI411", Away, day.AddHours(17)) with { Departure = Linked(At(First, false, 18), "XEI412", Away, day.AddHours(19)) }, token), token))["departure.slotId"]);
        Assert.Equal(
            ["events:errors.pairedNotADeparture"],
            (await RefusedAsync(await BookPrivateAsync(other, Arrival(At(First, true, 18), "XEI411", Away, day.AddHours(17)) with { Departure = Linked(At(Second, false, 19), "XEI412", Away, day.AddHours(20)) }, token), token))["departure.slotId"]);
        Assert.Equal(
            ["events:errors.pairedNotADeparture"],
            (await RefusedAsync(await BookPrivateAsync(other, Arrival(At(First, true, 18), "XEI411", Away, day.AddHours(17)) with { Departure = Linked(At(First, true, 19), "XEI412", Away, day.AddHours(20)) }, token), token))["departure.slotId"]);
        Assert.Equal(
            ["errors.required"],
            (await RefusedAsync(await BookPrivateAsync(other, Arrival(At(First, true, 18), "XEI411", Away, day.AddHours(17)) with { Departure = Linked(At(First, false, 19), string.Empty, Away, day.AddHours(20)) }, token), token))["departure.callsign"]);
        Assert.DoesNotContain(await BookingsAsync(id, token), row => row.BookerVid == OtherPilotVid);

        // In the same instant: a transaction of the test holds a booking of the arrival, not committed yet. The request sees the arrival
        // free, books the departure, and waits for the arrival in the database; once the test commits, the arrival is another pilot's,
        // and the departure booked a moment before goes back with it.
        var held = At(First, true, 19);
        var linked = At(First, false, 19, 30);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            var parent = await EventRowAsync(id, token);
            database.Bookings.Add(new EventBooking
            {
                EventId = id,
                SlotId = held,
                BookerVid = MemberVid,
                AircraftIcao = TypeB,
                Callsign = "XEI499",
                OtherIcao = Away,
                OtherTimeUtc = day.AddHours(18),
                CreatedAt = DateTime.UtcNow,
                OwnerDepartment = parent.OwnerDepartment,
                OwnerDepartmentMask = parent.OwnerDepartmentMask,
            });
            await database.SaveChangesAsync(token);

            var request = BookPrivateAsync(other, Arrival(held, "XEI421", Away, day.AddHours(18).AddMinutes(10)) with { Departure = Linked(linked, "XEI422", Away, day.AddHours(20).AddMinutes(30)) }, token);
            await WaitForALockWaitAsync(token);
            await transaction.CommitAsync(token);

            Assert.Equal(["events:errors.slotJustTaken"], (await RefusedAsync(await request, token))["slotId"]);
        }

        var after = await BookingsAsync(id, token);
        Assert.Equal(MemberVid, after.Single(row => row.SlotId == held).BookerVid);
        Assert.DoesNotContain(after, row => row.SlotId == linked);
        Assert.DoesNotContain(after, row => row.BookerVid == OtherPilotVid);
    }

    [Fact]
    public async Task TheExportCarriesThePrivateFlightsAndTheSlotEachIsPairedWith()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);

        var id = await EventAsync(coordinator, "export", publicSlots: false, day.AddHours(17), day.AddHours(19), token, Capacity(First, arrivals: 1, departures: 1));
        await OkAsync(await GenerateAsync(coordinator, id, token), token);
        await PublishAsync(coordinator, id, token);
        var slots = await PrivateSlotsAsync(id, token);
        var arrival = slots.Single(slot => slot.IsArrival && slot.OnBlockUtc == day.AddHours(17));
        var departure = slots.Single(slot => !slot.IsArrival && slot.OffBlockUtc == day.AddHours(18));
        var free = slots.Single(slot => slot.IsArrival && slot.OnBlockUtc == day.AddHours(18));

        using var pilot = await SignedInAsync(PilotVid, token);
        await CreatedAsync(
            await BookPrivateAsync(
                pilot,
                Arrival(arrival.Id, "XEI501", Away, day.AddHours(15).AddMinutes(45)) with { Departure = Linked(departure.Id, "XEI502", Second, day.AddHours(19).AddMinutes(5)) },
                token),
            token);

        await TouchLoginAsync(CoordinatorVid, token);
        using var gateManager = Program(await TokenAsync(coordinator, token));
        gateManager.DefaultRequestHeaders.Add(BookingsExport.Contract.Header, "1");
        var flights = (await OkAsync(await gateManager.GetAsync(new Uri(BookingsExport.Pattern.Replace("{slug}", Slug("export"), StringComparison.Ordinal), UriKind.Relative), token), token))
            .EnumerateArray()
            .ToDictionary(flight => flight.GetProperty("slot_id").GetInt64());

        // The arrival: the flight its pilot wrote, by the time at the airport of the event; no gate yet, the departure for the same one.
        var landing = flights[arrival.Id];
        Assert.Equal(
            ("XEI501", Away, First, TypeA, PilotVid, departure.Id),
            (landing.GetProperty("callsign").GetString(), landing.GetProperty("origin_icao").GetString(), landing.GetProperty("destination_icao").GetString(),
                landing.GetProperty("aircraft_icao").GetString(), landing.GetProperty("booked_by").GetInt32(), landing.GetProperty("paired_slot_id").GetInt64()));
        Assert.Equal(Stamp(day.AddHours(15).AddMinutes(45)), landing.GetProperty("eobt").GetString());
        Assert.Equal(Stamp(day.AddHours(17)), landing.GetProperty("eat").GetString());
        Assert.Equal(JsonValueKind.Null, landing.GetProperty("gate").ValueKind);
        Assert.Empty(landing.GetProperty("aircraft_types").EnumerateArray());

        // The departure, the other way: to the airport its pilot wrote, paired with the arrival.
        var leaving = flights[departure.Id];
        Assert.Equal(
            ("XEI502", First, Second, arrival.Id),
            (leaving.GetProperty("callsign").GetString(), leaving.GetProperty("origin_icao").GetString(), leaving.GetProperty("destination_icao").GetString(), leaving.GetProperty("paired_slot_id").GetInt64()));
        Assert.Equal(Stamp(day.AddHours(18)), leaving.GetProperty("eobt").GetString());
        Assert.Equal(Stamp(day.AddHours(19).AddMinutes(5)), leaving.GetProperty("eat").GetString());

        // A free private slot: its airport and its time, nobody's, paired with nothing.
        var open = flights[free.Id];
        Assert.Equal(
            (JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null),
            (open.GetProperty("callsign").ValueKind, open.GetProperty("booked_by").ValueKind, open.GetProperty("origin_icao").ValueKind, open.GetProperty("paired_slot_id").ValueKind));
        Assert.Equal(First, open.GetProperty("destination_icao").GetString());
        Assert.Equal(Stamp(day.AddHours(18)), open.GetProperty("eat").GetString());
    }

    [Fact]
    public async Task OneOfAPairGoingDissolvesTheLinkAndTheOtherStays()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);

        var id = await EventAsync(coordinator, "dissolve", publicSlots: false, day.AddHours(17), day.AddHours(21), token, Capacity(First, arrivals: 1, departures: 1));
        await OkAsync(await GenerateAsync(coordinator, id, token), token);
        await PublishAsync(coordinator, id, token);
        var slots = await PrivateSlotsAsync(id, token);
        long At(bool arrival, int hour) => slots.Single(slot => slot.IsArrival == arrival && (arrival ? slot.OnBlockUtc : slot.OffBlockUtc) == day.AddHours(hour)).Id;

        // The pilot withdraws the departure of a pair: the arrival stays, linked to nothing.
        using var pilot = await SignedInAsync(PilotVid, token);
        var first = await CreatedAsync(
            await BookPrivateAsync(pilot, Arrival(At(true, 17), "XEI601", Away, day.AddHours(16)) with { Departure = Linked(At(false, 18), "XEI602", Away, day.AddHours(19)) }, token),
            token);
        using (var withdrawn = await pilot.DeleteAsync($"{BookingEndpoints.MinePattern}/{first.GetProperty("departure").GetProperty("id").GetInt64()}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        }

        var kept = Assert.Single(await BookingsAsync(id, token));
        Assert.Equal(("XEI601", (long?)null), (kept.Callsign, kept.PairedBookingId));

        // The staff take the arrival of another pair away: its departure stays, and the pilot is told of the arrival alone.
        var second = await CreatedAsync(
            await BookPrivateAsync(pilot, Arrival(At(true, 19), "XEI603", Away, day.AddHours(18).AddMinutes(10)) with { Departure = Linked(At(false, 20), "XEI604", Away, day.AddHours(21)) }, token),
            token);
        using (var taken = await coordinator.PostAsJsonAsync(
            $"{BookingEndpoints.StaffPattern}/{second.GetProperty("booking").GetProperty("id").GetInt64()}/remove",
            new BookingRemovalRequest("evt-test-e7 the gate is closed"),
            token))
        {
            Assert.Equal(HttpStatusCode.NoContent, taken.StatusCode);
        }

        var left = (await BookingsAsync(id, token)).Select(row => row.Callsign).Order(StringComparer.Ordinal);
        Assert.Equal(["XEI601", "XEI604"], left);

        var mine = (await OkAsync(await pilot.GetAsync(BookingEndpoints.MinePattern, token), token)).EnumerateArray()
            .Where(row => row.GetProperty("eventId").GetInt64() == id)
            .ToList();
        Assert.All(mine, row => Assert.Equal(JsonValueKind.Null, row.GetProperty("pairedBookingId").ValueKind));

        await using var scope = _factory.Services.CreateAsyncScope();
        var told = Assert.Single(await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == EventsNotifications.BookingRemoved && row.Vid == PilotVid && row.DataJson.Contains(SlugStem))
            .ToListAsync(token));
        var data = JsonDocument.Parse(told.DataJson).RootElement;
        Assert.Equal(("XEI603", Away, First), (data.GetProperty("callsign").GetString(), data.GetProperty("departure").GetString(), data.GetProperty("arrival").GetString()));
    }

    [Fact]
    public async Task APrivateFlightIsRemindedByTheOffBlockItsPilotWrote()
    {
        var token = TestContext.Current.CancellationToken;
        var lead = await LeadAsync(token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // An event of two hours that starts three hours before the end of the reminder's window, one arrival an hour.
        var starts = Minute(DateTime.UtcNow) + lead - TimeSpan.FromHours(3);
        var id = await EventAsync(coordinator, "remind", publicSlots: false, starts, starts.AddHours(2), token, Capacity(First, arrivals: 1), opens: DateTime.UtcNow.AddDays(-1));
        await OkAsync(await GenerateAsync(coordinator, id, token), token);
        await PublishAsync(coordinator, id, token);

        // The arrival of the second hour, leaving the other airport fifty minutes before it lands: inside the window by that time.
        using var pilot = await SignedInAsync(PilotVid, token);
        var slot = (await PrivateSlotsAsync(id, token)).Single(row => row.OnBlockUtc == starts.AddHours(1));
        await CreatedAsync(await BookPrivateAsync(pilot, Arrival(slot.Id, "XEI701", Away, starts.AddMinutes(10)), token), token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<BookingRemindersJob>().RunAsync(token);
        }

        await using var read = _factory.Services.CreateAsyncScope();
        var told = Assert.Single(await read.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == EventsNotifications.BookingReminder && row.Vid == PilotVid && row.DataJson.Contains(SlugStem))
            .ToListAsync(token));
        var flights = JsonDocument.Parse(told.DataJson).RootElement.GetProperty("flights").GetString()!;
        Assert.Contains($"XEI701, {TypeA}: {Away} {EventsMail.Moment(starts.AddMinutes(10))} → {First} {EventsMail.Moment(starts.AddHours(1))}", flights, StringComparison.Ordinal);
        Assert.NotNull(Assert.Single(await BookingsAsync(id, token)).RemindedAt);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>Midnight of a day some days from today, in UTC: the slots are at hours of it.</summary>
    private static DateTime Day(int days) => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days), DateTimeKind.Utc);

    /// <summary>An instant to the minute.</summary>
    private static DateTime Minute(DateTime at) => new(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, DateTimeKind.Utc);

    /// <summary>A time of day, for the lists of slots of one day.</summary>
    private static TimeOnly Clock(int hour, int minute) => new(hour, minute);

    /// <summary>An instant as the export writes it: ISO 8601 in UTC, with the <c>Z</c>.</summary>
    private static string Stamp(DateTime at) => at.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>An airport of an event with its capacity: movements, or arrivals and departures, or none.</summary>
    private static (string Icao, int? Movements, int? Arrivals, int? Departures) Capacity(string icao, int? movements = null, int? arrivals = null, int? departures = null) =>
        (icao, movements, arrivals, departures);

    private static EventWriteDto Payload(string slug, DateTime starts, DateTime ends, bool publicSlots, bool privateSlots, DateTime? opens = null) => new(
        Kind: "rfo",
        PublicSlots: publicSlots,
        PrivateSlots: privateSlots,
        WholeDivision: false,
        Organizer: EventOrganizer.Division,
        ExternalUrl: null,
        Title: Text(slug),
        Slug: slug,
        Summary: Text(slug),
        Body: null,
        BannerMediaId: null,
        VisibleFromUtc: null,
        BookingOpensAtUtc: opens ?? DateTime.UtcNow.AddDays(-1),
        StartsAtUtc: starts,
        EndsAtUtc: ends,
        Visibility: Visibility.Public,
        RowVersion: default);

    /// <summary>A private arrival with its flight: the callsign, where it leaves from and when.</summary>
    private static PrivateBookingRequest Arrival(long slotId, string callsign, string other, DateTime otherTime) =>
        new(slotId, TypeA, callsign, other, otherTime, Departure: null);

    private static PrivateDepartureRequest Linked(long slotId, string callsign, string other, DateTime otherTime) =>
        new(slotId, callsign, other, otherTime);

    /// <summary>A table as a spreadsheet copies it: the header, then a row per line, separated by tabs.</summary>
    private static string Table(params string[] lines) => string.Join("\r\n", [Header, .. lines]) + "\r\n";

    private static string Line(string callsign, string types, string departure, DateTime offBlock, string arrival, DateTime onBlock) =>
        string.Join('\t', callsign, string.Empty, types, departure, Moment(offBlock), arrival, Moment(onBlock), string.Empty, string.Empty, string.Empty);

    private static string Moment(DateTime at) => at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The private slots at one airport, of one direction, by their time there, as times of the day.</summary>
    private static List<TimeOnly> Times(IEnumerable<EventSlot> slots, string airport, bool arrivals) =>
    [
        .. slots
            .Where(slot => slot.EventAirportIcao == airport && slot.IsArrival == arrivals)
            .Select(slot => (arrivals ? slot.OnBlockUtc : slot.OffBlockUtc)!.Value)
            .Order()
            .Select(TimeOnly.FromDateTime),
    ];

    /// <summary>
    /// An event of the class with its airports and their capacity, by default with private slots and its bookings open since
    /// yesterday, as the staff write it from its page: a draft.
    /// </summary>
    private static async Task<long> EventAsync(
        HttpClient coordinator,
        string name,
        bool publicSlots,
        DateTime starts,
        DateTime ends,
        CancellationToken cancellationToken,
        (string Icao, int? Movements, int? Arrivals, int? Departures) airport,
        (string Icao, int? Movements, int? Arrivals, int? Departures)? another = null,
        bool privateSlots = true,
        DateTime? opens = null)
    {
        var id = (await CreatedJsonAsync(coordinator, EventEndpoints.Pattern, Payload(Slug(name), starts, ends, publicSlots, privateSlots, opens), cancellationToken)).GetProperty("id").GetInt64();

        var ordinal = 1;
        foreach (var (icao, movements, arrivals, departures) in another is { } second ? new[] { airport, second } : [airport])
        {
            await CreatedJsonAsync(
                coordinator,
                EventAirportEndpoints.Pattern,
                new EventAirportWriteDto(id, icao, ordinal++, movements, arrivals, departures, RowVersion: default),
                cancellationToken);
        }

        return id;
    }

    /// <summary>A new capacity of arrivals for an airport of the event, through its form.</summary>
    private static async Task SetCapacityAsync(HttpClient coordinator, long eventId, string icao, int arrivals, CancellationToken cancellationToken)
    {
        var list = await OkAsync(await coordinator.GetAsync($"{EventAirportEndpoints.Pattern}?filter[eventId]={eventId}&pageSize=100", cancellationToken), cancellationToken);
        var row = list.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("icao").GetString() == icao);

        await OkAsync(
            await coordinator.PutAsJsonAsync(
                $"{EventAirportEndpoints.Pattern}/{row.GetProperty("id").GetInt64()}",
                new EventAirportWriteDto(eventId, icao, row.GetProperty("ordinal").GetInt32(), null, arrivals, null, row.GetProperty("rowVersion").GetDateTime()),
                cancellationToken),
            cancellationToken);
    }

    private static async Task PublishAsync(HttpClient coordinator, long id, CancellationToken cancellationToken) =>
        await OkAsync(await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/publish", new EventPublishRequest(default), cancellationToken), cancellationToken);

    private static Task<HttpResponseMessage> LoadAsync(HttpClient client, long id, string table, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/slots/load", new SlotLoadRequest(table, SlotLoadMode.Add), cancellationToken);

    private static Task<HttpResponseMessage> GenerateAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        client.PostAsync(new Uri($"{EventEndpoints.Pattern}/{id}/slots/generate", UriKind.Relative), null, cancellationToken);

    private static Task<HttpResponseMessage> BookPrivateAsync(HttpClient pilot, PrivateBookingRequest request, CancellationToken cancellationToken) =>
        pilot.PostAsJsonAsync($"{BookingEndpoints.MinePattern}/private", request, cancellationToken);

    private static async Task<JsonElement> CreatedJsonAsync<T>(HttpClient client, string uri, T payload, CancellationToken cancellationToken) =>
        await CreatedAsync(await client.PostAsJsonAsync(uri, payload, cancellationToken), cancellationToken);

    private static async Task<JsonElement> CreatedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.Created,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
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
    private static async Task<string> TokenAsync(HttpClient member, CancellationToken cancellationToken)
    {
        using var created = await member.PostAsJsonAsync(
            PersonalTokenEndpoints.Pattern,
            new { name = "evt-test-e7 gate", audience = BookingsExport.Audience, days = 1 },
            cancellationToken);
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync(cancellationToken));
        return (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("token").GetString()!;
    }

    /// <summary>A program that sends a token and nothing else: no cookie.</summary>
    private HttpClient Program(string text)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", text);
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

    /// <summary>The hours of the reminder as the division's settings say them: the test's times are counted from it.</summary>
    private async Task<TimeSpan> LeadAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var settings = await scope.ServiceProvider.GetRequiredService<ModuleSettingsStore>()
            .GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        Assert.True(settings.ReminderLeadHours >= 4, "The test counts its flights from a reminder of four hours at least.");
        return TimeSpan.FromHours(settings.ReminderLeadHours);
    }

    private async Task<Event> EventRowAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    /// <summary>The private slots of an event as stored, read past every filter.</summary>
    private async Task<List<EventSlot>> PrivateSlotsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Slots.IgnoreQueryFilters().AsNoTracking()
            .Where(slot => slot.EventId == eventId && slot.Kind == SlotKind.Private)
            .OrderBy(slot => slot.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The public slots of an event by their callsigns.</summary>
    private async Task<Dictionary<string, long>> SlotIdsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Slots.IgnoreQueryFilters().AsNoTracking()
            .Where(slot => slot.EventId == eventId && slot.Kind == SlotKind.Public)
            .ToDictionaryAsync(slot => slot.Callsign!, slot => slot.Id, cancellationToken);
    }

    /// <summary>The bookings of an event as stored, read past every filter.</summary>
    private async Task<List<EventBooking>> BookingsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Bookings.IgnoreQueryFilters().AsNoTracking()
            .Where(booking => booking.EventId == eventId)
            .OrderBy(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Until a transaction waits for a lock: the request under test, behind the transaction the test holds.</summary>
    /// <remarks>InnoDB refreshes <c>INNODB_TRX</c> only when nobody has read it for 100 ms: asked more often, it keeps answering what
    /// it saw the first time (CONTRIBUTING.md).</remarks>
    private async Task WaitForALockWaitAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.INNODB_TRX WHERE trx_state = 'LOCK WAIT'", connection);
        for (var tries = 0; tries < 40; tries++)
        {
            await Task.Delay(250, cancellationToken);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0)
            {
                return;
            }
        }

        Assert.Fail("The request never waited for the transaction the test holds.");
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A member with one position of the staff — or none, a member of the division and nothing else —, with an address or not.</summary>
    private async Task SeedUserAsync(int vid, string? position, string? email, CancellationToken cancellationToken)
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
        user.LastName = "Private";
        user.Email = email;
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

    /// <summary>The airports and the aircraft types of the class, in the core's snapshots, where a slot's and a flight's are checked.</summary>
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

    /// <summary>The events of this class with their rows and what they projected, the mails and tokens of its people, whatever a stopped run left.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await hub.Notifications.Where(row => People.Contains(row.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.PersonalTokens.Where(row => People.Contains(row.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.JobsLog.Where(run => run.Job == BookingRemindersJob.JobName).ExecuteDeleteAsync(cancellationToken);
    }
}
