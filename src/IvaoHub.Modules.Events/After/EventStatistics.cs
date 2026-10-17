using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registrations;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.After;

/// <summary>
/// The numbers of an event (design M4 §1.10, §5.1, E13a; note 2026-10-16-le-verifiche-dopo-l-evento, reading 8), worked out in this one place
/// from its rows — by <c>events-after</c> when it closes an event, and by «Recalculate» of the staff —, so the two give the same numbers while
/// the rows do not change.
/// <list type="bullet">
/// <item>the slots by kind, the bookings, those flown (<c>flown_at</c>) and those checked and not flown; the availabilities, the shifts, those
/// attended, the no-shows — proposed and confirmed —, those excused;</item>
/// <item>the reports of support and the signals of award stay 0 until their tables exist (E14a, E14b);</item>
/// <item>for an event in person, the registrations, the sums of their answers and the seats by activity <b>from the rows while they are
/// there</b>; past its retention the ones <c>events-retention</c> kept (E17) stay as they are;</item>
/// <item><b>who flew without booking</b> comes from the check alone, which counts it on the network a few airports per run
/// (<c>counted</c> of <see cref="WriteAsync"/>): a recalculation keeps it, and «not counted» stays empty, never 0.</item>
/// </list>
/// <para>The row is created when missing, <b>under the lock of the event's row</b> (<c>FOR UPDATE</c>), so the job's close and a «Recalculate»
/// in the same instant never both insert it (the review of #278, point 3): the caller holds a transaction, and saves.</para>
/// </summary>
public sealed class EventStatistics(EventsDbContext database, ModuleSettingsStore settingsStore)
{
    /// <summary>
    /// Writes the numbers of <paramref name="row"/> into its statistics, tracked, under the lock of the event's row: <paramref name="counted"/>
    /// adds the pilots who flew without booking at some more of its airports, and how many airports they were; none keeps what is there.
    /// </summary>
    public async Task<EventStats> WriteAsync(
        Event row,
        DateTime now,
        CancellationToken cancellationToken,
        (int Pilots, int Airports)? counted = null)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (database.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The statistics of an event are written inside a transaction, under the lock of its row.");
        }

        var eventId = row.Id;
        await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {eventId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var stats = await CrudSource.BackOffice<EventStats>(database).FirstOrDefaultAsync(candidate => candidate.EventId == eventId, cancellationToken);
        if (stats is null)
        {
            stats = new EventStats { EventId = eventId };
            database.Stats.Add(stats);
        }

        // In the event's care, as every row of the event (§1.1).
        stats.OwnerDepartment = row.OwnerDepartment;
        stats.OwnerDepartmentMask = row.OwnerDepartmentMask;

        var slots = await CrudSource.BackOffice<EventSlot>(database).AsNoTracking()
            .Where(slot => slot.EventId == eventId)
            .GroupBy(slot => slot.Kind)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        stats.PublicSlots = slots.Where(group => group.Key == SlotKind.Public).Sum(group => group.Count);
        stats.PrivateSlots = slots.Where(group => group.Key == SlotKind.Private).Sum(group => group.Count);

        var bookings = await CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
            .Where(booking => booking.EventId == eventId)
            .Select(booking => new { Flown = booking.FlownAt != null, Checked = booking.FlownCheckedAt != null })
            .ToListAsync(cancellationToken);
        stats.Booked = bookings.Count;
        stats.BookedFlown = bookings.Count(booking => booking.Flown);
        stats.BookedUnflown = bookings.Count(booking => booking.Checked && !booking.Flown);

        if (counted is { } more)
        {
            stats.FlownUnbooked = (stats.FlownUnbookedAirports is null ? 0 : stats.FlownUnbooked) + more.Pilots;
            stats.FlownUnbookedAirports = (stats.FlownUnbookedAirports ?? 0) + more.Airports;
        }

        stats.AtcAvailabilities = await CrudSource.BackOffice<EventAtcAvailability>(database).AsNoTracking()
            .CountAsync(availability => availability.EventId == eventId, cancellationToken);

        var shifts = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
            .Where(shift => shift.EventId == eventId)
            .GroupBy(shift => shift.Attendance)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        int Shifts(params ShiftAttendance[] outcomes) => shifts.Where(group => outcomes.Contains(group.Key)).Sum(group => group.Count);
        stats.AtcShifts = shifts.Sum(group => group.Count);
        stats.AtcAttended = Shifts(ShiftAttendance.Attended);
        stats.AtcNoShows = Shifts(ShiftAttendance.NoShowProposed, ShiftAttendance.NoShow);
        stats.AtcExcused = Shifts(ShiftAttendance.Excused);

        // The reports of support (E14a; note 2026-10-17-il-pirep-di-supporto, reading 11): every one sent, whatever it stands at, and those
        // accepted. The signals of award arrive with E14b.
        var reports = await CrudSource.BackOffice<EventReport>(database).AsNoTracking()
            .Where(report => report.EventId == row.Id)
            .GroupBy(report => report.State)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        stats.ReportsSubmitted = reports.Sum(group => group.Count);
        stats.ReportsAccepted = reports.Where(group => group.Key == ReportState.Accepted).Sum(group => group.Count);
        stats.AwardSignals = 0;

        await InPersonAsync(row, stats, now, cancellationToken);

        stats.ComputedAt = now;
        return stats;
    }

    /// <summary>
    /// The numbers of an event in person from its rows while any is there; past its retention with none left they are gone, and the numbers
    /// <c>events-retention</c> kept stay (E17).
    /// </summary>
    private async Task InPersonAsync(Event row, EventStats stats, DateTime now, CancellationToken cancellationToken)
    {
        var eventId = row.Id;
        var registrations = await CrudSource.BackOffice<EventRegistration>(database).AsNoTracking()
            .Where(registration => registration.EventId == eventId)
            .ToListAsync(cancellationToken);
        var anySeat = await CrudSource.BackOffice<EventActivitySeat>(database).AnyAsync(seat => seat.EventId == eventId, cancellationToken);

        // Past its retention with nothing left, the rows are gone and what events-retention kept stays; while any row is there — the run of
        // the month has not come yet — the numbers are the rows' (reading 8).
        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        if (registrations.Count == 0 && !anySeat && InPersonRetention.IsDue(row, settings.InPersonRetentionMonths, now))
        {
            return;
        }

        var questions = await database.Questions.AsNoTracking()
            .Where(question => question.EventId == eventId)
            .OrderBy(question => question.Ordinal)
            .ThenBy(question => question.Id)
            .ToListAsync(cancellationToken);
        var seats = await CrudSource.BackOffice<EventActivitySeat>(database).AsNoTracking()
            .Where(seat => seat.EventId == eventId)
            .GroupBy(seat => seat.ActivityId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        stats.Registrations = registrations.Count;
        stats.AnswerSumsJson = InPersonRetention.SumsJson(RegistrationSums.Of(questions, registrations).Values);
        stats.ActivitySeatsJson = InPersonRetention.SeatsJson(seats.ToDictionary(seat => seat.Key, seat => seat.Count));
    }
}
