using System.Data;
using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Registers;

/// <summary>What a decision of the staff on a register came to: done, refused field by field, nothing there, not theirs to take, or changed meanwhile.</summary>
public enum DecisionResult
{
    Done,
    Refused,
    NotFound,
    Forbidden,
    Changed,
}

/// <summary>What the staff send to decide how a shift went (§4.5): the outcome, and a note — required to excuse.</summary>
public sealed record AttendanceDecision(ShiftAttendance? Attendance, string? Note);

/// <summary>What the staff send to take a booking not flown out of its pilot's record (§3.7): why.</summary>
public sealed record UnflownExcuse(string? Note);

/// <summary>
/// The staff's two decisions on the registers (design M4 §3.7, §4.5, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 1, 2
/// and 6), each asked of the one handler on its row, in its event's care as it is now.
/// <list type="bullet">
/// <item><b>How a shift went</b>, with <c>EventAtc.Edit</c> on the shift — denied to its controller, the super administrator included
/// (<c>DeniedToStakeholder</c>), and on its position's FIR for the staff of a FIR —: on an event over and not cancelled, an unknown shift or a
/// no-show the check proposed becomes attended, a no-show or excused; a no-show becomes excused only, at any time — the no-show taken away
/// —; attended and excused are final. To excuse, a note. Under the lock of the event's row, the one the check after the event takes. The
/// statistics of the event are not written here: they are a row of the events' area, which the staff of the ATC do not write — whoever
/// writes the event presses «Recalculate» (E13a).</item>
/// <item><b>Taking a booking not flown out of its pilot's record</b>, with <c>EventBookings.Edit</c> on the booking: one the check looked at and
/// found no flight for, once, with a note. The booking carries <c>[WithdrawnByStakeholder]</c> because it carries no decision of the staff
/// while its pilot may withdraw it (Carmine's rule on #232, point 6): this one falls after the check, which comes after the event's end and
/// the six hours a slot may lie past it — after the off block of every slot, when no booking can be withdrawn any more.</item>
/// </list>
/// </summary>
public sealed class RegisterDecisions(
    EventsDbContext database,
    EventChildren children,
    IAuthorizationService authorization,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>A shift of an event not over yet: how it went is not known.</summary>
    public const string NotOverKey = "events:errors.attendanceNotOver";

    /// <summary>A shift of a cancelled event: nobody was meant to hold it.</summary>
    public const string CancelledKey = "events:errors.alreadyCancelled";

    /// <summary>An outcome that is not one of the staff's: attended, a no-show, excused.</summary>
    public const string OutcomeKey = "events:errors.attendanceNotAllowed";

    /// <summary>A shift already decided for good — attended, excused —, or a no-show given anything but an excuse.</summary>
    public const string DecidedKey = "events:errors.attendanceDecided";

    /// <summary>A booking the check found flown, or did not look at yet.</summary>
    public const string NotUnflownKey = "events:errors.bookingNotUnflown";

    /// <summary>A booking already taken out of the record.</summary>
    public const string ExcusedKey = "events:errors.bookingAlreadyExcused";

    /// <summary>
    /// The outcomes a shift may pass to, from the one it has (reading 1): an unknown shift or a proposed no-show to any of the staff's, a
    /// no-show to excused alone; attended and excused to none.
    /// </summary>
    public static bool Allowed(ShiftAttendance from, ShiftAttendance to) =>
        to is ShiftAttendance.Attended or ShiftAttendance.NoShow or ShiftAttendance.Excused
        && from switch
        {
            ShiftAttendance.Unknown or ShiftAttendance.NoShowProposed => true,
            ShiftAttendance.NoShow => to == ShiftAttendance.Excused,
            _ => false,
        };

    /// <summary>Decides how a shift went (readings 1 and 2).</summary>
    public async Task<(DecisionResult Result, Refusals Problems)> DecideAsync(
        long id,
        AttendanceDecision decision,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var problems = new Refusals();
        var note = Note(decision.Note, EventAtcShift.MaxNoteLength, problems);
        var now = clock.UtcNow;

        var eventId = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => (long?)candidate.EventId)
            .FirstOrDefaultAsync(cancellationToken);
        if (eventId is null)
        {
            return (DecisionResult.NotFound, problems);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {eventId.Value} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var shift = await CrudSource.BackOffice<EventAtcShift>(database).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (shift is null || (await children.AdoptAsync(shift, cancellationToken)).Event is not { } row)
        {
            return (DecisionResult.NotFound, problems);
        }

        if (!(await authorization.AuthorizeAsync(user, shift, EventsPermissions.AtcEdit)).Succeeded)
        {
            return (DecisionResult.Forbidden, problems);
        }

        if (row.CancelledAt is not null)
        {
            return (DecisionResult.Refused, problems.Add("id", CancelledKey));
        }

        if (!EventState.IsOver(row, now))
        {
            return (DecisionResult.Refused, problems.Add("id", NotOverKey));
        }

        if (decision.Attendance is not { } outcome)
        {
            problems.Add("attendance", "errors.required");
        }
        else if (outcome is not (ShiftAttendance.Attended or ShiftAttendance.NoShow or ShiftAttendance.Excused))
        {
            problems.Add("attendance", OutcomeKey);
        }
        else if (!Allowed(shift.Attendance, outcome))
        {
            problems.Add("attendance", DecidedKey);
        }

        if (decision.Attendance == ShiftAttendance.Excused && note is null)
        {
            problems.Add("note", "errors.required");
        }

        if (!problems.IsEmpty)
        {
            return (DecisionResult.Refused, problems);
        }

        shift.Attendance = decision.Attendance!.Value;
        shift.AttendanceBy = currentUser.Vid;
        shift.AttendanceAt = now;
        shift.AttendanceNote = note;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            return (DecisionResult.Changed, problems);
        }

        return (DecisionResult.Done, problems);
    }

    /// <summary>Takes a booking not flown out of its pilot's record, with a note (reading 6).</summary>
    public async Task<(DecisionResult Result, Refusals Problems)> ExcuseAsync(
        long id,
        UnflownExcuse excuse,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(excuse);

        var problems = new Refusals();
        var note = Note(excuse.Note, EventBooking.MaxNoteLength, problems);

        var booking = await CrudSource.BackOffice<EventBooking>(database).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (booking is null || (await children.AdoptAsync(booking, cancellationToken)).Event is null)
        {
            return (DecisionResult.NotFound, problems);
        }

        if (!(await authorization.AuthorizeAsync(user, booking, EventsPermissions.BookingsEdit)).Succeeded)
        {
            return (DecisionResult.Forbidden, problems);
        }

        if (booking.FlownCheckedAt is null || booking.FlownAt is not null)
        {
            return (DecisionResult.Refused, problems.Add("id", NotUnflownKey));
        }

        if (booking.UnflownExcusedBy is not null)
        {
            return (DecisionResult.Refused, problems.Add("id", ExcusedKey));
        }

        if (note is null && problems.IsEmpty)
        {
            problems.Add("note", "errors.required");
        }

        if (!problems.IsEmpty)
        {
            return (DecisionResult.Refused, problems);
        }

        booking.UnflownExcusedBy = currentUser.Vid;
        booking.UnflownExcusedNote = note;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Taken away, or its pilot's data erased, a moment before.
            database.ChangeTracker.Clear();
            return (DecisionResult.Changed, problems);
        }

        return (DecisionResult.Done, problems);
    }

    private static string? Note(string? text, int longest, Refusals problems)
    {
        var note = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (note?.Length > longest)
        {
            problems.Add("note", "errors.text.tooLong");
        }

        return note;
    }
}
