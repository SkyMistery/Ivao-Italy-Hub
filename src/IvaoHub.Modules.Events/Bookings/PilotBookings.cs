using System.Data;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
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
/// or the whole rotation of one, withdrawing a booking, and reading their own. Any signed in member — a pilot is not a role —, on an
/// event they see; nobody else's bookings, ever: the staff's side is <see cref="BookingEndpoints"/>'s «take away».
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
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>The field every refusal about the slot lands on: the slot is what the pilot chose.</summary>
    public const string SlotField = "slotId";

    /// <summary>The field of the aircraft type.</summary>
    public const string AircraftField = "aircraftIcao";

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
        if (Refusal(slot, mine, await TakenAsync([slot.Id], cancellationToken), gap) is { } refused)
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

        return (BookingResult.Done, Mine(booking, slot, row, now), problems);
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
                : Refusal(leg, mine, taken, gap) is { } refused ? refused
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
                    booked.Add(Mine(booking, leg, row, now));
                    continue;
                }
            }

            notBooked.Add(new RotationLegRefusalDto(leg.Id, leg.RotationLeg, leg.Callsign ?? string.Empty, reason));
        }

        await transaction.CommitAsync(cancellationToken);

        return (BookingResult.Done, new RotationBookingDto(booked, notBooked), problems);
    }

    /// <summary>
    /// Withdraws a booking of the pilot's (§3.6, c3): until the off block of its slot — after it, the flight is the pilot's to fly
    /// or not, and the check after the event reads it. The row is deleted and the slot is free again; the audit of the core keeps
    /// who withdrew what. Somebody else's booking is not found.
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
        if (slot is null || !BookingRules.IsOpen(slot, clock.UtcNow))
        {
            return (BookingResult.Refused, problems.Add("id", BookingRules.ClosedKey));
        }

        database.Bookings.Remove(booking);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The staff took it away a moment before: there is nothing left to withdraw.
            return (BookingResult.NotFound, problems);
        }

        return (BookingResult.Done, problems);
    }

    /// <summary>
    /// The pilot's own bookings, past ones too (§7.1): by the off block of their flight. A visitor has none; nobody reads another
    /// member's here.
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

        return
        [
            .. rows
                .OrderBy(entry => entry.slot.OffBlockUtc)
                .ThenBy(entry => entry.booking.Id)
                .Select(entry => Mine(entry.booking, entry.slot, entry.row, now)),
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
    /// A public slot and its event, when the reader sees the event (§2.1): published, for them, from its «seen from» to its end. An
    /// event not seen is not found, as its page is not; a private slot is booked with its own flight (E7), and refused there.
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
    /// Why the pilot cannot have this slot, under their lock: theirs already, another pilot's, or too close to one of theirs (§3.5).
    /// </summary>
    private static string? Refusal(EventSlot slot, List<(EventBooking Booking, EventSlot Slot)> mine, IReadOnlySet<long> taken, int gap)
    {
        if (mine.Any(entry => entry.Booking.SlotId == slot.Id))
        {
            return BookingRules.YoursKey;
        }

        if (taken.Contains(slot.Id))
        {
            return BookingRules.TakenKey;
        }

        return BookingInterval.Of(slot) is { } wanted
            && mine.Any(entry => BookingInterval.Of(entry.Slot) is { } held && !BookingRules.Compatible(held, wanted, gap))
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

    /// <summary>A booking as its pilot reads it: a public slot's flight, with the aircraft they chose.</summary>
    private static MyBookingDto Mine(EventBooking booking, EventSlot slot, Event row, DateTime now) => new(
        booking.Id,
        slot.Id,
        row.Id,
        row.Slug,
        row.Title,
        EventState.Of(row, now),
        slot.Kind,
        slot.Callsign ?? booking.Callsign ?? string.Empty,
        slot.FlightNumber,
        booking.AircraftIcao,
        slot.DepartureIcao ?? string.Empty,
        slot.OffBlockUtc.GetValueOrDefault(),
        slot.ArrivalIcao ?? string.Empty,
        slot.OnBlockUtc.GetValueOrDefault(),
        slot.IsArrival,
        slot.Stand,
        slot.RotationCode,
        slot.RotationLeg,
        BookingRules.IsOpen(slot, now),
        booking.CreatedAt);

    private static (BookingResult, MyBookingDto?, Refusals) Refused(Refusals problems) => (BookingResult.Refused, null, problems);
}
