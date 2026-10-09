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
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Export;
using IvaoHub.Modules.Events.Public;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The bookings of the pilots (M4, E6a), through the real host with the division file of this repository: a pilot books a public slot
/// of an event they see while its bookings are open and its off block is to come, with an aircraft the slot allows, compatible with
/// their other bookings of the event; books a whole rotation and is told which legs were taken; reads their own bookings and nobody
/// else's; the staff take a booking away with a reason, and the pilot is told. The rules that grow: a booked slot is not deleted nor
/// replaced, an event with bookings is not deleted, and whoever booked hears of a cancellation and of new times.
/// <para><b>The same instant</b> (design §10.1): a transaction of the test holds what the first request would hold — a booking written
/// and not committed, the lock of the pilot —, the request under test is sent and waits for it in the database (the test sees it in
/// <c>INNODB_TRX</c>), and the test commits: the request then reads what the other saved. Without the unique index, or without the
/// lock, the request would not wait at all. Besides those, the first bookings of six pilots are sent together, for real (the review
/// of #233, point 4).</para>
/// <para>⚠️ The coordinator is seeded **without an address**, as in <see cref="EventsStaffTests"/>: the tests of the contacts assert who
/// of the events receives a message. So are the three members of the staff of the bookings by a grant. The two pilots are members,
/// with an address each: they are told by mail.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsBookingsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // E6a's in the range the events module owns in the shared database (CONTRIBUTING.md): 761043–761046, left by E4.
    private const int CoordinatorVid = 761043;
    private const int PilotVid = 761044;
    private const int OtherPilotVid = 761045;
    private const int MemberVid = 761046;

    // Three more, which E10f (merged, #213) left free: the staff of the bookings by a grant to a VID, never by a position.
    private const int TrainingStaffVid = 761084;
    private const int ToursStaffVid = 761085;
    private const int OneEventStaffVid = 761086;

    /// <summary>Two airports of the events of this class, and two away from them: of no country the network has.</summary>
    private const string First = "XEF1";
    private const string Second = "XEF2";
    private const string Away = "XEF3";
    private const string Further = "XEF4";

    /// <summary>Two aircraft types the core knows here.</summary>
    private const string TypeA = "XE6A";
    private const string TypeB = "XE6B";

    private const string SlugStem = "evt-test-e6a";

    private const string Header = "callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly string[] Airports = [First, Second, Away, Further];

    private static readonly int[] People = [CoordinatorVid, PilotVid, OtherPilotVid, MemberVid, TrainingStaffVid, ToursStaffVid, OneEventStaffVid];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        // The recorded answers of the network (E10b's warning in HANDOFF-M4.md): no test calls it.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-EC", email: null, token);
        await SeedUserAsync(PilotVid, position: null, "e6a-pilot@example.org", token);
        await SeedUserAsync(OtherPilotVid, position: null, "e6a-other@example.org", token);
        await SeedUserAsync(MemberVid, position: null, email: null, token);
        await SeedUserAsync(TrainingStaffVid, position: null, email: null, token, staff: true);
        await SeedUserAsync(ToursStaffVid, position: null, email: null, token, staff: true);
        await SeedUserAsync(OneEventStaffVid, position: null, email: null, token, staff: true);
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
    public async Task APilotBooksAPublicSlotWithAnAircraftItAllowsAndOnlyTheyReadIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(
            coordinator,
            "book",
            Table(
                Line("XEA101", "XA101", $"{TypeA}/{TypeB}", First, day.AddHours(17), Away, day.AddHours(18), "B1"),
                Line("XEA102", string.Empty, TypeA, Away, day.AddHours(18).AddMinutes(5), First, day.AddHours(19)),
                Line("XEA103", string.Empty, TypeA, First, day.AddHours(21), Further, day.AddHours(22))),
            token);
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        using var other = await SignedInAsync(OtherPilotVid, token);
        using var member = await SignedInAsync(MemberVid, token);

        // What a request says by itself: an aircraft written, as the network writes one.
        var refused = await RefusedAsync(await BookAsync(pilot, slots["XEA101"], aircraft: null, token), token);
        Assert.Equal(["errors.required"], refused["aircraftIcao"]);
        refused = await RefusedAsync(await BookAsync(pilot, slots["XEA101"], "A-320", token), token);
        Assert.Equal(["events:errors.aircraftFormat"], refused["aircraftIcao"]);

        // An aircraft the slot does not allow; then one it does, written in lower case.
        refused = await RefusedAsync(await BookAsync(pilot, slots["XEA102"], TypeB, token), token);
        Assert.Equal([BookingRules.AircraftKey], refused["aircraftIcao"]);

        var booked = await CreatedAsync(await BookAsync(pilot, slots["XEA101"], TypeB.ToLowerInvariant(), token), token);
        Assert.Equal((slots["XEA101"], TypeB, "XEA101", true), (
            booked.GetProperty("slotId").GetInt64(),
            booked.GetProperty("aircraftIcao").GetString(),
            booked.GetProperty("callsign").GetString(),
            booked.GetProperty("withdrawable").GetBoolean()));
        Assert.Equal(Slug("book"), booked.GetProperty("eventSlug").GetString());

        // The same slot again is theirs already; for another pilot it is taken.
        Assert.Equal([BookingRules.YoursKey], (await RefusedAsync(await BookAsync(pilot, slots["XEA101"], TypeA, token), token))["slotId"]);
        Assert.Equal([BookingRules.TakenKey], (await RefusedAsync(await BookAsync(other, slots["XEA101"], TypeA, token), token))["slotId"]);

        // A flight leaving five minutes after theirs lands is too close to it (§3.5, ten minutes by default); a later one is not.
        Assert.Equal([BookingRules.IncompatibleKey], (await RefusedAsync(await BookAsync(pilot, slots["XEA102"], TypeA, token), token))["slotId"]);
        await CreatedAsync(await BookAsync(pilot, slots["XEA103"], TypeA, token), token);

        // A slot that does not exist, and one of an event nobody sees yet: not found.
        using (var nowhere = await BookAsync(pilot, long.MaxValue, TypeA, token))
        {
            Assert.Equal(HttpStatusCode.NotFound, nowhere.StatusCode);
        }

        var draft = Id(await CreatedJsonAsync(coordinator, EventEndpoints.Pattern, Payload("draft", day.AddHours(17), opens: DateTime.UtcNow.AddDays(-1)), token));
        await CreatedJsonAsync(coordinator, EventAirportEndpoints.Pattern, Airport(draft, First, 1), token);
        await OkAsync(await LoadAsync(coordinator, draft, Table(Line("XEA109", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), token), token);
        using (var unseen = await BookAsync(pilot, (await SlotIdsAsync(draft, token))["XEA109"], TypeA, token))
        {
            Assert.Equal(HttpStatusCode.NotFound, unseen.StatusCode);
        }

        // Each reads their own: the pilot their two, the other pilot and a member none; a visitor is not signed in.
        Assert.Equal(["XEA101", "XEA103"], (await MineAsync(pilot, token)).Select(row => row.GetProperty("callsign").GetString()));
        Assert.Empty(await MineAsync(other, token));
        Assert.Empty(await MineAsync(member, token));
        using (var anonymous = _factory.CreateApiClient())
        using (var nobody = await anonymous.GetAsync(BookingEndpoints.MinePattern, token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, nobody.StatusCode);
        }

        // Nobody else withdraws it nor takes it away: a member's withdrawal does not find it, and taking away is the staff's.
        var bookingId = Id(booked);
        using (var notTheirs = await member.DeleteAsync($"{BookingEndpoints.MinePattern}/{bookingId}", token))
        {
            Assert.Equal(HttpStatusCode.NotFound, notTheirs.StatusCode);
        }

        using (var notStaff = await RemoveAsync(member, bookingId, "evt-test-e6a not mine", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, notStaff.StatusCode);
        }

        // The page of the event says the two slots are taken, and nothing of whoever took them.
        var page = await OkAsync(await coordinator.GetAsync($"{PublicEventEndpoints.Pattern}/{Slug("book")}", token), token);
        var listed = page.GetProperty("slots").EnumerateArray().ToDictionary(slot => slot.GetProperty("callsign").GetString()!);
        Assert.Equal((true, false, true), (listed["XEA101"].GetProperty("taken").GetBoolean(), listed["XEA102"].GetProperty("taken").GetBoolean(), listed["XEA103"].GetProperty("taken").GetBoolean()));
        Assert.DoesNotContain(page.GetRawText(), PilotVid.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

        // The gate manager reads who booked and the aircraft they chose (§7.4), on a free slot nobody and nothing.
        await TouchLoginAsync(CoordinatorVid, token);
        using var gateManager = Program(await TokenAsync(coordinator, BookingsExport.Audience, token));
        gateManager.DefaultRequestHeaders.Add(BookingsExport.Contract.Header, "1");
        var flights = (await OkAsync(await gateManager.GetAsync(ExportUri("book"), token), token)).EnumerateArray()
            .ToDictionary(flight => flight.GetProperty("callsign").GetString()!);
        Assert.Equal((PilotVid, TypeB), (flights["XEA101"].GetProperty("booked_by").GetInt32(), flights["XEA101"].GetProperty("aircraft_icao").GetString()));
        Assert.Equal((PilotVid, TypeA), (flights["XEA103"].GetProperty("booked_by").GetInt32(), flights["XEA103"].GetProperty("aircraft_icao").GetString()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (flights["XEA102"].GetProperty("booked_by").ValueKind, flights["XEA102"].GetProperty("aircraft_icao").ValueKind));

        // Each booking was written by its pilot, in the audit of the core.
        await using var scope = _factory.Services.CreateAsyncScope();
        var bookingKey = bookingId.ToString(CultureInfo.InvariantCulture);
        Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(entry => entry.Entity == "evt_bookings" && entry.EntityId == bookingKey && entry.Action == "created" && entry.Vid == PilotVid, token));
    }

    [Fact]
    public async Task APilotWithdrawsUntilTheOffBlockAndTheSlotIsFreeAgain()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(coordinator, "withdraw", Table(Line("XEA121", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), token);
        var slot = (await SlotIdsAsync(id, token))["XEA121"];

        using var pilot = await SignedInAsync(PilotVid, token);
        using var other = await SignedInAsync(OtherPilotVid, token);
        var bookingId = Id(await CreatedAsync(await BookAsync(pilot, slot, TypeA, token), token));

        // Somebody else's booking is not theirs to withdraw: not found.
        using (var notTheirs = await other.DeleteAsync($"{BookingEndpoints.MinePattern}/{bookingId}", token))
        {
            Assert.Equal(HttpStatusCode.NotFound, notTheirs.StatusCode);
        }

        // The pilot withdraws it (§3.6): the row is deleted through the write guard of the core — the member it is about takes
        // back what they sent (E10h) —, and the audit keeps who did it.
        using (var withdrawn = await pilot.DeleteAsync($"{BookingEndpoints.MinePattern}/{bookingId}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        }

        Assert.Empty(await BookingsAsync(id, token));
        Assert.Empty(await MineAsync(pilot, token));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var key = bookingId.ToString(CultureInfo.InvariantCulture);
            Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
                .AnyAsync(entry => entry.Entity == "evt_bookings" && entry.EntityId == key && entry.Action == "deleted" && entry.Vid == PilotVid, token));
        }

        // The slot is free again: on the page, and for the next pilot. A booking withdrawn is withdrawn once.
        var page = await OkAsync(await coordinator.GetAsync($"{PublicEventEndpoints.Pattern}/{Slug("withdraw")}", token), token);
        Assert.False(page.GetProperty("slots").EnumerateArray().Single().GetProperty("taken").GetBoolean());
        await CreatedAsync(await BookAsync(other, slot, TypeA, token), token);

        using var twice = await pilot.DeleteAsync($"{BookingEndpoints.MinePattern}/{bookingId}", token);
        Assert.Equal(HttpStatusCode.NotFound, twice.StatusCode);
    }

    [Fact]
    public async Task DuringTheEventASlotIsBookedWhileItsOffBlockIsToComeAndNotWithdrawnAfterIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // An event in progress: it started an hour ago and ends in four; its bookings opened two days ago.
        var now = Minute(DateTime.UtcNow);
        var id = await PublishedAsync(
            coordinator,
            "running",
            Table(
                Line("XEA201", string.Empty, TypeA, First, now.AddMinutes(-30), Away, now.AddMinutes(30)),
                Line("XEA202", string.Empty, TypeA, First, now.AddHours(1), Away, now.AddHours(2))),
            token,
            starts: now.AddHours(-1),
            ends: now.AddHours(4),
            opens: now.AddDays(-2));
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);

        // Its off block has passed: closed. The other's is to come: booked, in the middle of the event.
        Assert.Equal([BookingRules.ClosedKey], (await RefusedAsync(await BookAsync(pilot, slots["XEA201"], TypeA, token), token))["slotId"]);
        var booked = await CreatedAsync(await BookAsync(pilot, slots["XEA202"], TypeA, token), token);
        Assert.Equal(nameof(EventStateKind.InProgress), booked.GetProperty("eventState").GetString());

        // A booking made before its off block, which has passed since: not withdrawn any more (§3.6), and it stays.
        var past = await BookedDirectlyAsync(id, slots["XEA201"], PilotVid, token);
        Assert.Equal([BookingRules.ClosedKey], (await RefusedAsync(await pilot.DeleteAsync($"{BookingEndpoints.MinePattern}/{past}", token), token))["id"]);
        Assert.Contains(await BookingsAsync(id, token), booking => booking.Id == past);
        Assert.False((await MineAsync(pilot, token)).Single(row => Id(row) == past).GetProperty("withdrawable").GetBoolean());
    }

    [Fact]
    public async Task TheBookingsOpenAtTheirMomentAndACancelledEventTellsWhoeverBookedOnce()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);
        var day = Day(days: 30);

        // Seen, and its bookings open tomorrow: not yet.
        var later = await PublishedAsync(
            coordinator,
            "later",
            Table(Line("XEA301", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))),
            token,
            opens: DateTime.UtcNow.AddDays(1));
        Assert.Equal(
            [BookingRules.NotOpenKey],
            (await RefusedAsync(await BookAsync(pilot, (await SlotIdsAsync(later, token))["XEA301"], TypeA, token), token))["slotId"]);

        // Two bookings of the pilot in an event that is then cancelled: one mail, with the note, and none to who booked nothing.
        var id = await PublishedAsync(
            coordinator,
            "cancelled",
            Table(
                Line("XEA311", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18)),
                Line("XEA312", string.Empty, TypeA, First, day.AddHours(19), Away, day.AddHours(20)),
                Line("XEA313", string.Empty, TypeA, First, day.AddHours(21), Away, day.AddHours(22))),
            token);
        var slots = await SlotIdsAsync(id, token);
        await CreatedAsync(await BookAsync(pilot, slots["XEA311"], TypeA, token), token);
        await CreatedAsync(await BookAsync(pilot, slots["XEA312"], TypeA, token), token);

        var stored = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
        await OkAsync(
            await coordinator.PostAsJsonAsync(
                $"{EventEndpoints.Pattern}/{id}/cancel",
                new EventCancelRequest(Text("evt-test-e6a the storm"), RowVersion(stored)),
                token),
            token);

        var told = await NotificationsAsync(EventsNotifications.EventCancelled, Slug("cancelled"), token);
        Assert.Equal([PilotVid], told.Select(row => row.Vid));
        Assert.Contains("evt-test-e6a the storm", told[0].DataJson, StringComparison.Ordinal);

        // A cancelled event takes no booking any more; the bookings it had stay.
        Assert.Equal(
            [BookingRules.CancelledKey],
            (await RefusedAsync(await BookAsync(pilot, slots["XEA313"], TypeA, token), token))["slotId"]);
        Assert.Equal(2, (await BookingsAsync(id, token)).Count);
    }

    [Fact]
    public async Task TheWholeRotationIsBookedLegByLegAndSaysWhichLegsWereNot()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);

        // A rotation of four legs — the last one for another aircraft —, a second one of two, and a slot alone.
        var id = await PublishedAsync(
            coordinator,
            "rotation",
            Table(
                Line("XEA401", string.Empty, $"{TypeA}/{TypeB}", First, day.AddHours(17), Away, day.AddHours(18), rotation: "R1", leg: "1"),
                Line("XEA402", string.Empty, $"{TypeA}/{TypeB}", Away, day.AddHours(18).AddMinutes(30), First, day.AddHours(19).AddMinutes(30), rotation: "R1", leg: "2"),
                Line("XEA403", string.Empty, $"{TypeA}/{TypeB}", First, day.AddHours(20), Further, day.AddHours(21), rotation: "R1", leg: "3"),
                Line("XEA404", string.Empty, TypeB, Further, day.AddHours(21).AddMinutes(30), First, day.AddHours(22).AddMinutes(30), rotation: "R1", leg: "4"),
                Line("XEA405", string.Empty, TypeA, First, day.AddHours(17), Second, day.AddHours(18)),
                Line("XEA411", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18), rotation: "R2", leg: "1"),
                Line("XEA412", string.Empty, TypeA, Away, day.AddHours(18).AddMinutes(30), First, day.AddHours(19).AddMinutes(30), rotation: "R2", leg: "2")),
            token);
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        using var other = await SignedInAsync(OtherPilotVid, token);
        using var member = await SignedInAsync(MemberVid, token);

        // Another pilot has the second leg.
        await CreatedAsync(await BookAsync(other, slots["XEA402"], TypeA, token), token);

        // The whole rotation, asked from any of its legs: the first and the third are booked; the second was taken, the fourth does
        // not allow the aircraft.
        var rotation = await OkAsync(await RotationAsync(pilot, slots["XEA403"], TypeA, token), token);
        Assert.Equal(["XEA401", "XEA403"], rotation.GetProperty("booked").EnumerateArray().Select(row => row.GetProperty("callsign").GetString()));
        Assert.Equal(
            [("XEA402", 2, BookingRules.TakenKey), ("XEA404", 4, BookingRules.AircraftKey)],
            rotation.GetProperty("notBooked").EnumerateArray().Select(row => (
                row.GetProperty("callsign").GetString(),
                row.GetProperty("leg").GetInt32(),
                row.GetProperty("reason").GetString())));

        // Asked again: nothing more to book, and why for each leg.
        var again = await OkAsync(await RotationAsync(pilot, slots["XEA401"], TypeA, token), token);
        Assert.Empty(again.GetProperty("booked").EnumerateArray());
        Assert.Equal(
            [BookingRules.YoursKey, BookingRules.TakenKey, BookingRules.YoursKey, BookingRules.AircraftKey],
            again.GetProperty("notBooked").EnumerateArray().Select(row => row.GetProperty("reason").GetString()));

        // A leg too close to a booking the pilot already has is not booked either (§3.5): a member holding XEA405, at the time of the
        // first leg of the second rotation, gets its second leg alone.
        await CreatedAsync(await BookAsync(member, slots["XEA405"], TypeA, token), token);
        var theirs = await OkAsync(await RotationAsync(member, slots["XEA412"], TypeA, token), token);
        Assert.Equal(["XEA412"], theirs.GetProperty("booked").EnumerateArray().Select(row => row.GetProperty("callsign").GetString()));
        Assert.Equal(
            [("XEA411", BookingRules.IncompatibleKey)],
            theirs.GetProperty("notBooked").EnumerateArray().Select(row => (row.GetProperty("callsign").GetString(), row.GetProperty("reason").GetString())));

        // A slot that is not a leg of a rotation.
        Assert.Equal(["events:errors.slotNotInRotation"], (await RefusedAsync(await RotationAsync(pilot, slots["XEA405"], TypeA, token), token))["slotId"]);

        // One booking per slot, whoever made it.
        var stored = await BookingsAsync(id, token);
        Assert.Equal(stored.Count, stored.Select(booking => booking.SlotId).Distinct().Count());
        Assert.Equal(
            [(slots["XEA401"], PilotVid), (slots["XEA402"], OtherPilotVid), (slots["XEA403"], PilotVid), (slots["XEA405"], MemberVid), (slots["XEA412"], MemberVid)],
            stored.Select(booking => (booking.SlotId, booking.BookerVid)).Order());
    }

    [Fact]
    public async Task TheStaffTakeABookingAwayWithAReasonAndThePilotIsTold()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(
            coordinator,
            "remove",
            Table(Line("XEA501", "XA501", TypeA, First, day.AddHours(17), Away, day.AddHours(18), "C4")),
            token);
        var slot = (await SlotIdsAsync(id, token))["XEA501"];

        using var pilot = await SignedInAsync(PilotVid, token);
        var bookingId = Id(await CreatedAsync(await BookAsync(pilot, slot, TypeA, token), token));

        // A reason is required.
        Assert.Equal(["errors.required"], (await RefusedAsync(await RemoveAsync(coordinator, bookingId, reason: " ", token), token))["reason"]);

        using (var removed = await RemoveAsync(coordinator, bookingId, "evt-test-e6a a double booking", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        // Gone, the slot free again, the pilot told why, and the audit says who took it away.
        Assert.Empty(await BookingsAsync(id, token));
        Assert.Empty(await MineAsync(pilot, token));
        var page = await OkAsync(await coordinator.GetAsync($"{PublicEventEndpoints.Pattern}/{Slug("remove")}", token), token);
        Assert.False(page.GetProperty("slots").EnumerateArray().Single().GetProperty("taken").GetBoolean());

        var told = Assert.Single(await NotificationsAsync(EventsNotifications.BookingRemoved, Slug("remove"), token));
        Assert.Equal(PilotVid, told.Vid);
        Assert.Contains("evt-test-e6a a double booking", told.DataJson, StringComparison.Ordinal);
        Assert.Contains("XEA501", told.DataJson, StringComparison.Ordinal);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var key = bookingId.ToString(CultureInfo.InvariantCulture);
            Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
                .AnyAsync(entry => entry.Entity == "evt_bookings" && entry.EntityId == key && entry.Action == "deleted" && entry.Vid == CoordinatorVid, token));
        }

        // Taken away once: a second time there is nothing to take.
        using var twice = await RemoveAsync(coordinator, bookingId, "evt-test-e6a again", token);
        Assert.Equal(HttpStatusCode.NotFound, twice.StatusCode);
    }

    [Fact]
    public async Task ABookedSlotAndAnEventWithBookingsStayAndNewTimesAreToldToWhoeverBooked()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(
            coordinator,
            "grow",
            Table(
                Line("XEA601", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18)),
                Line("XEA602", string.Empty, TypeA, First, day.AddHours(19), Away, day.AddHours(20)),
                Line("XEA603", string.Empty, TypeA, First, day.AddHours(21), Away, day.AddHours(22))),
            token);
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        await CreatedAsync(await BookAsync(pilot, slots["XEA601"], TypeA, token), token);

        // A booked slot is not deleted (§1.5): the staff take its booking away first.
        Assert.Equal(["events:errors.slotBooked"], (await RefusedAsync(await coordinator.DeleteAsync($"{EventSlotEndpoints.Pattern}/{slots["XEA601"]}", token), token))["id"]);

        // «Replace the free ones» leaves it, and so does «delete the free ones».
        var replaced = await OkAsync(
            await LoadAsync(coordinator, id, Table(Line("XEA611", string.Empty, TypeA, First, day.AddHours(19), Away, day.AddHours(20))), token, SlotLoadMode.ReplaceFree),
            token);
        Assert.Equal((1, 2), (replaced.GetProperty("added").GetInt32(), replaced.GetProperty("removed").GetInt32()));
        Assert.Equal(["XEA601", "XEA611"], (await SlotIdsAsync(id, token)).Keys.Order(StringComparer.Ordinal));

        var free = await OkAsync(await coordinator.PostAsync(new Uri($"{EventEndpoints.Pattern}/{id}/slots/delete-free", UriKind.Relative), null, token), token);
        Assert.Equal(1, free.GetProperty("removed").GetInt32());
        Assert.Equal(["XEA601"], (await SlotIdsAsync(id, token)).Keys);

        // An event somebody booked is not deleted: it is cancelled instead (§2.3).
        Assert.Equal(["events:errors.eventHasBookings"], (await RefusedAsync(await coordinator.DeleteAsync($"{EventEndpoints.Pattern}/{id}", token), token))["id"]);

        // New times: whoever booked is told, once; a save that keeps them tells nobody.
        var stored = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
        var moved = await OkAsync(
            await coordinator.PutAsJsonAsync(
                $"{EventEndpoints.Pattern}/{id}",
                Payload("grow", day.AddHours(16), opens: DateTime.UtcNow.AddDays(-1)) with { RowVersion = RowVersion(stored) },
                token),
            token);
        await OkAsync(
            await coordinator.PutAsJsonAsync(
                $"{EventEndpoints.Pattern}/{id}",
                Payload("grow", day.AddHours(16), opens: DateTime.UtcNow.AddDays(-1)) with { Summary = Text("evt-test-e6a grow, again"), RowVersion = RowVersion(moved) },
                token),
            token);

        var told = Assert.Single(await NotificationsAsync(EventsNotifications.EventChanged, Slug("grow"), token));
        Assert.Equal(PilotVid, told.Vid);
        Assert.Contains(day.AddHours(16).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), told.DataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoPilotsBookingTheSameSlotInTheSameInstantOneWins()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(coordinator, "race", Table(Line("XEA701", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), token);
        var slot = (await SlotIdsAsync(id, token))["XEA701"];

        using var pilot = await SignedInAsync(PilotVid, token);

        // The other pilot's booking written and not committed yet: the request finds the slot free, inserts, and waits on the
        // unique index of the slot until the other transaction decides.
        await using (var held = await HeldAsync(id, token))
        {
            await held.BookAsync(slot, OtherPilotVid, token);

            var racing = BookAsync(pilot, slot, TypeA, token);
            await WaitForALockWaitAsync(token);
            await held.CommitAsync(token);

            Assert.Equal([BookingRules.TakenKey], (await RefusedAsync(await racing, token))["slotId"]);
        }

        Assert.Equal([OtherPilotVid], (await BookingsAsync(id, token)).Select(booking => booking.BookerVid));
    }

    [Fact]
    public async Task TwoIncompatibleBookingsOfTheSamePilotInTheSameInstantOneWins()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);

        // XEA802 overlaps XEA801; XEA800 goes with both.
        var id = await PublishedAsync(
            coordinator,
            "same-pilot",
            Table(
                Line("XEA800", string.Empty, TypeA, First, day.AddHours(13), Away, day.AddHours(14)),
                Line("XEA801", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18)),
                Line("XEA802", string.Empty, TypeA, First, day.AddHours(17).AddMinutes(30), Second, day.AddHours(18).AddMinutes(30)),
                Line("XEA803", string.Empty, TypeA, First, day.AddHours(18).AddMinutes(5), Away, day.AddHours(19))),
            token,
            starts: day.AddHours(13));
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);

        // The pilot's first booking of the event in flight: it holds the row of the event, and has written XEA801. The second
        // request waits for it, then reads it, and refuses what does not go with it.
        await using (var held = await HeldAsync(id, token))
        {
            await held.LockAsync($"SELECT id AS `Value` FROM evt_events WHERE id = {id} FOR UPDATE", token);
            await held.BookAsync(slots["XEA801"], PilotVid, token);

            var racing = BookAsync(pilot, slots["XEA802"], TypeA, token);
            await WaitForALockWaitAsync(token);
            await held.CommitAsync(token);

            Assert.Equal([BookingRules.IncompatibleKey], (await RefusedAsync(await racing, token))["slotId"]);
        }

        // Once the pilot has a booking, the lock is the row of the first one: the same, one request after the other.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            await database.Bookings.IgnoreQueryFilters().Where(booking => booking.EventId == id).ExecuteDeleteAsync(token);
        }

        var first = Id(await CreatedAsync(await BookAsync(pilot, slots["XEA800"], TypeA, token), token));
        await using (var held = await HeldAsync(id, token))
        {
            await held.LockAsync($"SELECT id AS `Value` FROM evt_bookings WHERE id = {first} FOR UPDATE", token);
            await held.BookAsync(slots["XEA801"], PilotVid, token);

            var racing = BookAsync(pilot, slots["XEA803"], TypeA, token);
            await WaitForALockWaitAsync(token);
            await held.CommitAsync(token);

            Assert.Equal([BookingRules.IncompatibleKey], (await RefusedAsync(await racing, token))["slotId"]);
        }

        Assert.Equal(
            [slots["XEA800"], slots["XEA801"]],
            (await BookingsAsync(id, token)).Select(booking => booking.SlotId).Order());
    }

    [Fact]
    public async Task ABookingWaitingForTheEventWhileItIsCancelledIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(coordinator, "cancelling", Table(Line("XEA131", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), token);
        var slot = (await SlotIdsAsync(id, token))["XEA131"];

        using var pilot = await SignedInAsync(PilotVid, token);

        // The staff's cancellation written and not committed yet: the pilot's first booking reads the event open, then waits for its
        // row; once the cancellation is saved, the booking reads the event again under its lock and is refused — its pilot was not
        // among those the cancellation told.
        await using (var held = await HeldAsync(id, token))
        {
            await held.ExecuteAsync($"UPDATE evt_events SET cancelled_at = {DateTime.UtcNow} WHERE id = {id}", token);

            var racing = BookAsync(pilot, slot, TypeA, token);
            await WaitForALockWaitAsync(token);
            await held.CommitAsync(token);

            Assert.Equal([BookingRules.CancelledKey], (await RefusedAsync(await racing, token))["slotId"]);
        }

        Assert.Empty(await BookingsAsync(id, token));
    }

    [Fact]
    public async Task ALegOfTheRotationTakenInTheSameInstantFailsAloneAndTheRestAreBooked()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(
            coordinator,
            "race-rotation",
            Table(
                Line("XEA901", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18), rotation: "R9", leg: "1"),
                Line("XEA902", string.Empty, TypeA, Away, day.AddHours(18).AddMinutes(30), First, day.AddHours(19).AddMinutes(30), rotation: "R9", leg: "2"),
                Line("XEA903", string.Empty, TypeA, First, day.AddHours(20), Away, day.AddHours(21), rotation: "R9", leg: "3")),
            token);
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);

        // The other pilot writes the second leg in the same instant: the rotation books the first, waits on the second, and when the
        // other commits that insert alone fails; the third is booked in the same transaction.
        await using (var held = await HeldAsync(id, token))
        {
            await held.BookAsync(slots["XEA902"], OtherPilotVid, token);

            var racing = RotationAsync(pilot, slots["XEA901"], TypeA, token);
            await WaitForALockWaitAsync(token);
            await held.CommitAsync(token);

            var rotation = await OkAsync(await racing, token);
            Assert.Equal(["XEA901", "XEA903"], rotation.GetProperty("booked").EnumerateArray().Select(row => row.GetProperty("callsign").GetString()));
            var notBooked = Assert.Single(rotation.GetProperty("notBooked").EnumerateArray());
            Assert.Equal(("XEA902", BookingRules.TakenKey), (notBooked.GetProperty("callsign").GetString(), notBooked.GetProperty("reason").GetString()));
        }

        Assert.Equal(
            [(slots["XEA901"], PilotVid), (slots["XEA902"], OtherPilotVid), (slots["XEA903"], PilotVid)],
            (await BookingsAsync(id, token)).Select(booking => (booking.SlotId, booking.BookerVid)).Order());
    }

    [Fact]
    public async Task TheStaffTakeABookingAwayWithThePermissionOnItsEventAsItIsNow()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(coordinator, "care", Table(Line("XEA161", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), token);
        var other = await PublishedAsync(coordinator, "care-other", Table(Line("XEA162", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), token);

        // The event is in the care of the flight operations too when the pilot books, and the booking takes that care.
        await CareAsync(id, [Department.ED, Department.FOD], token);
        using var pilot = await SignedInAsync(PilotVid, token);
        using var created = await BookAsync(pilot, (await SlotIdsAsync(id, token))["XEA161"], TypeA, token);

        // No address of its own: the pilot reads it in their list (the review of #233, point 5).
        Assert.Null(created.Headers.Location);
        var booking = Id(await CreatedAsync(created, token));
        var elsewhere = Id(await CreatedAsync(await BookAsync(pilot, (await SlotIdsAsync(other, token))["XEA162"], TypeA, token), token));
        Assert.Equal(DepartmentMask.Of([Department.ED, Department.FOD]), (await BookingsAsync(id, token)).Single().OwnerDepartmentMask);

        // Then the training department takes the place of the flight operations on the event.
        await CareAsync(id, [Department.ED, Department.TD], token);

        await GrantAsync(TrainingStaffVid, Department.TD, scope: null, token);
        await GrantAsync(ToursStaffVid, Department.FOD, scope: null, token);
        await GrantAsync(OneEventStaffVid, Department.ED, Event.ScopeOf(other), token);
        using var training = await SignedInAsync(TrainingStaffVid, token);
        using var tours = await SignedInAsync(ToursStaffVid, token);
        using var oneEvent = await SignedInAsync(OneEventStaffVid, token);

        // A grant on another event, the department that left the event, a department the other event was never in: none of
        // them takes a booking away (the review of #233, points 1 and 2).
        foreach (var (client, bookingId) in new[] { (oneEvent, booking), (tours, booking), (training, elsewhere) })
        {
            using var refused = await RemoveAsync(client, bookingId, "evt-test-e6a not theirs", token);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        Assert.Equal(2, (await BookingsAsync(id, token)).Count + (await BookingsAsync(other, token)).Count);

        // The department that came in does, as the event is now.
        using (var removed = await RemoveAsync(training, booking, "evt-test-e6a the event is ours now", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        Assert.Empty(await BookingsAsync(id, token));
    }

    [Fact]
    public async Task ABookingCaughtInADeadlockIsToldToTryAgainNotThatTheSlotIsTaken()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        string[] heavy = ["XEA173", "XEA174", "XEA175", "XEA176", "XEA177", "XEA178", "XEA179", "XEA180"];
        var id = await PublishedAsync(
            coordinator,
            "deadlock",
            Table(
            [
                Line("XEA171", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18)),
                Line("XEA172", string.Empty, TypeA, First, day.AddHours(19), Away, day.AddHours(20)),
                .. heavy.Select((callsign, index) => Line(callsign, string.Empty, TypeA, First, day.AddHours(20).AddMinutes(5 * index), Away, day.AddHours(21))),
            ]),
            token);
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        var first = Id(await CreatedAsync(await BookAsync(pilot, slots["XEA171"], TypeA, token), token));

        // Another transaction has written many bookings, XEA172 among them, and not committed. The pilot's booking of XEA172
        // takes the pilot's lock — the row of their first booking —, finds the slot free, and waits on the other's key; then the
        // other asks for the row the pilot holds. The database rolls the lighter of the two back: the pilot's, which wrote one row.
        await using (var held = await HeldAsync(id, token))
        {
            foreach (var callsign in heavy)
            {
                await held.BookAsync(slots[callsign], OtherPilotVid, token);
            }

            await held.BookAsync(slots["XEA172"], OtherPilotVid, token);

            var racing = BookAsync(pilot, slots["XEA172"], TypeA, token);
            await WaitForALockWaitAsync(token);
            await held.LockAsync($"SELECT id AS `Value` FROM evt_bookings WHERE id = {first} FOR UPDATE", token);

            // Not «taken»: the other transaction may still roll back, and the slot be free (the review of #233, point 3).
            using var answer = await racing;
            Assert.Equal(HttpStatusCode.Conflict, answer.StatusCode);
        }

        Assert.Equal([slots["XEA171"]], (await BookingsAsync(id, token)).Select(booking => booking.SlotId));
    }

    [Fact]
    public async Task TheFirstBookingOfAPilotWaitingForAnotherPilotsFirstTakesNoGapAndGoesThrough()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(
            coordinator,
            "first-bookings",
            Table(
                Line("XEA181", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18)),
                Line("XEA182", string.Empty, TypeA, First, day.AddHours(19), Away, day.AddHours(20))),
            token);
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);

        // The other pilot's first booking in flight holds the row of the event, and has written nothing yet. The pilot's first
        // booking looks for a booking of theirs to lock, finds none, and waits for the row of the event; then the other writes its
        // booking and commits. Reading what is committed, the pilot's look took no gap of the index and the other's insert goes
        // through; under the default isolation it would have locked the gap that insert falls into, and the two would deadlock
        // (the note of E6a, reading 1; the review of #233, point 4).
        await using (var held = await HeldAsync(id, token))
        {
            await held.LockAsync($"SELECT id AS `Value` FROM evt_events WHERE id = {id} FOR UPDATE", token);

            var racing = BookAsync(pilot, slots["XEA182"], TypeA, token);
            await WaitForALockWaitAsync(token);
            await held.BookAsync(slots["XEA181"], OtherPilotVid, token);
            await held.CommitAsync(token);

            await CreatedAsync(await racing, token);
        }

        Assert.Equal(
            [(slots["XEA181"], OtherPilotVid), (slots["XEA182"], PilotVid)],
            (await BookingsAsync(id, token)).Select(booking => (booking.SlotId, booking.BookerVid)).Order());
    }

    [Fact]
    public async Task TheFirstBookingsOfSixPilotsSentTogetherAreAllMade()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        int[] pilots = [PilotVid, OtherPilotVid, MemberVid, TrainingStaffVid, ToursStaffVid, OneEventStaffVid];
        var id = await PublishedAsync(
            coordinator,
            "together",
            Table([.. pilots.Select((_, index) => Line($"XEA19{index}", string.Empty, TypeA, First, day.AddHours(17).AddMinutes(10 * index), Away, day.AddHours(19)))]),
            token);
        var slots = await SlotIdsAsync(id, token);

        // Six pilots, each their first booking of the event, in the same instant: they meet on the row of the event, one after the
        // other, and none of them deadlocks (the review of #233, point 4).
        var clients = new List<HttpClient>();
        try
        {
            foreach (var vid in pilots)
            {
                clients.Add(await SignedInAsync(vid, token));
            }

            var answers = await Task.WhenAll(clients.Select((client, index) => BookAsync(client, slots[$"XEA19{index}"], TypeA, token)));
            foreach (var answer in answers)
            {
                await CreatedAsync(answer, token);
            }
        }
        finally
        {
            clients.ForEach(client => client.Dispose());
        }

        Assert.Equal(pilots.Order(), (await BookingsAsync(id, token)).Select(booking => booking.BookerVid).Order());
    }

    [Fact]
    public async Task ABookingWaitingForTheEventWhileItStopsBeingSeenIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(coordinator, "unseen-meanwhile", Table(Line("XEA141", string.Empty, TypeA, First, day.AddHours(17), Away, day.AddHours(18))), token);
        var slot = (await SlotIdsAsync(id, token))["XEA141"];

        using var pilot = await SignedInAsync(PilotVid, token);

        // The staff move its «seen from» to tomorrow, not committed yet: the pilot's first booking reads the event seen, then waits
        // for its row; once that is saved, the booking reads the event again under its lock, as its first read did, and does not
        // find it (the review of #233, point 7).
        await using (var held = await HeldAsync(id, token))
        {
            await held.ExecuteAsync($"UPDATE evt_events SET visible_from_utc = {DateTime.UtcNow.AddDays(1)} WHERE id = {id}", token);

            var racing = BookAsync(pilot, slot, TypeA, token);
            await WaitForALockWaitAsync(token);
            await held.CommitAsync(token);

            using var answer = await racing;
            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
        }

        Assert.Empty(await BookingsAsync(id, token));
    }

    [Fact]
    public async Task ABookedSlotCorrectedByTheStaffKeepsItsBookingAndTellsItsPilotWhenTheFlightChanges()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 30);
        var id = await PublishedAsync(
            coordinator,
            "corrected",
            Table(
                Line("XEA211", "XA211", $"{TypeA}/{TypeB}", First, day.AddHours(17), Away, day.AddHours(18), "A1"),
                Line("XEA212", string.Empty, TypeA, Away, day.AddHours(19), First, day.AddHours(20)),
                Line("XEA213", string.Empty, TypeA, First, day.AddHours(21), Away, day.AddHours(22))),
            token);
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        var booking = Id(await CreatedAsync(await BookAsync(pilot, slots["XEA211"], TypeB, token), token));
        await CreatedAsync(await BookAsync(pilot, slots["XEA212"], TypeA, token), token);

        // The stand, the flight number, and which admitted type comes first: the staff's to plan with, and nobody is told.
        await CorrectAsync(coordinator, slots["XEA211"], slot => slot with { Stand = "B7", FlightNumber = "XA299", MainAircraftType = TypeB, OtherAircraftTypes = TypeA }, token);
        Assert.Empty(await NotificationsAsync(EventsNotifications.BookingChanged, Slug("corrected"), token));

        // New times, five minutes from the pilot's other booking where the event asks ten (Carmine's answer 3 on #233): the
        // correction is saved, nothing is checked again, the booking stays as it is, and its pilot is told the flight as it is now.
        await CorrectAsync(coordinator, slots["XEA211"], slot => slot with { OffBlockUtc = day.AddHours(17).AddMinutes(30), OnBlockUtc = day.AddHours(18).AddMinutes(55) }, token);
        var told = Assert.Single(await NotificationsAsync(EventsNotifications.BookingChanged, Slug("corrected"), token));
        Assert.Equal(PilotVid, told.Vid);
        Assert.Contains(Stamp(day.AddHours(18).AddMinutes(55)), told.DataJson, StringComparison.Ordinal);
        Assert.Contains("XEA211", told.DataJson, StringComparison.Ordinal);

        // Admitted types that leave the pilot's aircraft out: saved, the booking keeps it, and the pilot is told again.
        await CorrectAsync(coordinator, slots["XEA211"], slot => slot with { MainAircraftType = TypeA, OtherAircraftTypes = null }, token);
        Assert.Equal(2, (await NotificationsAsync(EventsNotifications.BookingChanged, Slug("corrected"), token)).Count);
        var kept = Assert.Single(await BookingsAsync(id, token), row => row.SlotId == slots["XEA211"]);
        Assert.Equal((booking, TypeB), (kept.Id, kept.AircraftIcao));

        // A slot nobody booked: corrected, and nobody is told.
        await CorrectAsync(coordinator, slots["XEA213"], slot => slot with { OffBlockUtc = day.AddHours(21).AddMinutes(20), OnBlockUtc = day.AddHours(22).AddMinutes(20) }, token);
        Assert.Equal(2, (await NotificationsAsync(EventsNotifications.BookingChanged, Slug("corrected"), token)).Count);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>Midnight of a day some days from today, in UTC: the slots are at hours of it.</summary>
    private static DateTime Day(int days) => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days), DateTimeKind.Utc);

    /// <summary>An instant to the minute, as a table writes it: what is loaded is what is compared.</summary>
    private static DateTime Minute(DateTime at) => new(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, DateTimeKind.Utc);

    /// <summary>An RFO with public slots, called after its address in both languages of the division.</summary>
    private static EventWriteDto Payload(string name, DateTime starts, DateTime opens, DateTime? ends = null) => new(
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
        BookingOpensAtUtc: opens,
        StartsAtUtc: starts,
        EndsAtUtc: ends ?? starts.AddHours(5),
        Visibility: Visibility.Public,
        RowVersion: default);

    private static EventAirportWriteDto Airport(long eventId, string icao, int ordinal) =>
        new(eventId, icao, ordinal, MaxMovementsPerHour: null, MaxArrivalsPerHour: null, MaxDeparturesPerHour: null, RowVersion: default);

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>A table as a spreadsheet copies it: the header, then a row per line, separated by tabs.</summary>
    private static string Table(params string[] lines) => string.Join("\r\n", [Header, .. lines]) + "\r\n";

    private static string Line(
        string callsign,
        string flightNumber,
        string types,
        string departure,
        DateTime offBlock,
        string arrival,
        DateTime onBlock,
        string stand = "",
        string rotation = "",
        string leg = "") =>
        string.Join('\t', callsign, flightNumber, types, departure, Stamp(offBlock), arrival, Stamp(onBlock), stand, rotation, leg);

    /// <summary>An instant as the table writes it: in UTC, to the minute.</summary>
    private static string Stamp(DateTime at) => at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private static DateTime RowVersion(JsonElement row) => row.GetProperty("rowVersion").GetDateTime();

    private static Uri ExportUri(string name) =>
        new(BookingsExport.Pattern.Replace("{slug}", Slug(name), StringComparison.Ordinal), UriKind.Relative);

    private static Task<HttpResponseMessage> BookAsync(HttpClient client, long slotId, string? aircraft, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(slotId, aircraft), cancellationToken);

    private static Task<HttpResponseMessage> RotationAsync(HttpClient client, long slotId, string? aircraft, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{BookingEndpoints.MinePattern}/rotation", new BookingRequest(slotId, aircraft), cancellationToken);

    private static Task<HttpResponseMessage> RemoveAsync(HttpClient client, long bookingId, string? reason, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{BookingEndpoints.StaffPattern}/{bookingId}/remove", new BookingRemovalRequest(reason), cancellationToken);

    private static async Task<List<JsonElement>> MineAsync(HttpClient client, CancellationToken cancellationToken) =>
        [.. (await OkAsync(await client.GetAsync(BookingEndpoints.MinePattern, cancellationToken), cancellationToken)).EnumerateArray()];

    /// <summary>A slot corrected from its form, as the staff correct one: read, changed, saved.</summary>
    private static async Task CorrectAsync(HttpClient staff, long slotId, Func<EventSlotWriteDto, EventSlotWriteDto> change, CancellationToken cancellationToken)
    {
        var slot = await OkAsync(await staff.GetAsync($"{EventSlotEndpoints.Pattern}/{slotId}", cancellationToken), cancellationToken);
        string? Text(string name) => slot.GetProperty(name).ValueKind == JsonValueKind.Null ? null : slot.GetProperty(name).GetString();
        var types = slot.GetProperty("aircraftTypes").EnumerateArray().Select(type => type.GetString()!).ToList();
        var stored = new EventSlotWriteDto(
            slot.GetProperty("eventId").GetInt64(),
            Text("callsign")!,
            Text("flightNumber"),
            types[0],
            types.Count > 1 ? string.Join('/', types.Skip(1)) : null,
            Text("departureIcao")!,
            slot.GetProperty("offBlockUtc").GetDateTime(),
            Text("arrivalIcao")!,
            slot.GetProperty("onBlockUtc").GetDateTime(),
            Text("stand"),
            Text("rotationCode"),
            slot.GetProperty("rotationLeg").ValueKind == JsonValueKind.Null ? null : slot.GetProperty("rotationLeg").GetInt32(),
            RowVersion(slot));

        await OkAsync(await staff.PutAsJsonAsync($"{EventSlotEndpoints.Pattern}/{slotId}", change(stored), cancellationToken), cancellationToken);
    }

    private static Task<HttpResponseMessage> LoadAsync(HttpClient client, long id, string text, CancellationToken cancellationToken, SlotLoadMode mode = SlotLoadMode.Add) =>
        client.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/slots/load", new SlotLoadRequest(text, mode), cancellationToken);

    /// <summary>
    /// An RFO with public slots and its two airports, its slots loaded from the table and published: by default thirty days from
    /// today, from 17:00 to 22:00, its bookings open since yesterday.
    /// </summary>
    private static async Task<long> PublishedAsync(
        HttpClient coordinator,
        string name,
        string table,
        CancellationToken cancellationToken,
        DateTime? starts = null,
        DateTime? ends = null,
        DateTime? opens = null)
    {
        var start = starts ?? Day(days: 30).AddHours(17);
        var payload = Payload(name, start, opens ?? DateTime.UtcNow.AddDays(-1), ends ?? (starts is null ? start.AddHours(5) : start.AddHours(9)));
        var id = Id(await CreatedJsonAsync(coordinator, EventEndpoints.Pattern, payload, cancellationToken));
        await CreatedJsonAsync(coordinator, EventAirportEndpoints.Pattern, Airport(id, First, 1), cancellationToken);
        await CreatedJsonAsync(coordinator, EventAirportEndpoints.Pattern, Airport(id, Second, 2), cancellationToken);
        await OkAsync(await LoadAsync(coordinator, id, table, cancellationToken), cancellationToken);
        await OkAsync(await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/publish", new EventPublishRequest(default), cancellationToken), cancellationToken);
        return id;
    }

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
    private static async Task<string> TokenAsync(HttpClient member, string audience, CancellationToken cancellationToken)
    {
        using var created = await member.PostAsJsonAsync(
            PersonalTokenEndpoints.Pattern,
            new { name = "evt-test-e6a gate", audience, days = 1 },
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

    /// <summary>The slots of an event by their callsigns, read past every filter.</summary>
    private async Task<Dictionary<string, long>> SlotIdsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Slots.AsNoTracking()
            .Where(slot => slot.EventId == eventId)
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

    /// <summary>
    /// A booking written straight into the database, as it was made at a moment the test cannot go back to — before its off block,
    /// which has passed since.
    /// </summary>
    private async Task<long> BookedDirectlyAsync(long eventId, long slotId, int vid, CancellationToken cancellationToken)
    {
        await using var held = await HeldAsync(eventId, cancellationToken);
        var id = await held.BookAsync(slotId, vid, cancellationToken);
        await held.CommitAsync(cancellationToken);
        return id;
    }

    /// <summary>
    /// A transaction of the test on the events, in the isolation the verbs use: what another request holds while it runs. Nobody is
    /// signed in on it — the installation itself —, so the write guard leaves it alone.
    /// </summary>
    private async Task<HeldTransaction> HeldAsync(long eventId, CancellationToken cancellationToken)
    {
        var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var parent = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == eventId, cancellationToken);
        var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        return new HeldTransaction(scope, database, transaction, parent);
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

    /// <summary>Notifications of a kind about one event of this class, by its title in the mail.</summary>
    private async Task<List<Notification>> NotificationsAsync(string type, string title, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == type && People.Contains(row.Vid) && row.DataJson.Contains(title))
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);
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

    /// <summary>
    /// A member with one position of the staff — or none, a member of the division and nothing else, or staff by a grant alone —,
    /// with an address or not.
    /// </summary>
    private async Task SeedUserAsync(int vid, string? position, string? email, CancellationToken cancellationToken, bool staff = false)
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
        user.LastName = "Bookings";
        user.Email = email;
        user.IsStaff = staff || position is not null;
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

    /// <summary>
    /// The events of this class with their rows and what they projected, the tokens, the mails and the grants of its people, whatever
    /// a stopped run left.
    /// </summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await hub.PersonalTokens.Where(row => People.Contains(row.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications.Where(row => People.Contains(row.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserGrants.Where(row => row.Vid != null && People.Contains(row.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// <c>EventBookings.Edit</c> granted to one member on a department — or, with a scope, on one event alone —, as a screen of the
    /// module will write it. The member signs in afterwards: the effective permissions are computed at login.
    /// </summary>
    private async Task GrantAsync(int vid, Department department, string? scope, CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();
        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = EventsPermissions.BookingsEdit,
            Department = department,
            ResourceScope = scope,
            Effect = GrantEffect.Grant,
            Reason = SlugStem,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The departments an event is in the care of, changed straight in the database: no form of the module writes the set yet, and
    /// the base department is always in it.
    /// </summary>
    private async Task CareAsync(long eventId, Department[] departments, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mask = DepartmentMask.Of(departments);
        await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters()
            .Where(row => row.Id == eventId)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.OwnerDepartmentMask, mask), cancellationToken);
    }

    /// <summary>
    /// A transaction the test holds on the events, as a request in flight would: a booking written and not committed, a lock taken.
    /// Disposed without a commit, it is rolled back.
    /// </summary>
    private sealed class HeldTransaction(
        AsyncServiceScope scope,
        EventsDbContext database,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Event parent) : IAsyncDisposable
    {
        /// <summary>A booking of <paramref name="vid"/> written in the transaction, in the event's care, as the verbs write one.</summary>
        public async Task<long> BookAsync(long slotId, int vid, CancellationToken cancellationToken)
        {
            var booking = new EventBooking
            {
                EventId = parent.Id,
                SlotId = slotId,
                BookerVid = vid,
                AircraftIcao = TypeA,
                CreatedAt = DateTime.UtcNow,
                OwnerDepartment = parent.OwnerDepartment,
                OwnerDepartmentMask = parent.OwnerDepartmentMask,
            };

            database.Bookings.Add(booking);
            await database.SaveChangesAsync(cancellationToken);
            return booking.Id;
        }

        /// <summary>A row locked in the transaction, as the lock of a pilot is taken.</summary>
        public Task LockAsync(FormattableString sql, CancellationToken cancellationToken) =>
            database.Database.SqlQuery<long>(sql).ToListAsync(cancellationToken);

        /// <summary>A row written in the transaction behind every service, as another write in flight holds it.</summary>
        public Task<int> ExecuteAsync(FormattableString sql, CancellationToken cancellationToken) =>
            database.Database.ExecuteSqlAsync(sql, cancellationToken);

        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }
}
