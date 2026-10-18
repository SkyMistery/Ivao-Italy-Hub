using System.Text.Json.Nodes;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registrations;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The events' half of erasing a person's data (design M4 §11.1, E8b; notes 2026-09-29-i-dati-dei-membri-negli-eventi §2.5,
/// 2026-09-25-la-cancellazione-dei-dati-di-una-persona and 2026-10-11-duplica-e-la-cancellazione-negli-eventi §2), for the rows of M4a:
/// a pilot's bookings — and since then a member's registrations (E16), a controller's availabilities (E11a), shifts (E11b) and requests to hand
/// one over (E12), and a member's seats in the activities (E17), by the same rule.
/// <list type="bullet">
/// <item>A booking of an event <b>still to end</b> goes (<see cref="EventState.IsOver"/>; a cancelled event too, until its end — Carmine's
/// answer 2 on issue #248, (i)): its slot is free again, on the page and in the export. Through the change
/// tracker, so that the audit keeps that a row went and the core empties its copies.</item>
/// <item>A booking of an event <b>over</b> stays, under the pseudonym the core writes into <c>booker_vid</c>: it is the event's history,
/// which its statistics count. Without what is the person's — the flight of a private slot as they wrote it (the callsign, the other
/// airport, the time there), the note the staff wrote about it when it was not flown, and the session of the network it was found in,
/// which ties the row to them on the network's tracker, as the session of a flight of the tours does.</item>
/// <item>A private arrival and its linked departure (E7) are one pilot's, in one event: they go together or stay together, and the
/// link that dissolves when one of a pair goes alone (<c>BookingPairs.LetGoAsync</c>) is not asked for.</item>
/// </list>
/// What they did as staff — an event cancelled, a booking not flown excused — stays, under the pseudonym the core writes into every
/// <c>…By</c> column, with their words, which are about others (answer 4 of the note of T20b). Having covered somebody else's position in a shift they missed
/// (<c>covered_by_vid</c>, E13a) is that shift's history too, and stays under the pseudonym the core writes there. Nothing here writes a VID.
/// </summary>
public sealed class EventsPersonalData(EventsDbContext database, IClock clock) : IPersonalDataEraser
{
    public string ModuleKey => EventsModule.ModuleKey;

    public async Task<IReadOnlyList<ErasureLine>> PreviewAsync(int vid, CancellationToken cancellationToken = default)
    {
        var ends = await Bookings(vid).AsNoTracking()
            .Join(CrudSource.BackOffice<Event>(database), booking => booking.EventId, row => row.Id, (booking, row) => row)
            .ToListAsync(cancellationToken);
        var registered = await Registrations(vid).AsNoTracking()
            .Join(CrudSource.BackOffice<Event>(database), registration => registration.EventId, row => row.Id, (registration, row) => row)
            .ToListAsync(cancellationToken);
        var available = await Availabilities(vid).AsNoTracking()
            .Join(CrudSource.BackOffice<Event>(database), availability => availability.EventId, row => row.Id, (availability, row) => row)
            .ToListAsync(cancellationToken);
        var held = await Shifts(vid).AsNoTracking()
            .Join(CrudSource.BackOffice<Event>(database), shift => shift.EventId, row => row.Id, (shift, row) => row)
            .ToListAsync(cancellationToken);
        var asked = await Transfers(vid).AsNoTracking()
            .Join(CrudSource.BackOffice<Event>(database), transfer => transfer.EventId, row => row.Id, (transfer, row) => row)
            .ToListAsync(cancellationToken);
        var seated = await Seats(vid).AsNoTracking()
            .Join(CrudSource.BackOffice<Event>(database), seat => seat.EventId, row => row.Id, (seat, row) => row)
            .ToListAsync(cancellationToken);
        var reports = await Reports(vid).CountAsync(cancellationToken);
        var now = clock.UtcNow;

        return Lines(
            toCome: ends.Count(row => !EventState.IsOver(row, now)),
            over: ends.Count(row => EventState.IsOver(row, now)),
            registrationsToCome: registered.Count(row => !EventState.IsOver(row, now)),
            registrationsOver: registered.Count(row => EventState.IsOver(row, now)),
            availabilitiesToCome: available.Count(row => !EventState.IsOver(row, now)),
            availabilitiesOver: available.Count(row => EventState.IsOver(row, now)),
            shiftsToCome: held.Count(row => !EventState.IsOver(row, now)),
            shiftsOver: held.Count(row => EventState.IsOver(row, now)),
            transfersToCome: asked.Count(row => !EventState.IsOver(row, now)),
            transfersOver: asked.Count(row => EventState.IsOver(row, now)),
            seatsToCome: seated.Count(row => !EventState.IsOver(row, now)),
            seatsOver: seated.Count(row => EventState.IsOver(row, now)),
            reports);
    }

    public async Task<IReadOnlyList<ErasureLine>> EraseAsync(ErasureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bookings = await Bookings(request.Vid).AsTracking().ToListAsync(cancellationToken);
        var eventIds = bookings.Select(booking => booking.EventId).Distinct().ToList();
        var events = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        // A booking whose event is gone — unreachable today, since its slot's key holds the event — is left out, as the preview's join
        // leaves it out: the two say the same lines (the review of #249, point 1).
        var now = clock.UtcNow;
        var over = bookings.Where(booking => events.TryGetValue(booking.EventId, out var row) && EventState.IsOver(row, now)).ToList();
        var toCome = bookings.Where(booking => events.TryGetValue(booking.EventId, out var row) && !EventState.IsOver(row, now)).ToList();

        // The history of the event, without the person: the slot, the aircraft, whether and when it was flown and checked, the link
        // of a pair, when it was booked. The core writes the pseudonym into the pilot's column afterwards.
        foreach (var booking in over)
        {
            booking.Callsign = null;
            booking.OtherIcao = null;
            booking.OtherTimeUtc = null;
            booking.UnflownExcusedNote = null;
            booking.FlownSessionId = null;
        }

        database.Bookings.RemoveRange(toCome);

        var (registrationsToCome, registrationsOver) = await EraseRegistrationsAsync(request.Vid, now, cancellationToken);
        var (availabilitiesToCome, availabilitiesOver) = await EraseAvailabilitiesAsync(request.Vid, now, cancellationToken);
        var (transfersToCome, transfersOver) = await EraseTransfersAsync(request.Vid, now, cancellationToken);
        var (shiftsToCome, shiftsOver) = await EraseShiftsAsync(request.Vid, now, cancellationToken);
        var (seatsToCome, seatsOver) = await EraseSeatsAsync(request.Vid, now, cancellationToken);
        var reports = await EraseReportsAsync(request.Vid, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return Lines(
            toCome: toCome.Count,
            over: over.Count,
            registrationsToCome,
            registrationsOver,
            availabilitiesToCome,
            availabilitiesOver,
            shiftsToCome,
            shiftsOver,
            transfersToCome,
            transfersOver,
            seatsToCome,
            seatsOver,
            reports);
    }

    /// <summary>
    /// The person's reports of support (E14a; note 2026-10-17-il-pirep-di-supporto, reading 12, as Carmine changed it on #289): all of events
    /// over, so all kept, with the pseudonym the core writes into <c>vid</c> — the statistics and the rules of award count their items —, without
    /// the validator's note about them, and their items without the session that ties them to the person on the network; a <b>flight's</b>
    /// callsign goes too, and a <b>shift's</b> stays: it names a position, not the person, and the rules of the minutes read it.
    /// </summary>
    private async Task<int> EraseReportsAsync(int vid, CancellationToken cancellationToken)
    {
        var reports = await Reports(vid).AsTracking().ToListAsync(cancellationToken);
        foreach (var report in reports)
        {
            report.DecisionNote = null;
        }

        var flights = reports.Where(report => report.Kind == ReportKind.Pilot).Select(report => report.Id).ToHashSet();
        foreach (var item in await CrudSource.BackOffice<EventReportItem>(database).AsTracking()
                     .Where(item => item.Vid == vid)
                     .ToListAsync(cancellationToken))
        {
            if (flights.Contains(item.ReportId))
            {
                item.Callsign = string.Empty;
            }

            item.SessionId = null;
        }

        return reports.Count;
    }

    /// <summary>
    /// The person's seats in the activities of the events in person (E17), with their registrations: one of an event still to end goes — the
    /// turn is free again —; one of an event over stays, under the pseudonym the core writes into <c>vid</c>, since the statistics count it.
    /// A seat has no words of the person to empty.
    /// </summary>
    private async Task<(int ToCome, int Over)> EraseSeatsAsync(int vid, DateTime now, CancellationToken cancellationToken)
    {
        var seats = await Seats(vid).AsTracking().ToListAsync(cancellationToken);
        var eventIds = seats.Select(seat => seat.EventId).Distinct().ToList();
        var events = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        var over = seats.Count(seat => events.TryGetValue(seat.EventId, out var row) && EventState.IsOver(row, now));
        var toCome = seats.Where(seat => events.TryGetValue(seat.EventId, out var row) && !EventState.IsOver(row, now)).ToList();
        database.Seats.RemoveRange(toCome);

        return (toCome.Count, over);
    }

    /// <summary>
    /// The person's shifts as a controller (E11b, treated as the availabilities): one of an event still to end goes — its piece of the
    /// position is uncovered again —; one of an event over stays, under the pseudonym the core writes into <c>controller_vid</c>, with its
    /// hours and how it went, and without the staff's note on it and the why of the proposal, which are about them.
    /// </summary>
    private async Task<(int ToCome, int Over)> EraseShiftsAsync(int vid, DateTime now, CancellationToken cancellationToken)
    {
        var shifts = await Shifts(vid).AsTracking().ToListAsync(cancellationToken);
        var eventIds = shifts.Select(shift => shift.EventId).Distinct().ToList();
        var events = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        var over = shifts.Where(shift => events.TryGetValue(shift.EventId, out var row) && EventState.IsOver(row, now)).ToList();
        var toCome = shifts.Where(shift => events.TryGetValue(shift.EventId, out var row) && !EventState.IsOver(row, now)).ToList();

        foreach (var shift in over)
        {
            shift.AttendanceNote = null;
            shift.ReasonJson = null;
        }

        // A shift that goes takes its requests to hand it over (E12), each with its row in the audit: one asked by somebody whose shift it was
        // until the staff gave it to the person, too.
        var going = toCome.Select(shift => shift.Id).ToList();
        database.AtcTransfers.RemoveRange(await CrudSource.BackOffice<EventAtcTransfer>(database).AsTracking()
            .Where(transfer => going.Contains(transfer.ShiftId))
            .ToListAsync(cancellationToken));
        database.AtcShifts.RemoveRange(toCome);

        return (toCome.Count, over.Count);
    }

    /// <summary>
    /// The person's requests to hand a shift over (E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, reading 9, treated as the
    /// shifts): one of an event still to end goes; one of an event over stays, under the pseudonym the core writes into <c>controller_vid</c>,
    /// without their note and the staff's answer to them. A request that names the person as whoever takes the shift takes the pseudonym from
    /// the core in <c>to_vid</c>, and is approved towards nobody.
    /// </summary>
    private async Task<(int ToCome, int Over)> EraseTransfersAsync(int vid, DateTime now, CancellationToken cancellationToken)
    {
        var transfers = await Transfers(vid).AsTracking().ToListAsync(cancellationToken);
        var eventIds = transfers.Select(transfer => transfer.EventId).Distinct().ToList();
        var events = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        var over = transfers.Where(transfer => events.TryGetValue(transfer.EventId, out var row) && EventState.IsOver(row, now)).ToList();
        var toCome = transfers.Where(transfer => events.TryGetValue(transfer.EventId, out var row) && !EventState.IsOver(row, now)).ToList();

        foreach (var transfer in over)
        {
            transfer.Note = null;
            transfer.DecisionNote = null;
        }

        database.AtcTransfers.RemoveRange(toCome);

        return (toCome.Count, over.Count);
    }

    /// <summary>
    /// The person's availabilities as a controller (E11a, treated as the registrations): one of an event still to end goes, with its
    /// windows — the roster no longer counts on them —; one of an event over stays, under the pseudonym the core writes into
    /// <c>controller_vid</c> of the availability and of its windows, with its hours and without the note, which is their words.
    /// </summary>
    private async Task<(int ToCome, int Over)> EraseAvailabilitiesAsync(int vid, DateTime now, CancellationToken cancellationToken)
    {
        var availabilities = await Availabilities(vid).AsTracking().ToListAsync(cancellationToken);
        var eventIds = availabilities.Select(availability => availability.EventId).Distinct().ToList();
        var events = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        var over = availabilities.Where(availability => events.TryGetValue(availability.EventId, out var row) && EventState.IsOver(row, now)).ToList();
        var toCome = availabilities.Where(availability => events.TryGetValue(availability.EventId, out var row) && !EventState.IsOver(row, now)).ToList();

        foreach (var availability in over)
        {
            availability.Note = null;
        }

        var going = toCome.Select(availability => availability.Id).ToList();
        database.AtcWindows.RemoveRange(await CrudSource.BackOffice<EventAtcWindow>(database).AsTracking()
            .Where(window => going.Contains(window.AvailabilityId))
            .ToListAsync(cancellationToken));
        database.AtcAvailabilities.RemoveRange(toCome);

        return (toCome.Count, over.Count);
    }

    /// <summary>
    /// The person's registrations to the events in person (E16; note 2026-09-29-i-dati-dei-membri-negli-eventi §2.5: «treated as the
    /// bookings»): one of an event still to end goes; one of an event over stays, under the pseudonym, with only the answers the sums count
    /// — a yes or no, a choice, several choices, a quantity, to questions the event still has — and nothing else: no text, which is their
    /// words, as a booking kept loses the staff's note (Carmine's answer 2 on #255), and no history in the audit.
    /// </summary>
    private async Task<(int ToCome, int Over)> EraseRegistrationsAsync(int vid, DateTime now, CancellationToken cancellationToken)
    {
        var registrations = await Registrations(vid).AsTracking().ToListAsync(cancellationToken);
        var eventIds = registrations.Select(registration => registration.EventId).Distinct().ToList();
        var events = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var counted = (await database.Questions.AsNoTracking()
                .Where(question => eventIds.Contains(question.EventId) && question.Type != QuestionType.Text)
                .ToListAsync(cancellationToken))
            .ToLookup(question => question.EventId);

        var over = registrations.Where(registration => events.TryGetValue(registration.EventId, out var row) && EventState.IsOver(row, now)).ToList();
        var toCome = registrations.Where(registration => events.TryGetValue(registration.EventId, out var row) && !EventState.IsOver(row, now)).ToList();

        foreach (var registration in over)
        {
            // Only what the sums count, of questions the event still has, as it fits them; every other key goes — a text, an answer to a
            // question deleted since, a value of another type, a «choice» that is none of the question's (the review of #255, point 1, and
            // small 1 of its second reading).
            var answers = RegistrationAnswers.Of(registration);
            var kept = new JsonObject();
            foreach (var question in counted[registration.EventId])
            {
                var key = EventQuestion.AnswerKey(question.Id);
                if (RegistrationAnswers.Counted(question, answers[key]))
                {
                    kept[key] = answers[key]!.DeepClone();
                }
            }

            registration.AnswersJson = kept.ToJsonString();

            // Written even when nothing changed, so the core counts the row as emptied and empties its earlier copies in the audit, which may
            // still hold words the member took out of an earlier answer (point 2): a row whose only change is the pseudonym keeps its history.
            database.Entry(registration).Property(row => row.AnswersJson).IsModified = true;
        }
        database.Registrations.RemoveRange(toCome);

        return (toCome.Count, over.Count);
    }

    /// <summary>The person's bookings, read past every filter: the superadmin erases bookings of events nobody sees any more.</summary>
    private IQueryable<EventBooking> Bookings(int vid) =>
        CrudSource.BackOffice<EventBooking>(database).Where(booking => booking.BookerVid == vid);

    /// <summary>The person's registrations, read past every filter, as their bookings are.</summary>
    private IQueryable<EventRegistration> Registrations(int vid) =>
        CrudSource.BackOffice<EventRegistration>(database).Where(registration => registration.Vid == vid);

    /// <summary>The person's seats in the activities, read past every filter, as their registrations are.</summary>
    private IQueryable<EventActivitySeat> Seats(int vid) =>
        CrudSource.BackOffice<EventActivitySeat>(database).Where(seat => seat.Vid == vid);

    /// <summary>The person's availabilities as a controller, read past every filter, as their bookings are.</summary>
    private IQueryable<EventAtcAvailability> Availabilities(int vid) =>
        CrudSource.BackOffice<EventAtcAvailability>(database).Where(availability => availability.ControllerVid == vid);

    /// <summary>The person's shifts as a controller, read past every filter, as their bookings are.</summary>
    private IQueryable<EventAtcShift> Shifts(int vid) =>
        CrudSource.BackOffice<EventAtcShift>(database).Where(shift => shift.ControllerVid == vid);

    /// <summary>The person's requests to hand a shift over, read past every filter, as their shifts are.</summary>
    private IQueryable<EventAtcTransfer> Transfers(int vid) =>
        CrudSource.BackOffice<EventAtcTransfer>(database).Where(transfer => transfer.ControllerVid == vid);

    /// <summary>The person's reports of support, read past every filter.</summary>
    private IQueryable<EventReport> Reports(int vid) =>
        CrudSource.BackOffice<EventReport>(database).Where(report => report.Vid == vid);

    private static IReadOnlyList<ErasureLine> Lines(
        int toCome,
        int over,
        int registrationsToCome,
        int registrationsOver,
        int availabilitiesToCome,
        int availabilitiesOver,
        int shiftsToCome,
        int shiftsOver,
        int transfersToCome,
        int transfersOver,
        int seatsToCome,
        int seatsOver,
        int reports) =>
    [
        new("events:erasure.bookingsToCome", toCome, ErasureOutcome.Deleted),
        new("events:erasure.bookingsOver", over, ErasureOutcome.Anonymised),
        new("events:erasure.registrationsToCome", registrationsToCome, ErasureOutcome.Deleted),
        new("events:erasure.registrationsOver", registrationsOver, ErasureOutcome.Anonymised),
        new("events:erasure.availabilitiesToCome", availabilitiesToCome, ErasureOutcome.Deleted),
        new("events:erasure.availabilitiesOver", availabilitiesOver, ErasureOutcome.Anonymised),
        new("events:erasure.shiftsToCome", shiftsToCome, ErasureOutcome.Deleted),
        new("events:erasure.shiftsOver", shiftsOver, ErasureOutcome.Anonymised),
        new("events:erasure.transfersToCome", transfersToCome, ErasureOutcome.Deleted),
        new("events:erasure.transfersOver", transfersOver, ErasureOutcome.Anonymised),
        new("events:erasure.seatsToCome", seatsToCome, ErasureOutcome.Deleted),
        new("events:erasure.seatsOver", seatsOver, ErasureOutcome.Anonymised),
        new("events:erasure.reports", reports, ErasureOutcome.Anonymised),
    ];
}
