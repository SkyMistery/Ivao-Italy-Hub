using System.Data;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Bookings;

/// <summary>What a verb of a pilot came to: done, refused field by field, or about nothing the pilot may see.</summary>
public enum BookingResult
{
    Done,
    Refused,
    NotFound,
}

/// <summary>
/// A pilot's bookings (design M4 §3.3, §3.5, §3.6, §10.1; note 2026-09-29-gli-slot-e-le-prenotazioni §2.5–§2.7): booking a public slot,
/// or the whole rotation of one, booking a private slot with the flight they fly, and its linked departure with it (§3.4, E7),
/// withdrawing a booking, and reading their own. Any signed in member — a pilot is not a role —, on an event they see; nobody else's
/// bookings, ever: the staff's side is <see cref="BookingEndpoints"/>'s «take away».
/// <para><b>The evening the bookings open</b> (§10.1): many pilots in the same minute, perhaps two processes. One slot, one booking is
/// the unique index of the database: two pilots booking the same slot in the same instant meet there, and the second reads «slot just
/// taken by another pilot». The compatibility of one pilot's bookings (§3.5) is checked once, by the server, with the bookings
/// already saved before it: two requests of the same pilot are serialised by a lock — <c>SELECT … FOR UPDATE</c> on the row of their
/// first booking of the event, or on the row of the event when it is their first. No lock in memory, no cache of what is free.</para>
/// <para>The transaction reads what is committed (<c>READ COMMITTED</c>): the request that waited for the lock reads the booking the
/// other one just saved, and a lock on a booking that does not exist yet takes no gap — under the default isolation of the
/// database the first bookings of two pilots would lock the same gap of the index, and deadlock on the row of the event.</para>
/// </summary>
public sealed class PilotBookings(
    EventsDbContext database,
    ModuleSettingsStore settings,
    IAirportDirectory airports,
    IAircraftTypeDirectory aircraftTypes,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>The field every refusal about the slot lands on: the slot is what the pilot chose.</summary>
    public const string SlotField = "slotId";

    /// <summary>The field of the aircraft type.</summary>
    public const string AircraftField = "aircraftIcao";

    /// <summary>The fields of the flight a pilot writes for a private slot (E7): the linked departure's are the same after its prefix.</summary>
    public const string CallsignField = "callsign";

    public const string OtherIcaoField = "otherIcao";

    public const string OtherTimeField = "otherTimeUtc";

    /// <summary>The slot of the linked departure, where every refusal about it lands.</summary>
    public const string DepartureSlotField = PrivateBookingRequestValidator.DeparturePrefix + SlotField;

    /// <summary>
    /// Books one public slot (§3.3): an event the pilot sees, not cancelled, whose bookings are open; the slot still open, with an
    /// aircraft type it allows; nobody's yet; and compatible with the pilot's other bookings of the event, under the pilot's lock.
    /// <para>A deadlock rolls the transaction back: it is thrown, and the caller answers «try again», as for the whole rotation —
    /// it is not a slot taken, and the same request may well go through a moment later.</para>
    /// </summary>
    public async Task<(BookingResult Result, MyBookingDto? Booking, Refusals Problems)> BookAsync(
        BookingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Refusals();
        var now = clock.UtcNow;
        var (slot, row) = await SlotOfASeenEventAsync(request.SlotId, now, cancellationToken);
        if (slot is null || row is null)
        {
            return (BookingResult.NotFound, null, problems);
        }

        var aircraft = request.AircraftIcao!.Trim().ToUpperInvariant();
        if (Closed(slot, row, now) is { } closed)
        {
            return Refused(problems.Add(SlotField, closed));
        }

        if (!slot.AircraftTypes.Contains(aircraft, StringComparer.Ordinal))
        {
            return Refused(problems.Add(AircraftField, BookingRules.AircraftKey));
        }

        var gap = await GapAsync(cancellationToken);

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockThePilotAsync(row.Id, cancellationToken);

        if (await EventNowAsync(row.Id, now, cancellationToken) is not { } current)
        {
            return (BookingResult.NotFound, null, problems);
        }

        if (BookingRules.EventClosed(current, now) is { } closedNow)
        {
            return Refused(problems.Add(SlotField, closedNow));
        }

        var mine = await MineInEventAsync(row.Id, cancellationToken);
        if (Refusal(slot.Id, BookingInterval.Of(slot), mine, await TakenAsync([slot.Id], cancellationToken), gap) is { } refused)
        {
            return Refused(problems.Add(SlotField, refused));
        }

        var booking = NewBooking(row, slot, aircraft, now);
        database.Bookings.Add(booking);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.Duplicated(exception))
        {
            // Another pilot booked it an instant before: the unique index answered.
            database.Entry(booking).State = EntityState.Detached;
            return Refused(problems.Add(SlotField, BookingRules.TakenKey));
        }
        catch (Exception exception) when (DatabaseErrors.LostTheSlot(exception))
        {
            // The staff deleted the slot in the same instant: it is not there any more.
            database.Entry(booking).State = EntityState.Detached;
            return (BookingResult.NotFound, null, problems);
        }

        return (BookingResult.Done, Mine(booking, slot, row, now, paired: null), problems);
    }

    /// <summary>
    /// Books the whole rotation a public slot belongs to (§3.3, c3): in one transaction, every leg still open, free, allowing the
    /// aircraft and compatible with the pilot's bookings — those already saved and the legs booked a moment before in the same
    /// request —, in the order of the legs; and says which were not, and why. A leg another pilot took in the same instant is
    /// one of those: its insert alone fails, and the rest go on.
    /// <para>A deadlock rolls the whole transaction back, the legs booked before it included: it is thrown, and the caller answers
    /// «try again».</para>
    /// </summary>
    public async Task<(BookingResult Result, RotationBookingDto? Rotation, Refusals Problems)> BookRotationAsync(
        BookingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Refusals();
        var now = clock.UtcNow;
        var (slot, row) = await SlotOfASeenEventAsync(request.SlotId, now, cancellationToken);
        if (slot is null || row is null)
        {
            return (BookingResult.NotFound, null, problems);
        }

        if (slot.Kind != SlotKind.Public || slot.RotationCode is not { } rotation)
        {
            return (BookingResult.Refused, null, problems.Add(SlotField, "events:errors.slotNotInRotation"));
        }

        if (BookingRules.EventClosed(row, now) is { } closed)
        {
            return (BookingResult.Refused, null, problems.Add(SlotField, closed));
        }

        var aircraft = request.AircraftIcao!.Trim().ToUpperInvariant();
        var gap = await GapAsync(cancellationToken);

        // Its legs, in their order. The database does not mind the case and a rotation does: R1 and r1 are two (note of E5, §1.4).
        var legs = (await database.Slots.AsNoTracking()
                .Where(leg => leg.EventId == row.Id && leg.Kind == SlotKind.Public && leg.RotationCode == rotation)
                .ToListAsync(cancellationToken))
            .Where(leg => string.Equals(leg.RotationCode, rotation, StringComparison.Ordinal))
            .OrderBy(leg => leg.RotationLeg)
            .ThenBy(leg => leg.Id)
            .ToList();

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockThePilotAsync(row.Id, cancellationToken);

        if (await EventNowAsync(row.Id, now, cancellationToken) is not { } current)
        {
            return (BookingResult.NotFound, null, problems);
        }

        if (BookingRules.EventClosed(current, now) is { } closedNow)
        {
            return (BookingResult.Refused, null, problems.Add(SlotField, closedNow));
        }

        var mine = await MineInEventAsync(row.Id, cancellationToken);
        var taken = await TakenAsync([.. legs.Select(leg => leg.Id)], cancellationToken);
        var booked = new List<MyBookingDto>();
        var notBooked = new List<RotationLegRefusalDto>();

        foreach (var leg in legs)
        {
            var reason = !BookingRules.IsOpen(leg, now) ? BookingRules.ClosedKey
                : Refusal(leg.Id, BookingInterval.Of(leg), mine, taken, gap) is { } refused ? refused
                : !leg.AircraftTypes.Contains(aircraft, StringComparer.Ordinal) ? BookingRules.AircraftKey
                : null;

            if (reason is null)
            {
                var booking = NewBooking(row, leg, aircraft, now);
                database.Bookings.Add(booking);

                try
                {
                    await database.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception) when (!DatabaseErrors.Deadlocked(exception)
                    && (DatabaseErrors.TookTheKey(exception) || DatabaseErrors.LostTheSlot(exception)))
                {
                    // Taken by another pilot in the same instant, or deleted by the staff: this leg alone fails, the rest go on.
                    database.Entry(booking).State = EntityState.Detached;
                    reason = DatabaseErrors.LostTheSlot(exception) ? "events:errors.slotGone" : BookingRules.TakenKey;
                }

                if (reason is null)
                {
                    mine.Add((booking, leg));
                    booked.Add(Mine(booking, leg, row, now, paired: null));
                    continue;
                }
            }

            notBooked.Add(new RotationLegRefusalDto(leg.Id, leg.RotationLeg, leg.Callsign ?? string.Empty, reason));
        }

        await transaction.CommitAsync(cancellationToken);

        return (BookingResult.Done, new RotationBookingDto(booked, notBooked), problems);
    }

    /// <summary>
    /// Books a private slot with the flight the pilot flies through it (§3.4, E7): the callsign, the aircraft type they declare — one
    /// the core knows —, the other airport — one the core knows, not the slot's own — and the time there. An arrival leaves that
    /// airport at that time and lands at the slot's; a departure leaves at the slot's and lands there at that time. Bookable while the
    /// off block of that flight is to come: a departure's time at the airport of the event, the time an arrival leaves the other one.
    /// <para><b>The linked departure</b>: an arrival may bring a private departure from the same airport, with its own callsign and
    /// destination, flown with the same aircraft — it lands, and leaves again from the same gate. It leaves at least
    /// <c>bookingGapMinutes</c> after the arrival lands. The two are born together, the arrival naming the departure
    /// (<see cref="EventBooking.PairedBookingId"/>), or neither is: one transaction, under the pilot's lock, each checked against the
    /// pilot's other bookings as one slot is (§3.5).</para>
    /// <para>A refusal lands on the field it is about — the linked departure's under <c>departure.…</c> —; a deadlock is thrown and
    /// answered «try again», as for a public slot.</para>
    /// </summary>
    public async Task<(BookingResult Result, PrivateBookingDto? Booked, Refusals Problems)> BookPrivateAsync(
        PrivateBookingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Refusals();
        var now = clock.UtcNow;
        var (slot, row) = await SlotOfASeenEventAsync(request.SlotId, now, cancellationToken);
        if (slot is null || row is null)
        {
            return (BookingResult.NotFound, null, problems);
        }

        if (slot.Kind != SlotKind.Private)
        {
            return (BookingResult.Refused, null, problems.Add(SlotField, BookingRules.SlotNotPrivateKey));
        }

        if (BookingRules.EventClosed(row, now) is { } closed)
        {
            return (BookingResult.Refused, null, problems.Add(SlotField, closed));
        }

        var aircraft = request.AircraftIcao!.Trim().ToUpperInvariant();
        var booking = PrivateBooking(row, slot, aircraft, request.Callsign, request.OtherIcao, request.OtherTimeUtc, now);
        FlightProblems(slot, booking, now, problems, prefix: string.Empty);

        // The linked departure: a private departure of the same event, from the airport the arrival lands at.
        EventSlot? departureSlot = null;
        EventBooking? departure = null;
        if (request.Departure is not null && !slot.IsArrival)
        {
            problems.Add(DepartureSlotField, BookingRules.PairedOnlyForArrivalKey);
        }
        else if (request.Departure is { } paired)
        {
            departureSlot = await database.Slots.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == paired.SlotId, cancellationToken);

            if (departureSlot is not { Kind: SlotKind.Private, IsArrival: false }
                || departureSlot.EventId != slot.EventId
                || !string.Equals(departureSlot.EventAirportIcao, slot.EventAirportIcao, StringComparison.Ordinal))
            {
                departureSlot = null;
                problems.Add(DepartureSlotField, BookingRules.PairedNotADepartureKey);
            }
            else
            {
                departure = PrivateBooking(row, departureSlot, aircraft, paired.Callsign, paired.OtherIcao, paired.OtherTimeUtc, now);
                FlightProblems(departureSlot, departure, now, problems, PrivateBookingRequestValidator.DeparturePrefix);
            }
        }

        // What the core knows, asked once: the other airports, and the aircraft type.
        var named = await airports.FindAsync(
            [.. new[] { booking.OtherIcao, departure?.OtherIcao }.OfType<string>().Distinct(StringComparer.Ordinal)],
            cancellationToken);
        if (!named.ContainsKey(booking.OtherIcao!))
        {
            problems.Add(OtherIcaoField, "events:errors.airportUnknown");
        }

        if (departure is not null && !named.ContainsKey(departure.OtherIcao!))
        {
            problems.Add(PrivateBookingRequestValidator.DeparturePrefix + OtherIcaoField, "events:errors.airportUnknown");
        }

        if ((await aircraftTypes.UnknownAsync([aircraft], cancellationToken)).Count > 0)
        {
            problems.Add(AircraftField, "events:errors.aircraftUnknown");
        }

        var gap = await GapAsync(cancellationToken);
        var wanted = BookingInterval.Of(slot, booking);
        var wantedDeparture = departureSlot is null ? null : BookingInterval.Of(departureSlot, departure);

        // It leaves the gate the arrival came to: after it has landed, at least the minutes between two bookings of a pilot.
        if (wanted is { } landing && wantedDeparture is { } leaving && leaving.FromUtc < landing.ToUtc.AddMinutes(gap))
        {
            problems.Add(DepartureSlotField, BookingRules.PairedTooSoonKey);
        }

        if (!problems.IsEmpty)
        {
            return (BookingResult.Refused, null, problems);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockThePilotAsync(row.Id, cancellationToken);

        if (await EventNowAsync(row.Id, now, cancellationToken) is not { } current)
        {
            return (BookingResult.NotFound, null, problems);
        }

        if (BookingRules.EventClosed(current, now) is { } closedNow)
        {
            return (BookingResult.Refused, null, problems.Add(SlotField, closedNow));
        }

        var mine = await MineInEventAsync(row.Id, cancellationToken);
        var taken = await TakenAsync(departureSlot is null ? [slot.Id] : [slot.Id, departureSlot.Id], cancellationToken);
        if (Refusal(slot.Id, wanted, mine, taken, gap) is { } refused)
        {
            problems.Add(SlotField, refused);
        }

        if (departureSlot is not null && Refusal(departureSlot.Id, wantedDeparture, mine, taken, gap) is { } refusedDeparture)
        {
            problems.Add(DepartureSlotField, refusedDeparture);
        }

        if (!problems.IsEmpty)
        {
            return (BookingResult.Refused, null, problems);
        }

        // Born together (§3.4): the departure first, so that the arrival names it; neither, if either fails.
        var saving = departure is null ? SlotField : DepartureSlotField;
        try
        {
            if (departure is not null)
            {
                database.Bookings.Add(departure);
                await database.SaveChangesAsync(cancellationToken);
                booking.PairedBookingId = departure.Id;
                saving = SlotField;
            }

            database.Bookings.Add(booking);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.Duplicated(exception) || DatabaseErrors.LostTheSlot(exception))
        {
            // Another pilot booked one of the two an instant before, or the staff took its slot away: nothing is booked.
            Forget(booking, departure);

            if (DatabaseErrors.LostTheSlot(exception) && saving == SlotField)
            {
                return (BookingResult.NotFound, null, new Refusals());
            }

            return (BookingResult.Refused, null, problems.Add(saving, DatabaseErrors.LostTheSlot(exception) ? "events:errors.slotGone" : BookingRules.TakenKey));
        }

        return (
            BookingResult.Done,
            new PrivateBookingDto(
                Mine(booking, slot, row, now, departure?.Id),
                departure is null ? null : Mine(departure, departureSlot!, row, now, booking.Id)),
            problems);
    }

    /// <summary>
    /// What a flight through a private slot says against the slot (E7), before anything is locked: it comes from, or goes to, another
    /// airport; its times are in order — an arrival leaves before it lands at the slot's time, a departure lands after it leaves at the
    /// slot's —; and it has not left yet. Each refusal on its field, the linked departure's after its prefix.
    /// </summary>
    private static void FlightProblems(EventSlot slot, EventBooking flight, DateTime now, Refusals problems, string prefix)
    {
        if (string.Equals(flight.OtherIcao, slot.EventAirportIcao, StringComparison.Ordinal))
        {
            problems.Add(prefix + OtherIcaoField, "events:errors.otherIsTheSlots");
        }

        var atTheAirport = slot.IsArrival ? slot.OnBlockUtc : slot.OffBlockUtc;
        if (atTheAirport is not { } time || time <= now)
        {
            // The slot's own time has passed: an arrival lands no more at it, a departure leaves no more.
            problems.Add(prefix + SlotField, BookingRules.ClosedKey);
            return;
        }

        if (BookingInterval.Of(slot, flight) is not { } interval || interval.ToUtc <= interval.FromUtc)
        {
            problems.Add(prefix + OtherTimeField, "events:errors.onBlockBeforeOffBlock");
        }
        else if (!BookingRules.IsOpen(slot, flight, now))
        {
            // An arrival whose off block, at the other airport, has passed: it is booked before it leaves.
            problems.Add(prefix + OtherTimeField, BookingRules.OffBlockPassedKey);
        }
    }

    /// <summary>A booking of the reader's for a private slot, with the flight they wrote, in the event's care (§1.1).</summary>
    private EventBooking PrivateBooking(
        Event row,
        EventSlot slot,
        string aircraft,
        string? callsign,
        string? otherIcao,
        DateTime? otherTimeUtc,
        DateTime now)
    {
        var booking = NewBooking(row, slot, aircraft, now);
        booking.Callsign = callsign!.Trim().ToUpperInvariant();
        booking.OtherIcao = otherIcao!.Trim().ToUpperInvariant();
        booking.OtherTimeUtc = otherTimeUtc;
        return booking;
    }

    /// <summary>The bookings a failed save leaves behind, taken off the context: nothing of them was kept.</summary>
    private void Forget(params EventBooking?[] bookings)
    {
        foreach (var booking in bookings.OfType<EventBooking>())
        {
            database.Entry(booking).State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Withdraws a booking of the pilot's (§3.6, c3): until the off block of its flight — after it, the flight is the pilot's to fly
    /// or not, and the check after the event reads it. The row is deleted and the slot is free again; the audit of the core keeps
    /// who withdrew what. Somebody else's booking is not found. One of a private arrival and its linked departure goes alone: the
    /// link dissolves and the other stays (<see cref="BookingPairs"/>).
    /// </summary>
    public async Task<(BookingResult Result, Refusals Problems)> WithdrawAsync(long id, CancellationToken cancellationToken)
    {
        var problems = new Refusals();
        var vid = currentUser.Vid;
        var booking = await database.Bookings.FirstOrDefaultAsync(row => row.Id == id && row.BookerVid == vid, cancellationToken);
        if (booking is null)
        {
            return (BookingResult.NotFound, problems);
        }

        var slot = await database.Slots.AsNoTracking().FirstOrDefaultAsync(row => row.Id == booking.SlotId, cancellationToken);
        if (slot is null || !BookingRules.IsOpen(slot, booking, clock.UtcNow))
        {
            return (BookingResult.Refused, problems.Add("id", BookingRules.ClosedKey));
        }

        await BookingPairs.LetGoAsync(database, booking, cancellationToken);
        database.Bookings.Remove(booking);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The staff took it away a moment before — or its linked arrival: there is nothing left to withdraw as it was read.
            return (BookingResult.NotFound, problems);
        }

        return (BookingResult.Done, problems);
    }

    /// <summary>
    /// The pilot's own bookings, past ones too (§7.1): by the off block of their flight — a private one's as its pilot wrote it (E7).
    /// A private arrival and its linked departure each say the other. A visitor has none; nobody reads another member's here.
    /// </summary>
    public async Task<IReadOnlyList<MyBookingDto>> MineAsync(CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var now = clock.UtcNow;

        var rows = await database.Bookings.AsNoTracking()
            .Where(booking => booking.BookerVid == vid)
            .Join(database.Slots.AsNoTracking(), booking => booking.SlotId, slot => slot.Id, (booking, slot) => new { booking, slot })
            .Join(database.Events.AsNoTracking(), pair => pair.booking.EventId, row => row.Id, (pair, row) => new { pair.booking, pair.slot, row })
            .ToListAsync(cancellationToken);

        var pairs = BookingPairs.Of(rows.Select(entry => entry.booking));

        return
        [
            .. rows
                .OrderBy(entry => BookedFlight.Of(entry.slot, entry.booking).OffBlockUtc)
                .ThenBy(entry => entry.booking.Id)
                .Select(entry => Mine(entry.booking, entry.slot, entry.row, now, pairs.TryGetValue(entry.booking.Id, out var paired) ? paired : null)),
        ];
    }

    /// <summary>
    /// The pilot's lock on their bookings of an event (§3.5): the row of their first booking of it, or the row of the event when
    /// they have none — so two requests of the same pilot run one after the other, and the second reads what the first saved.
    /// Different pilots with bookings lock different rows; first bookings meet on the event's, one transaction each, briefly.
    /// </summary>
    private async Task LockThePilotAsync(long eventId, CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var first = await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_bookings WHERE event_id = {eventId} AND booker_vid = {vid} ORDER BY id LIMIT 1 FOR UPDATE")
            .ToListAsync(cancellationToken);

        if (first.Count == 0)
        {
            await database.Database
                .SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {eventId} FOR UPDATE")
                .ToListAsync(cancellationToken);
        }
    }

    /// <summary>
    /// A slot and its event, when the reader sees the event (§2.1): published, for them, from its «seen from» to its end. An event
    /// not seen is not found, as its page is not. Each verb says which kind of slot it books: a public one with its flight, a private
    /// one with the flight the pilot writes (E7).
    /// </summary>
    private async Task<(EventSlot? Slot, Event? Event)> SlotOfASeenEventAsync(long slotId, DateTime now, CancellationToken cancellationToken)
    {
        var slot = await database.Slots.AsNoTracking().FirstOrDefaultAsync(row => row.Id == slotId, cancellationToken);
        if (slot is null)
        {
            return (null, null);
        }

        var row = await database.Events.AsNoTracking()
            .Where(EventState.Seen(now))
            .FirstOrDefaultAsync(candidate => candidate.Id == slot.EventId, cancellationToken);

        return (slot, row);
    }

    /// <summary>
    /// The event as it is now, read again under the pilot's lock and asked what the first read asked: a cancellation saved while
    /// the request waited for its lock is seen — on the row of the event the first booking of a pilot waits for the cancellation
    /// itself —, and no booking enters an event its pilots were just told is cancelled. None when the event stopped being seen
    /// meanwhile — no longer published, its «seen from» moved later, its end moved earlier (the review of #233, point 7) — or was
    /// deleted: not found, as on the first read.
    /// </summary>
    private Task<Event?> EventNowAsync(long eventId, DateTime now, CancellationToken cancellationToken) =>
        database.Events.AsNoTracking().Where(EventState.Seen(now)).FirstOrDefaultAsync(row => row.Id == eventId, cancellationToken);

    /// <summary>Why a slot cannot be booked now, before anything is locked: a private one, an event closed, a slot closed.</summary>
    private static string? Closed(EventSlot slot, Event row, DateTime now) =>
        slot.Kind != SlotKind.Public ? "events:errors.bookingPrivateSlot"
        : BookingRules.EventClosed(row, now) is { } closed ? closed
        : !BookingRules.IsOpen(slot, now) ? BookingRules.ClosedKey
        : null;

    /// <summary>
    /// Why the pilot cannot have this slot, under their lock: theirs already, another pilot's, or a flight — <paramref name="wanted"/>,
    /// a public slot's own or the one written for a private slot — too close to one of theirs, public or private (§3.5).
    /// </summary>
    private static string? Refusal(
        long slotId,
        BookingInterval? wanted,
        List<(EventBooking Booking, EventSlot Slot)> mine,
        IReadOnlySet<long> taken,
        int gap)
    {
        if (mine.Any(entry => entry.Booking.SlotId == slotId))
        {
            return BookingRules.YoursKey;
        }

        if (taken.Contains(slotId))
        {
            return BookingRules.TakenKey;
        }

        return wanted is { } flight
            && mine.Any(entry => BookingInterval.Of(entry.Slot, entry.Booking) is { } held && !BookingRules.Compatible(held, flight, gap))
            ? BookingRules.IncompatibleKey
            : null;
    }

    /// <summary>The pilot's bookings of the event with their slots, as committed now: what a new one is checked against.</summary>
    private async Task<List<(EventBooking Booking, EventSlot Slot)>> MineInEventAsync(long eventId, CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var rows = await database.Bookings.AsNoTracking()
            .Where(booking => booking.EventId == eventId && booking.BookerVid == vid)
            .Join(database.Slots.AsNoTracking(), booking => booking.SlotId, slot => slot.Id, (booking, slot) => new { booking, slot })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => (row.booking, row.slot))];
    }

    /// <summary>Which of these slots somebody booked, whoever asks: a booking is a row of a member, never shown here but counted.</summary>
    private async Task<IReadOnlySet<long>> TakenAsync(IReadOnlyCollection<long> slotIds, CancellationToken cancellationToken) =>
        (await CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
            .Where(booking => slotIds.Contains(booking.SlotId))
            .Select(booking => booking.SlotId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    private async Task<int> GapAsync(CancellationToken cancellationToken) =>
        (await settings.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken)).BookingGapMinutes;

    /// <summary>A booking of the reader's, in the event's care: the departments and the scope of every row of the event (§1.1).</summary>
    private EventBooking NewBooking(Event row, EventSlot slot, string aircraft, DateTime now) => new()
    {
        EventId = row.Id,
        SlotId = slot.Id,
        BookerVid = currentUser.Vid,
        AircraftIcao = aircraft,
        CreatedAt = now,
        OwnerDepartment = row.OwnerDepartment,
        OwnerDepartmentMask = row.OwnerDepartmentMask,
    };

    /// <summary>
    /// A booking as its pilot reads it: its flight (<see cref="BookedFlight"/>), with the aircraft they chose, and the booking it is
    /// linked with, when it is a private arrival or its departure.
    /// </summary>
    private static MyBookingDto Mine(EventBooking booking, EventSlot slot, Event row, DateTime now, long? paired)
    {
        var flight = BookedFlight.Of(slot, booking);

        return new MyBookingDto(
            booking.Id,
            slot.Id,
            row.Id,
            row.Slug,
            row.Title,
            EventState.Of(row, now),
            slot.Kind,
            flight.Callsign ?? string.Empty,
            slot.FlightNumber,
            booking.AircraftIcao,
            flight.DepartureIcao ?? string.Empty,
            flight.OffBlockUtc.GetValueOrDefault(),
            flight.ArrivalIcao ?? string.Empty,
            flight.OnBlockUtc.GetValueOrDefault(),
            slot.IsArrival,
            slot.Stand,
            slot.RotationCode,
            slot.RotationLeg,
            BookingRules.IsOpen(slot, booking, now),
            booking.CreatedAt,
            paired);
    }

    private static (BookingResult, MyBookingDto?, Refusals) Refused(Refusals problems) => (BookingResult.Refused, null, problems);
}
