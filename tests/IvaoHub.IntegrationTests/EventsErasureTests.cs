using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Erasing a person's data in the events (M4, E8b, design §11.1; note 2026-10-11-duplica-e-la-cancellazione-negli-eventi §2), through the
/// real host and the core's erasure, as the superadmin asks it: the bookings of events still to end go — their slots are free again on
/// the page —, a pair with them, and a cancelled event's too until its end; the bookings of events that ended stay, under the pseudonym,
/// without the flight the pilot wrote, the note about it and the session of the network; what the person did as staff stays under the
/// pseudonym, with their words; nothing of the private flight is left in the audit log; and an erasure run again finds nothing.
/// <para>Every row is written by the installation: the bookings of an event that ended could not be made any other way. The other pilot
/// and the staff member who excused a flight are a VID in a row — nobody signs in as them.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsErasureTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The two VIDs of the events module's range the merged phases left unused (CONTRIBUTING.md): the person erased — whom no other
    // class's rows name — and the superadmin who erases them.
    private const int PersonVid = 761098;
    private const int SuperadminVid = 761015;

    // Another pilot, and a member of the staff: E6b's coordinator (EventsBookingPagesTests), reused as a VID in a row and nothing else.
    private const int OtherVid = 761063;

    /// <summary>The airport of the events of this class, and one away from it: of no country the network has.</summary>
    private const string First = "XEJ1";
    private const string Away = "XEJ3";

    private const string Type = "XEJA";

    private const string SlugStem = "evt-test-e8b-erase";

    /// <summary>The note of the staff about the person's flight not flown, which goes; and the person's own note as staff, which stays.</summary>
    private const string NoteAboutThem = "evt-test-e8b network failure";
    private const string TheirWords = "evt-test-e8b their words";

    /// <summary>The note on a booking withdrawn before the erasure, which its copies in the audit log lose.</summary>
    private const string WithdrawnNote = "evt-test-e8b withdrawn before";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly string[] Airports = [First, Away];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(PersonVid, superadmin: false, token);
        await SeedUserAsync(SuperadminVid, superadmin: true, token);
        await SeedReferenceAsync(token);
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows, the superadmin's role, its airports and its type.</summary>
    public async ValueTask DisposeAsync()
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.Users.Where(user => user.Vid == SuperadminVid).ExecuteUpdateAsync(set => set.SetProperty(user => user.IsSuperadmin, false));
            await database.IvaoAirports.Where(airport => Airports.Contains(airport.Icao)).ExecuteDeleteAsync();
            await database.IvaoAircraftTypes.Where(type => type.IcaoCode == Type).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AnErasedPilotLeavesTheEventsThatEndedTheirHistoryAndTheEventsToComeTheirSlots()
    {
        var token = TestContext.Current.CancellationToken;
        var (soon, past) = (Day(days: 10), Day(days: -10));

        // To come: a public slot of theirs and one of another pilot's, and their private arrival with its linked departure.
        var toCome = await EventAsync("to-come", soon, cancelledBy: null, token);
        var theirs = await PublicSlotAsync(toCome, "XEJ801", soon.AddHours(17).AddMinutes(30), token);
        var others = await PublicSlotAsync(toCome, "XEJ802", soon.AddHours(18), token);
        var landing = await PrivateSlotAsync(toCome, arrival: true, soon.AddHours(18), token);
        var leaving = await PrivateSlotAsync(toCome, arrival: false, soon.AddHours(19), token);
        var theirsBooked = await BookingAsync(toCome, theirs, PersonVid, token);
        await BookingAsync(toCome, others, OtherVid, token);
        var departure = await BookingAsync(toCome, leaving, PersonVid, token, row => (row.Callsign, row.OtherIcao, row.OtherTimeUtc) = ("XEJ804", Away, soon.AddHours(20)));
        var arrival = await BookingAsync(toCome, landing, PersonVid, token, row => (row.Callsign, row.OtherIcao, row.OtherTimeUtc, row.PairedBookingId) = ("XEJ803", Away, soon.AddHours(17), departure));

        // Ended: a public slot they flew, found in a session of the network; their private arrival not flown, which the staff excused
        // with a note; another pilot's not flown, which they excused as staff, in their own words.
        var ended = await EventAsync("ended", past, cancelledBy: null, token);
        var flownAt = past.AddHours(18);
        var checkedAt = past.AddHours(21);
        var flown = await BookingAsync(ended, await PublicSlotAsync(ended, "XEJ811", past.AddHours(17).AddMinutes(30), token), PersonVid, token, row => (row.FlownAt, row.FlownSessionId, row.FlownCheckedAt) = (flownAt, 987654, checkedAt));
        var excused = await BookingAsync(
            ended,
            await PrivateSlotAsync(ended, arrival: true, past.AddHours(18), token),
            PersonVid,
            token,
            row => (row.Callsign, row.OtherIcao, row.OtherTimeUtc, row.FlownCheckedAt, row.UnflownExcusedBy, row.UnflownExcusedNote) = ("XEJ812", Away, past.AddHours(17), checkedAt, OtherVid, NoteAboutThem));
        var excusedByThem = await BookingAsync(
            ended,
            await PublicSlotAsync(ended, "XEJ813", past.AddHours(18), token),
            OtherVid,
            token,
            row => (row.FlownCheckedAt, row.UnflownExcusedBy, row.UnflownExcusedNote) = (checkedAt, PersonVid, TheirWords));

        // Cancelled by them as staff and still to end, with a booking of theirs; and cancelled and ended, with another.
        var cancelled = await EventAsync("cancelled", soon.AddDays(2), cancelledBy: PersonVid, token);
        var ofCancelled = await BookingAsync(cancelled, await PublicSlotAsync(cancelled, "XEJ821", soon.AddDays(2).AddHours(18), token), PersonVid, token);
        var cancelledEnded = await EventAsync("cancelled-ended", past.AddDays(-2), cancelledBy: OtherVid, token);
        var keptOfCancelled = await BookingAsync(cancelledEnded, await PublicSlotAsync(cancelledEnded, "XEJ831", past.AddDays(-2).AddHours(18), token), PersonVid, token);

        Assert.True(
            await AuditMentionsAsync("XEJ812", token) && await AuditMentionsAsync("XEJ804", token),
            "Without the private flights in the audit log, the check below would prove nothing.");

        // What the superadmin reads before confirming, and what the erasure did: the same lines.
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        (string Key, int Count, string Outcome)[] lines =
        [
            ("events:erasure.bookingsToCome", 4, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.bookingsOver", 3, nameof(ErasureOutcome.Anonymised)),
            ("events:erasure.registrationsToCome", 0, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.registrationsOver", 0, nameof(ErasureOutcome.Anonymised)),
            ("events:erasure.availabilitiesToCome", 0, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.availabilitiesOver", 0, nameof(ErasureOutcome.Anonymised)),
            ("events:erasure.shiftsToCome", 0, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.shiftsOver", 0, nameof(ErasureOutcome.Anonymised)),
            ("events:erasure.transfersToCome", 0, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.transfersOver", 0, nameof(ErasureOutcome.Anonymised)),
        ];
        Assert.Equal(lines, EventsLines(await superadmin.GetFromJsonAsync<JsonElement>(new Uri($"{ErasureEndpoints.Pattern}/{PersonVid}", UriKind.Relative), token)));

        var erased = await EraseAsync(superadmin, PersonVid, token);
        var pseudonym = erased.GetProperty("pseudonym").GetInt32();
        Assert.Equal(lines, EventsLines(erased));

        // To come: their three bookings gone, the other pilot's there; on the page their slots are free again.
        Assert.Equal([(others, OtherVid)], (await BookingsAsync(toCome, token)).Select(row => (row.SlotId, row.BookerVid)));
        using (var visitor = _factory.CreateApiClient())
        {
            var page = await visitor.GetFromJsonAsync<JsonElement>($"/api/events/public/{Slug("to-come")}", token);
            var taken = page.GetProperty("slots").EnumerateArray()
                .Concat(page.GetProperty("privateSlots").EnumerateArray())
                .ToDictionary(slot => slot.GetProperty("id").GetInt64(), slot => slot.GetProperty("taken").GetBoolean());
            Assert.Equal((false, true, false, false), (taken[theirs], taken[others], taken[landing], taken[leaving]));
        }

        // Ended: their bookings stay under the pseudonym, without the flight they wrote, the note about it and the session of the
        // network; whether and when it was flown and checked stays. What they did as staff stays, in their words.
        var stored = (await BookingsAsync(ended, token)).ToDictionary(row => row.Id);
        Assert.Equal((pseudonym, (long?)null, (DateTime?)flownAt, (DateTime?)checkedAt), (stored[flown].BookerVid, stored[flown].FlownSessionId, stored[flown].FlownAt, stored[flown].FlownCheckedAt));
        Assert.Equal(
            (pseudonym, (string?)null, (string?)null, (DateTime?)null, (string?)null, (int?)OtherVid, Type),
            (stored[excused].BookerVid, stored[excused].Callsign, stored[excused].OtherIcao, stored[excused].OtherTimeUtc, stored[excused].UnflownExcusedNote, stored[excused].UnflownExcusedBy, stored[excused].AircraftIcao));
        Assert.Equal((OtherVid, (int?)pseudonym, TheirWords), (stored[excusedByThem].BookerVid, stored[excusedByThem].UnflownExcusedBy, stored[excusedByThem].UnflownExcusedNote));

        // A cancelled event still to end loses their booking, and keeps who cancelled it — under the pseudonym; one cancelled and ended
        // keeps theirs, under the pseudonym too.
        Assert.Empty(await BookingsAsync(cancelled, token));
        Assert.Equal((int?)pseudonym, (await EventRowAsync(cancelled, token)).CancelledBy);
        Assert.Equal(pseudonym, Assert.Single(await BookingsAsync(cancelledEnded, token), row => row.Id == keptOfCancelled).BookerVid);

        // Nothing of the private flights in the audit log: of the bookings that went — the arrival and its linked departure —, and of the
        // ones that stayed.
        Assert.False(await AuditMentionsAsync("XEJ803", token));
        Assert.False(await AuditMentionsAsync("XEJ804", token));
        Assert.False(await AuditMentionsAsync("XEJ812", token));
        Assert.False(await AuditMentionsAsync(NoteAboutThem, token));

        // Each booking that went keeps its row in the audit log, «erased», and nothing of it (the review of #249, point 5).
        long[] went = [theirsBooked, arrival, departure, ofCancelled];
        Assert.Equal(
            went.Select(id => (id.ToString(CultureInfo.InvariantCulture), (string?)null, (string?)null)).Order(),
            (await ErasedRowsAsync(went, token)).Order());

        // Run again: nothing left about the VID.
        Assert.All(EventsLines(await EraseAsync(superadmin, PersonVid, token)), line => Assert.Equal(0, line.Count));
    }

    [Fact]
    public async Task APrivateBookingWithdrawnBeforeTheErasureLeavesNothingOfItsFlightInTheAuditLog()
    {
        var token = TestContext.Current.CancellationToken;
        var soon = Day(days: 12);

        // Their private arrival, with all five columns of theirs written, withdrawn a week before the erasure: no row any more,
        // only its two copies in the audit log, which no eraser finds (E10n, Carmine's answer 2 on #249).
        var withdrawnEvent = await EventAsync("withdrawn", soon, cancelledBy: null, token);
        var landing = await PrivateSlotAsync(withdrawnEvent, arrival: true, soon.AddHours(18), token);
        var withdrawn = await BookingAsync(
            withdrawnEvent,
            landing,
            PersonVid,
            token,
            row => (row.Callsign, row.OtherIcao, row.OtherTimeUtc, row.UnflownExcusedNote, row.FlownSessionId) =
                ("XEJ841", Away, soon.AddHours(17), WithdrawnNote, 765432));
        await WithdrawAsync(withdrawn, token);
        Assert.True(
            await AuditMentionsAsync("XEJ841", token) && await AuditMentionsAsync(WithdrawnNote, token),
            "Without the flight in the audit log, the check below would prove nothing.");

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var pseudonym = (await EraseAsync(superadmin, PersonVid, token)).GetProperty("pseudonym").GetInt32();

        // Its two copies stay, under the pseudonym, with the aircraft and the slot; the five columns of the pilot's are empty. The
        // keys of the audit's JSON are the properties' names.
        var copies = await AuditRowsAsync(withdrawn, token);
        Assert.Equal(["created", "deleted"], copies.Select(row => row.Action));
        foreach (var copy in new[] { JsonNode.Parse(copies[0].AfterJson!)!.AsObject(), JsonNode.Parse(copies[1].BeforeJson!)!.AsObject() })
        {
            Assert.Equal((pseudonym, Type, landing), (copy["BookerVid"]!.GetValue<int>(), copy["AircraftIcao"]!.GetValue<string>(), copy["SlotId"]!.GetValue<long>()));
            foreach (var column in (string[])["Callsign", "OtherIcao", "OtherTimeUtc", "UnflownExcusedNote", "FlownSessionId"])
            {
                Assert.True(copy.ContainsKey(column) && copy[column] is null, $"{column} is still in the audit: {copy[column]}");
            }
        }

        Assert.False(await AuditMentionsAsync("XEJ841", token) || await AuditMentionsAsync(WithdrawnNote, token));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>Midnight of a day some days from today, in UTC: the events are at hours of it.</summary>
    private static DateTime Day(int days) => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days), DateTimeKind.Utc);

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>The events' lines of an erasure's preview or result: what, how many, and what becomes of them.</summary>
    private static (string Key, int Count, string Outcome)[] EventsLines(JsonElement answer) =>
    [
        .. answer.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("key").GetString()!.StartsWith($"{EventsModule.ModuleKey}:", StringComparison.Ordinal))
            .Select(line => (line.GetProperty("key").GetString()!, line.GetProperty("count").GetInt32(), line.GetProperty("outcome").GetString()!)),
    ];

    /// <summary>The erasure of a VID, asked by the superadmin; the answer, with the pseudonym and the lines.</summary>
    private static async Task<JsonElement> EraseAsync(HttpClient superadmin, int vid, CancellationToken cancellationToken)
    {
        using var response = await superadmin.PostAsync(new Uri($"{ErasureEndpoints.Pattern}/{vid}", UriKind.Relative), content: null, cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>
    /// A published RFO of the class from 17:00 to 20:00 of a day, with one airport, written by the installation; cancelled by
    /// <paramref name="cancelledBy"/> when one is given.
    /// </summary>
    private async Task<long> EventAsync(string name, DateTime day, int? cancelledBy, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfo",
            PublicSlots = true,
            PrivateSlots = true,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            BookingOpensAtUtc = day.AddDays(-7),
            StartsAtUtc = day.AddHours(17),
            EndsAtUtc = day.AddHours(20),
            Status = PublishStatus.Published,
            PublishedAt = day.AddDays(-10),
            Visibility = Visibility.Public,
            CancelledAt = cancelledBy is null ? null : day.AddDays(-1),
            CancelledBy = cancelledBy,
            CancellationNote = cancelledBy is null ? null : Text("evt-test-e8b cancelled"),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);

        database.Airports.Add(new EventAirport { EventId = row.Id, Icao = First, Ordinal = 1, MaxArrivalsPerHour = 2, MaxDeparturesPerHour = 2, OwnerDepartment = row.OwnerDepartment, OwnerDepartmentMask = row.OwnerDepartmentMask });
        await database.SaveChangesAsync(cancellationToken);

        return row.Id;
    }

    /// <summary>A public slot leaving the airport of the event for the one away, an hour long, written by the installation.</summary>
    private Task<long> PublicSlotAsync(long eventId, string callsign, DateTime offBlock, CancellationToken cancellationToken) =>
        SlotAsync(
            new EventSlot
            {
                EventId = eventId,
                Kind = SlotKind.Public,
                EventAirportIcao = First,
                IsArrival = false,
                Callsign = callsign,
                AircraftTypes = [Type],
                DepartureIcao = First,
                ArrivalIcao = Away,
                OffBlockUtc = offBlock,
                OnBlockUtc = offBlock.AddHours(1),
            },
            cancellationToken);

    /// <summary>A private slot at the airport of the event, as the generator makes it: its direction and its time there.</summary>
    private Task<long> PrivateSlotAsync(long eventId, bool arrival, DateTime at, CancellationToken cancellationToken) =>
        SlotAsync(
            new EventSlot
            {
                EventId = eventId,
                Kind = SlotKind.Private,
                EventAirportIcao = First,
                IsArrival = arrival,
                OffBlockUtc = arrival ? null : at,
                OnBlockUtc = arrival ? at : null,
                Generated = true,
            },
            cancellationToken);

    private async Task<long> SlotAsync(EventSlot slot, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        slot.OwnerDepartment = Department.ED;
        slot.OwnerDepartmentMask = DepartmentMask.Of(Department.ED);
        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);
        return slot.Id;
    }

    /// <summary>A booking of a slot by a pilot, written by the installation, with whatever else <paramref name="write"/> puts on it.</summary>
    private async Task<long> BookingAsync(long eventId, long slotId, int vid, CancellationToken cancellationToken, Action<EventBooking>? write = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var booking = new EventBooking
        {
            EventId = eventId,
            SlotId = slotId,
            BookerVid = vid,
            AircraftIcao = Type,
            CreatedAt = DateTime.UtcNow,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        write?.Invoke(booking);

        database.Bookings.Add(booking);
        await database.SaveChangesAsync(cancellationToken);
        return booking.Id;
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

    private async Task<Event> EventRowAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    /// <summary>Whether a row of the audit log still copies a text with the marker in it, before or after its change.</summary>
    private async Task<bool> AuditMentionsAsync(string marker, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(row => row.BeforeJson!.Contains(marker) || row.AfterJson!.Contains(marker), cancellationToken);
    }

    /// <summary>A booking taken back as the installation would, through the context: the audit keeps its created and deleted copies.</summary>
    private async Task WithdrawAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        database.Bookings.Remove(await database.Bookings.IgnoreQueryFilters().SingleAsync(row => row.Id == id, cancellationToken));
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Every row of the audit log of one booking, in their order: what happened, and its copies before and after.</summary>
    private async Task<List<(string Action, string? BeforeJson, string? AfterJson)>> AuditRowsAsync(long id, CancellationToken cancellationToken)
    {
        var key = id.ToString(CultureInfo.InvariantCulture);
        await using var scope = _factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking()
            .Where(row => row.Entity == "evt_bookings" && row.EntityId == key)
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => (row.Action, row.BeforeJson, row.AfterJson))];
    }

    /// <summary>The rows the audit log keeps of these bookings going in an erasure: which one, and what it copies of it before and after.</summary>
    private async Task<List<(string Id, string? Before, string? After)>> ErasedRowsAsync(long[] bookings, CancellationToken cancellationToken)
    {
        var ids = bookings.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToArray();
        await using var scope = _factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking()
            .Where(row => row.Action == "erased" && row.Entity == "evt_bookings" && ids.Contains(row.EntityId))
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => (row.EntityId, row.BeforeJson, row.AfterJson))];
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A member of the division, or the super administrator; never with an address. The erasure takes the person's user, so each test seeds it.</summary>
    private async Task SeedUserAsync(int vid, bool superadmin, CancellationToken cancellationToken)
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
        user.LastName = "Erasure";
        user.Email = null;
        user.IsSuperadmin = superadmin;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The airports and the aircraft type of the class, in the core's snapshots, which the page of an event reads.</summary>
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
}
