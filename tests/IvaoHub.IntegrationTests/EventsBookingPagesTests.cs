using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using IvaoHub.Modules.Events.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Quartz;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The pages of the bookings (M4, E6b), through the real host with the division file of this repository: the reminder of the day
/// before — once per booking, the near flights of a pilot in one mail with the routes of the flight operations, never again on a
/// second run nor from two runs at once —; the block <c>events.myEvents</c>, which tells a visitor to sign in and a pilot their flights
/// still to fly; the staff's list of the bookings of an event, by the time of their flights, with the pilots named the way the core
/// names a person; and the address <c>mine</c>, which no event takes.
/// <para>⚠️ The coordinator is seeded **without an address**, as in <see cref="EventsStaffTests"/>: the tests of the contacts assert who
/// of the events receives a message. The two pilots have one each: they are told by mail. The job of the reminders is paused in the
/// host's scheduler, so the only runs are the test's.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsBookingPagesTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // E6b's in the range the events module owns in the shared database (CONTRIBUTING.md): 761063–761066, left by E5.
    private const int CoordinatorVid = 761063;
    private const int PilotVid = 761064;
    private const int OtherPilotVid = 761065;
    private const int MemberVid = 761066;

    /// <summary>Two airports of the events of this class, and one away from them: of no country the network has.</summary>
    private const string First = "XEG1";
    private const string Second = "XEG2";
    private const string Away = "XEG3";

    /// <summary>Two aircraft types the core knows here.</summary>
    private const string TypeA = "XE7A";
    private const string TypeB = "XE7B";

    private const string SlugStem = "evt-test-e6b";

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
        await SeedUserAsync(PilotVid, position: null, "e6b-pilot@example.org", token);
        await SeedUserAsync(OtherPilotVid, position: null, "e6b-other@example.org", token);
        await SeedUserAsync(MemberVid, position: null, email: null, token);
        await SeedReferenceAsync(token);
        await ForgetAsync(token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows, its mails, its airports and types, its positions.</summary>
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
    public async Task TheReminderGoesOnceToEachPilotWithTheirNearFlightsInOneMailAndTheRoutes()
    {
        var token = TestContext.Current.CancellationToken;
        var lead = await LeadAsync(token);
        var now = Minute(DateTime.UtcNow);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // A rotation out of the first airport and back — its first leg due, its second leaving half an hour after the window, near
        // enough to go with it —, a flight of the same pilot a day later, and another pilot's flight, due too.
        var first = now + lead - TimeSpan.FromHours(2);
        var id = await PublishedAsync(
            coordinator,
            "remind",
            Table(
                Line("XEH101", "XH101", TypeA, First, first, Away, first.AddHours(1), "C3", rotation: "R1", leg: "1"),
                Line("XEH102", string.Empty, TypeA, Away, first.AddHours(2).AddMinutes(30), First, first.AddHours(3).AddMinutes(30), rotation: "R1", leg: "2"),
                Line("XEH103", string.Empty, TypeA, First, now + lead + lead + TimeSpan.FromHours(1), Second, now + lead + lead + TimeSpan.FromHours(2)),
                Line("XEH201", string.Empty, TypeB, First, first.AddMinutes(30), Second, first.AddHours(2))),
            token,
            starts: first.AddHours(-1),
            ends: now + lead + lead);
        var slots = await SlotIdsAsync(id, token);

        // The flight operations wrote two routes for the first leg, one with remarks in both languages, and one for no flight of these.
        await CreatedJsonAsync(coordinator, EventRouteEndpoints.Pattern, Route(id, First, Away, "DCT XEHAA DCT", "evt-test-e6b by the coast"), token);
        await CreatedJsonAsync(coordinator, EventRouteEndpoints.Pattern, Route(id, First, Away, "DCT XEHBB DCT", remarks: null), token);
        await CreatedJsonAsync(coordinator, EventRouteEndpoints.Pattern, Route(id, Second, First, "DCT XEHCC DCT", remarks: null), token);

        using var pilot = await SignedInAsync(PilotVid, token);
        using var other = await SignedInAsync(OtherPilotVid, token);
        var rotation = await OkAsync(await pilot.PostAsJsonAsync($"{BookingEndpoints.MinePattern}/rotation", new BookingRequest(slots["XEH101"], TypeA), token), token);
        Assert.Equal(2, rotation.GetProperty("booked").GetArrayLength());
        await CreatedAsync(await pilot.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(slots["XEH103"], TypeA), token), token);
        await CreatedAsync(await other.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(slots["XEH201"], TypeB), token), token);

        await RunAsync(token);

        // One mail to the pilot: the two legs of the evening, the routes of the first with its remarks, and not the flight a day later.
        var told = Assert.Single(await RemindersAsync(PilotVid, token));
        var data = JsonDocument.Parse(told.DataJson).RootElement;
        var flights = data.GetProperty("flights").GetString()!;
        Assert.Contains("XEH101 (XH101), XE7A: XEG1", flights, StringComparison.Ordinal);
        Assert.Contains(", stand C3", flights, StringComparison.Ordinal);
        Assert.Contains("XEH102, XE7A: XEG3", flights, StringComparison.Ordinal);
        Assert.Contains("DCT XEHAA DCT (evt-test-e6b by the coast)", flights, StringComparison.Ordinal);
        Assert.Contains("DCT XEHBB DCT", flights, StringComparison.Ordinal);
        Assert.DoesNotContain("XEHCC", flights, StringComparison.Ordinal);
        Assert.DoesNotContain("XEH103", flights, StringComparison.Ordinal);
        Assert.Equal("2", data.GetProperty("count").GetString());
        Assert.Equal(Slug("remind"), data.GetProperty("title").GetString());
        Assert.EndsWith("/events/mine", data.GetProperty("mine").GetString(), StringComparison.Ordinal);

        // One to the other pilot, with their flight alone.
        var theirs = Assert.Single(await RemindersAsync(OtherPilotVid, token));
        Assert.Contains("XEH201", theirs.DataJson, StringComparison.Ordinal);
        Assert.DoesNotContain("XEH101", theirs.DataJson, StringComparison.Ordinal);

        // Marked once each, the flight a day later not yet.
        var stored = (await BookingsAsync(id, token)).ToDictionary(booking => booking.SlotId);
        Assert.NotNull(stored[slots["XEH101"]].RemindedAt);
        Assert.NotNull(stored[slots["XEH102"]].RemindedAt);
        Assert.NotNull(stored[slots["XEH201"]].RemindedAt);
        Assert.Null(stored[slots["XEH103"]].RemindedAt);

        // The mark is the job's bookkeeping: no row in the audit for it.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var keys = stored.Values.Select(booking => booking.Id.ToString(CultureInfo.InvariantCulture)).ToList();
            Assert.False(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
                .AnyAsync(entry => entry.Entity == "evt_bookings" && keys.Contains(entry.EntityId) && entry.Action == "updated", token));
        }

        // A second run — late, or twice — sends nothing again.
        await RunAsync(token);
        Assert.Single(await RemindersAsync(PilotVid, token));
        Assert.Single(await RemindersAsync(OtherPilotVid, token));
    }

    [Fact]
    public async Task ACancelledEventAndAFlightGoneAreRemindedNoMore()
    {
        var token = TestContext.Current.CancellationToken;
        var lead = await LeadAsync(token);
        var now = Minute(DateTime.UtcNow);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        // A flight due, in an event that is then cancelled: the booking stays, and nobody is reminded of an event that will not be.
        var due = now + lead - TimeSpan.FromHours(1);
        var cancelled = await PublishedAsync(
            coordinator,
            "cancelled",
            Table(Line("XEH301", string.Empty, TypeA, First, due, Away, due.AddHours(1))),
            token,
            starts: due.AddHours(-1),
            ends: due.AddHours(3));
        await CreatedAsync(await pilot.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest((await SlotIdsAsync(cancelled, token))["XEH301"], TypeA), token), token);
        var stored = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{cancelled}", token), token);
        await OkAsync(
            await coordinator.PostAsJsonAsync(
                $"{EventEndpoints.Pattern}/{cancelled}/cancel",
                new EventCancelRequest(Text("evt-test-e6b the storm"), stored.GetProperty("rowVersion").GetDateTime()),
                token),
            token);

        // A booking whose off block passed while the hub slept: the flight is gone, and it is reminded no more.
        var running = await PublishedAsync(
            coordinator,
            "gone",
            Table(Line("XEH302", string.Empty, TypeA, First, now.AddHours(2), Away, now.AddHours(3))),
            token,
            starts: now.AddHours(-1),
            ends: now.AddHours(4),
            opens: now.AddDays(-2));
        var gone = await PublishedSlotAsync(running, "XEH302", token);
        await MoveSlotAsync(gone, now.AddMinutes(-30), now.AddMinutes(30), token);
        await BookedDirectlyAsync(running, gone, PilotVid, token);

        await RunAsync(token);

        Assert.Empty(await RemindersAsync(PilotVid, token));
        Assert.All(await BookingsAsync(cancelled, token), booking => Assert.Null(booking.RemindedAt));
        Assert.All(await BookingsAsync(running, token), booking => Assert.Null(booking.RemindedAt));
    }

    [Fact]
    public async Task TwoRunsAtOnceSendTheReminderOnce()
    {
        var token = TestContext.Current.CancellationToken;
        var lead = await LeadAsync(token);
        var now = Minute(DateTime.UtcNow);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var due = now + lead - TimeSpan.FromHours(1);
        var id = await PublishedAsync(
            coordinator,
            "race",
            Table(Line("XEH401", string.Empty, TypeA, First, due, Away, due.AddHours(1))),
            token,
            starts: due.AddHours(-1),
            ends: due.AddHours(3));
        var booking = Id(await CreatedAsync(await pilot.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest((await SlotIdsAsync(id, token))["XEH401"], TypeA), token), token));

        // A transaction of the test holds what another run would hold: the pilot's booking locked, and marked, not yet committed.
        await using (var held = _factory.Services.CreateAsyncScope())
        {
            var database = held.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            await database.Database
                .SqlQuery<long>($"SELECT id AS `Value` FROM evt_bookings WHERE id = {booking} FOR UPDATE")
                .ToListAsync(token);
            var row = await database.Bookings.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == booking, token);
            row.RemindedAt = now;
            await database.SaveChangesAsync(token);

            // The run under test finds the booking due — the mark is not committed —, and waits for the lock in the database.
            var run = RunAsync(token);
            await WaitForALockWaitAsync(token);
            await transaction.CommitAsync(token);
            await run;
        }

        // Read again under the lock, the booking was reminded by the other run: nothing more is sent.
        Assert.Empty(await RemindersAsync(PilotVid, token));
        Assert.Equal(now, Assert.Single(await BookingsAsync(id, token)).RemindedAt);
    }

    [Fact]
    public async Task TheBlockTellsAVisitorToSignInAndAPilotTheirFlightsStillToFly()
    {
        var token = TestContext.Current.CancellationToken;
        var now = Minute(DateTime.UtcNow);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // An event twenty days away with a flight booked, and one in progress whose booked flight has landed.
        var day = Day(days: 20);
        var later = await PublishedAsync(coordinator, "block", Table(Line("XEH501", string.Empty, TypeA, First, day.AddHours(18), Away, day.AddHours(19))), token);
        var running = await PublishedAsync(
            coordinator,
            "landed",
            Table(Line("XEH502", string.Empty, TypeA, First, now.AddHours(2), Away, now.AddHours(3))),
            token,
            starts: now.AddHours(-3),
            ends: now.AddHours(4),
            opens: now.AddDays(-2));

        using var pilot = await SignedInAsync(PilotVid, token);
        await CreatedAsync(await pilot.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest((await SlotIdsAsync(later, token))["XEH501"], TypeA), token), token);
        var landed = await PublishedSlotAsync(running, "XEH502", token);
        await MoveSlotAsync(landed, now.AddHours(-2), now.AddHours(-1), token);
        await BookedDirectlyAsync(running, landed, PilotVid, token);

        // A visitor: told to sign in, and nothing else.
        using (var anonymous = _factory.CreateApiClient())
        {
            var block = await OkAsync(await anonymous.GetAsync($"/api/blocks/data/{MyEventsProvider.BlockType}", token), token);
            Assert.False(block.GetProperty("signedIn").GetBoolean());
            Assert.False(block.TryGetProperty("bookings", out _));
        }

        // The pilot: the flight to come, not the one landed, with what /events/mine reads of it.
        var mine = await OkAsync(await pilot.GetAsync($"/api/blocks/data/{MyEventsProvider.BlockType}", token), token);
        Assert.True(mine.GetProperty("signedIn").GetBoolean());
        var flight = Assert.Single(
            mine.GetProperty("bookings").EnumerateArray(),
            row => row.GetProperty("eventSlug").GetString()!.StartsWith(SlugStem, StringComparison.Ordinal));
        Assert.Equal(("XEH501", TypeA, Slug("block")), (flight.GetProperty("callsign").GetString(), flight.GetProperty("aircraftIcao").GetString(), flight.GetProperty("eventSlug").GetString()));
        Assert.Equal(nameof(SlotKind.Public), flight.GetProperty("kind").GetString());

        // The landed one is still the pilot's, on their page.
        Assert.Contains(
            (await OkAsync(await pilot.GetAsync(BookingEndpoints.MinePattern, token), token)).EnumerateArray(),
            row => row.GetProperty("callsign").GetString() == "XEH502");

        // A member with no booking: signed in, and nothing to fly.
        using var member = await SignedInAsync(MemberVid, token);
        var none = await OkAsync(await member.GetAsync($"/api/blocks/data/{MyEventsProvider.BlockType}", token), token);
        Assert.True(none.GetProperty("signedIn").GetBoolean());
        Assert.Empty(none.GetProperty("bookings").EnumerateArray());
    }

    [Fact]
    public async Task TheStaffListTheBookingsOfAnEventByTheTimeOfTheirFlightsWithThePilots()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);

        // Three flights booked in an order that is not their time's, a flight of another event, and a slot nobody booked.
        var id = await PublishedAsync(
            coordinator,
            "list",
            Table(
                Line("XEH601", string.Empty, TypeA, First, day.AddHours(19), Away, day.AddHours(20)),
                Line("XEH602", string.Empty, TypeA, Away, day.AddHours(16).AddMinutes(30), First, day.AddHours(17).AddMinutes(30)),
                Line("XEH603", string.Empty, $"{TypeA}/{TypeB}", First, day.AddHours(18), Second, day.AddHours(19), "D4"),
                Line("XEH604", string.Empty, TypeA, First, day.AddHours(21), Away, day.AddHours(22))),
            token);
        var another = await PublishedAsync(coordinator, "another", Table(Line("XEH611", string.Empty, TypeA, First, day.AddHours(18), Away, day.AddHours(19))), token);
        var slots = await SlotIdsAsync(id, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        using var other = await SignedInAsync(OtherPilotVid, token);
        await CreatedAsync(await pilot.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(slots["XEH601"], TypeA), token), token);
        await CreatedAsync(await other.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(slots["XEH603"], TypeB), token), token);
        await CreatedAsync(await pilot.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(slots["XEH602"], TypeA), token), token);
        await CreatedAsync(await pilot.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest((await SlotIdsAsync(another, token))["XEH611"], TypeA), token), token);

        // By the time at the airport of the event: the arrival at 17:30, the departures at 18:00 and 19:00; never the free slot, never
        // another event's.
        var page = await OkAsync(await coordinator.GetAsync(ListUri(id), token), token);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.Equal(["XEH602", "XEH603", "XEH601"], items.Select(row => row.GetProperty("callsign").GetString()));

        // The pilot as the core names a person, the flight of the slot, the aircraft chosen, the reminder not sent yet.
        var withStand = items[1];
        Assert.Equal((OtherPilotVid, "Test Pages"), (withStand.GetProperty("pilot").GetProperty("vid").GetInt32(), withStand.GetProperty("pilot").GetProperty("name").GetString()));
        Assert.Equal((TypeB, First, Second, "D4"), (
            withStand.GetProperty("aircraftIcao").GetString(),
            withStand.GetProperty("departureIcao").GetString(),
            withStand.GetProperty("arrivalIcao").GetString(),
            withStand.GetProperty("stand").GetString()));
        Assert.Equal(JsonValueKind.Null, withStand.GetProperty("remindedAt").ValueKind);

        // A VID finds a pilot's; the newest first when asked.
        var found = await OkAsync(await coordinator.GetAsync($"{ListUri(id)}&q={OtherPilotVid}", token), token);
        Assert.Equal(["XEH603"], found.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("callsign").GetString()));
        var newest = await OkAsync(await coordinator.GetAsync($"{ListUri(id)}&sort=createdAt&dir=desc", token), token);
        Assert.Equal("XEH602", newest.GetProperty("items")[0].GetProperty("callsign").GetString());

        // A person whose data was erased is a pseudonym with no name: the page says so with the core's word.
        var erased = (await BookingsAsync(id, token)).Single(booking => booking.SlotId == slots["XEH601"]);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Bookings.IgnoreQueryFilters()
                .Where(booking => booking.Id == erased.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(booking => booking.BookerVid, -761064), token);
        }

        var afterErasure = await OkAsync(await coordinator.GetAsync(ListUri(id), token), token);
        var pseudonym = afterErasure.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("callsign").GetString() == "XEH601");
        Assert.Equal(-761064, pseudonym.GetProperty("pilot").GetProperty("vid").GetInt32());
        Assert.Equal(JsonValueKind.Null, pseudonym.GetProperty("pilot").GetProperty("name").ValueKind);

        // A member reads nobody's bookings here, and a visitor nothing.
        using var member = await SignedInAsync(MemberVid, token);
        using (var refused = await member.GetAsync(ListUri(id), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using var anonymous = _factory.CreateApiClient();
        using var nobody = await anonymous.GetAsync(ListUri(id), token);
        Assert.Equal(HttpStatusCode.Unauthorized, nobody.StatusCode);
    }

    [Fact]
    public async Task NoEventTakesTheAddressOfAMembersBookings()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        foreach (var slug in new[] { "mine", " MINE " })
        {
            var refused = await RefusedAsync(await coordinator.PostAsJsonAsync(EventEndpoints.Pattern, Payload(SlugStem, Day(days: 20).AddHours(17), DateTime.UtcNow.AddDays(-1)) with { Slug = slug }, token), token);
            Assert.Equal(["events:errors.slugReserved"], refused["slug"]);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>Midnight of a day some days from today, in UTC: the slots are at hours of it.</summary>
    private static DateTime Day(int days) => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days), DateTimeKind.Utc);

    /// <summary>An instant to the minute, as a table writes it: what is loaded is what is compared.</summary>
    private static DateTime Minute(DateTime at) => new(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, DateTimeKind.Utc);

    private static Uri ListUri(long eventId) =>
        new($"{BookingEndpoints.StaffPattern}?filter[eventId]={eventId}&pageSize=100", UriKind.Relative);

    /// <summary>An RFO with public slots, called after its address in both languages of the division.</summary>
    private static EventWriteDto Payload(string name, DateTime starts, DateTime opens, DateTime? ends = null) => new(
        Kind: "rfo",
        PublicSlots: true,
        PrivateSlots: false,
        WholeDivision: false,
        Organizer: EventOrganizer.Division,
        ExternalUrl: null,
        Title: Text(name),
        Slug: name,
        Summary: Text(name),
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

    private static EventRouteWriteDto Route(long eventId, string departure, string arrival, string route, string? remarks) =>
        new(eventId, departure, arrival, route, remarks is null ? null : Text(remarks), RowVersion: default);

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

    /// <summary>
    /// An RFO with public slots and its two airports, its slots loaded from the table and published: by default from 17:00 to 22:00 of
    /// the slots' day, its bookings open since yesterday.
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
        var start = starts ?? Day(days: 20).AddHours(17);
        var payload = Payload(Slug(name), start, opens ?? DateTime.UtcNow.AddDays(-1), ends ?? start.AddHours(5));
        var id = Id(await CreatedJsonAsync(coordinator, EventEndpoints.Pattern, payload, cancellationToken));
        await CreatedJsonAsync(coordinator, EventAirportEndpoints.Pattern, Airport(id, First, 1), cancellationToken);
        await CreatedJsonAsync(coordinator, EventAirportEndpoints.Pattern, Airport(id, Second, 2), cancellationToken);
        await OkAsync(
            await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/slots/load", new SlotLoadRequest(table, SlotLoadMode.Add), cancellationToken),
            cancellationToken);
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

    /// <summary>The hours of the reminder as the division's settings say them: the test's times are counted from it.</summary>
    private async Task<TimeSpan> LeadAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var settings = await scope.ServiceProvider.GetRequiredService<ModuleSettingsStore>()
            .GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        Assert.True(settings.ReminderLeadHours >= 6, "The test counts its flights from a reminder of six hours at least.");
        return TimeSpan.FromHours(settings.ReminderLeadHours);
    }

    /// <summary>One run of the reminders, as the scheduler would start it.</summary>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BookingRemindersJob>().RunAsync(cancellationToken);

        var last = await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
            .Where(run => run.Job == BookingRemindersJob.JobName)
            .OrderByDescending(run => run.Id)
            .FirstAsync(cancellationToken);
        Assert.True(last.Status == "succeeded", last.Message);
    }

    /// <summary>The reminders a member of this class was sent, about an event of this class.</summary>
    private async Task<List<Notification>> RemindersAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == EventsNotifications.BookingReminder && row.Vid == vid && row.DataJson.Contains(SlugStem))
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The slots of an event by their callsigns, read past every filter.</summary>
    private async Task<Dictionary<string, long>> SlotIdsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Slots.AsNoTracking()
            .Where(slot => slot.EventId == eventId)
            .ToDictionaryAsync(slot => slot.Callsign!, slot => slot.Id, cancellationToken);
    }

    private async Task<long> PublishedSlotAsync(long eventId, string callsign, CancellationToken cancellationToken) =>
        (await SlotIdsAsync(eventId, cancellationToken))[callsign];

    /// <summary>
    /// A slot's flight moved straight in the database, to a moment a table cannot write any more: the past, which the test cannot go
    /// back to.
    /// </summary>
    private async Task MoveSlotAsync(long slotId, DateTime offBlock, DateTime onBlock, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Slots
            .Where(slot => slot.Id == slotId)
            .ExecuteUpdateAsync(set => set.SetProperty(slot => slot.OffBlockUtc, offBlock).SetProperty(slot => slot.OnBlockUtc, onBlock), cancellationToken);
    }

    /// <summary>A booking written straight into the database, as it was made before its off block, which has passed since.</summary>
    private async Task BookedDirectlyAsync(long eventId, long slotId, int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var parent = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == eventId, cancellationToken);
        database.Bookings.Add(new EventBooking
        {
            EventId = eventId,
            SlotId = slotId,
            BookerVid = vid,
            AircraftIcao = TypeA,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            OwnerDepartment = parent.OwnerDepartment,
            OwnerDepartmentMask = parent.OwnerDepartmentMask,
        });
        await database.SaveChangesAsync(cancellationToken);
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

    /// <summary>Until a transaction waits for a lock: the run under test, behind the transaction the test holds.</summary>
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

        Assert.Fail("The run never waited for the transaction the test holds.");
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
        user.LastName = "Pages";
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

    /// <summary>The events of this class with their rows and what they projected, the mails of its people, whatever a stopped run left.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await hub.Notifications.Where(row => People.Contains(row.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.JobsLog.Where(run => run.Job == BookingRemindersJob.JobName).ExecuteDeleteAsync(cancellationToken);
    }
}
