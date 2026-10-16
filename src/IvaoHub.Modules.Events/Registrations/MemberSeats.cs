using System.Data;
using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>What a member sends to take a seat (design M4 §4-bis.2, E17): the activity, and the start of the turn they want.</summary>
public sealed record SeatRequest(long ActivityId, DateTime? StartsAtUtc);

/// <summary>The rules one request can answer by itself; whether the turn is one of the activity's, and free, are the server's under its lock.</summary>
public sealed class SeatRequestValidator : AbstractValidator<SeatRequest>
{
    public SeatRequestValidator()
    {
        RuleFor(request => request.ActivityId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(request => request.StartsAtUtc).NotNull().WithMessage("errors.required");
    }
}

/// <summary>
/// A seat as its member reads it, under their registration on <c>/events/mine</c> and on the event's page: the activity with its words —
/// which the page writes in the language on screen —, when the turn starts and ends.
/// </summary>
public sealed record MySeatDto(
    long Id,
    long ActivityId,
    Localized<string> Activity,
    Localized<string>? Place,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc);

/// <summary>
/// A member's seats in the parallel activities of an event in person (design M4 §4-bis.1, §4-bis.2, E17; note
/// 2026-10-15-le-attivita-e-la-conservazione-breve §2): taking one in a turn and giving it back, until the event starts. Any signed in member
/// registered to the event; nobody else's seat, ever.
/// <para><b>The last seat of a turn</b>: a turn has several seats, so no unique index can count them. In a transaction that reads what is
/// committed (<c>READ COMMITTED</c>, as a pilot's booking, §10.1), the member's registration is locked first (<c>SELECT … FOR
/// UPDATE</c>) — two requests of the same member, and a withdrawal in the same instant, wait for each other, so the overlap of their turns is
/// checked against the seats the other just took —, then the activity's row, under which the seats of the turn are counted: two members on
/// the last seat in the same instant, one wins, and the other reads «turn full». Always in that order, so no two requests wait on each
/// other in a circle.</para>
/// </summary>
public sealed class MemberSeats(
    EventsDbContext database,
    EventsMail mail,
    LocaleCatalog catalog,
    IOptions<DivisionOptions> division,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>The field every refusal about the turn lands on: the turn is what the member chose.</summary>
    public const string TurnField = "startsAtUtc";

    public const string NotRegisteredKey = "events:errors.seatNotRegistered";

    public const string NoTurnKey = "events:errors.seatNoTurn";

    public const string FullKey = "events:errors.seatFull";

    public const string OverlapKey = "events:errors.seatOverlaps";

    public const string AlreadyKey = "events:errors.seatAlreadyYours";

    /// <summary>
    /// An instant the client sent, in UTC: one that says its zone is turned into UTC, one that says none is read as UTC — never as the
    /// server's time (the review of #271, point 5), as the slots read theirs (<c>SlotRules</c>, <c>AssumeUniversal</c>).
    /// </summary>
    public static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    /// <summary>
    /// Takes a seat in a turn of an activity (§4-bis.2): an event in person the member sees, open to registrations (until its start, not
    /// cancelled), which they registered to; a turn of the activity; a seat still free; no other turn of theirs in the event that meets
    /// it. The member is told their registration as it is now (<see cref="EventsNotifications.RegistrationReceived"/>), once it is saved.
    /// </summary>
    public async Task<(BookingResult Result, MySeatDto? Seat, Refusals Problems)> TakeAsync(SeatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Refusals();
        var now = clock.UtcNow;
        var vid = currentUser.Vid;
        var start = Utc(request.StartsAtUtc!.Value);

        var activity = await database.Activities.AsNoTracking().FirstOrDefaultAsync(row => row.Id == request.ActivityId, cancellationToken);
        if (activity is null || await SeenAsync(activity.EventId, now, cancellationToken) is not { InPerson: true } row)
        {
            return (BookingResult.NotFound, null, problems);
        }

        if (!RegistrationRules.IsOpen(row, now))
        {
            return (BookingResult.Refused, null, problems.Add(TurnField, RegistrationRules.ClosedKey));
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        if (!await RegistrationSeats.LockAsync(database, row.Id, vid, cancellationToken))
        {
            return (BookingResult.Refused, null, problems.Add(TurnField, NotRegisteredKey));
        }

        var locked = await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_activities WHERE id = {activity.Id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        // Read again under the locks: the staff may have changed the activity, or cancelled the event, a moment before.
        activity = await database.Activities.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == request.ActivityId, cancellationToken);
        var current = await SeenAsync(row.Id, now, cancellationToken);
        if (locked.Count == 0 || activity is null || current is null)
        {
            return (BookingResult.NotFound, null, problems);
        }

        if (!RegistrationRules.IsOpen(current, now))
        {
            return (BookingResult.Refused, null, problems.Add(TurnField, RegistrationRules.ClosedKey));
        }

        if (!activity.IsTurn(start))
        {
            return (BookingResult.Refused, null, problems.Add(TurnField, NoTurnKey));
        }

        var mine = await RegistrationSeats.OfAsync(database, [row.Id], vid, cancellationToken);
        if (mine.Any(entry => entry.Seat.ActivityId == activity.Id && entry.Seat.StartsAtUtc == start))
        {
            return (BookingResult.Refused, null, problems.Add(TurnField, AlreadyKey));
        }

        var ends = start.AddMinutes(activity.SlotMinutes);
        if (mine.Any(entry => entry.Seat.StartsAtUtc < ends && start < entry.Seat.StartsAtUtc.AddMinutes(entry.Activity.SlotMinutes)))
        {
            return (BookingResult.Refused, null, problems.Add(TurnField, OverlapKey));
        }

        var taken = await CrudSource.BackOffice<EventActivitySeat>(database)
            .CountAsync(seat => seat.ActivityId == activity.Id && seat.StartsAtUtc == start, cancellationToken);
        if (taken >= activity.SeatsPerSlot)
        {
            return (BookingResult.Refused, null, problems.Add(TurnField, FullKey));
        }

        // In the event's care: the departments and the scope of every row of the event (§1.1).
        var seat = new EventActivitySeat
        {
            ActivityId = activity.Id,
            EventId = row.Id,
            Vid = vid,
            StartsAtUtc = start,
            CreatedAt = now,
            OwnerDepartment = current.OwnerDepartment,
            OwnerDepartmentMask = current.OwnerDepartmentMask,
        };
        database.Seats.Add(seat);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await TellAsync(current, vid, cancellationToken);

        return (BookingResult.Done, Of(seat, activity), problems);
    }

    /// <summary>
    /// Gives a seat back (§4-bis.2): until the event starts, a cancelled one too, as withdrawing. The row is deleted, as the core lets the member
    /// who took it do (<c>[WithdrawnByStakeholder]</c>, E10h), loaded first so that the guard reads whose it is. Somebody else's seat is not
    /// found. The member is told their registration as it is now.
    /// <para>Under the registration's lock, as every change of the member's seats (the review of #271, point 2): a withdrawal in the same
    /// instant takes the seat with the registration, and this one then finds nothing — never a write of a row already gone.</para>
    /// </summary>
    public async Task<(BookingResult Result, Refusals Problems)> GiveBackAsync(long id, CancellationToken cancellationToken)
    {
        var problems = new Refusals();
        var vid = currentUser.Vid;
        var mine = await database.Seats.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.Vid == vid, cancellationToken);
        if (mine is null)
        {
            return (BookingResult.NotFound, problems);
        }

        var row = await database.Events.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == mine.EventId, cancellationToken);
        if (row is null || !RegistrationRules.IsWithdrawable(row, clock.UtcNow))
        {
            return (BookingResult.Refused, problems.Add("id", RegistrationRules.ClosedKey));
        }

        await using (var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            // Read again under the lock: a withdrawal, or the staff's «take away», may have taken it with the registration.
            var seat = await RegistrationSeats.LockAsync(database, row.Id, vid, cancellationToken)
                ? await database.Seats.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.Vid == vid, cancellationToken)
                : null;
            if (seat is null)
            {
                return (BookingResult.NotFound, problems);
            }

            database.Seats.Remove(seat);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await TellAsync(row, vid, cancellationToken);

        return (BookingResult.Done, problems);
    }

    /// <summary>
    /// The mail of a registration as it is now — its answers and its turns —, in the member's language (E16's reading 12, every change); none
    /// when the registration is gone meanwhile.
    /// </summary>
    public async Task TellAsync(Event row, int vid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var registration = await database.Registrations.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.EventId == row.Id && candidate.Vid == vid, cancellationToken);
        if (registration is null)
        {
            return;
        }

        var questions = await database.Questions.AsNoTracking()
            .Where(question => question.EventId == row.Id)
            .OrderBy(question => question.Ordinal)
            .ThenBy(question => question.Id)
            .ToListAsync(cancellationToken);
        var answers = RegistrationAnswers.Of(registration);
        var seats = await RegistrationSeats.OfAsync(database, [row.Id], vid, cancellationToken);
        var fallback = division.Value.DefaultLocale;

        await mail.RegistrationReceivedAsync(
            row,
            vid,
            locale => RegistrationAnswers.Lines(questions, answers, locale, fallback, catalog),
            locale => EventsMail.SeatLines(seats, locale, fallback, catalog),
            cancellationToken);
    }

    /// <summary>A seat as its member reads it.</summary>
    public static MySeatDto Of(EventActivitySeat seat, EventActivity activity)
    {
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(activity);

        return new MySeatDto(seat.Id, activity.Id, activity.Name, activity.Place, seat.StartsAtUtc, seat.StartsAtUtc.AddMinutes(activity.SlotMinutes));
    }

    /// <summary>The event when the reader sees it (§2.1): an event not seen is not found, as its page is not.</summary>
    private Task<Event?> SeenAsync(long eventId, DateTime now, CancellationToken cancellationToken) =>
        database.Events.AsNoTracking().Where(EventState.Seen(now)).FirstOrDefaultAsync(row => row.Id == eventId, cancellationToken);
}

/// <summary>
/// The seats of a member's registration (E17): read with their activities, locked behind the registration, and taken away with it — when
/// the member withdraws, when the staff take the registration away, when the person's data is erased. One place, so that a seat never
/// outlives the registration it needs.
/// </summary>
public static class RegistrationSeats
{
    /// <summary>
    /// Locks the member's registration to the event (<c>SELECT … FOR UPDATE</c>), in the caller's transaction: what every change of the
    /// member's seats and of the registration itself waits on. False when they have none.
    /// </summary>
    public static async Task<bool> LockAsync(EventsDbContext database, long eventId, int vid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        return (await database.Database
                .SqlQuery<long>($"SELECT id AS `Value` FROM evt_registrations WHERE event_id = {eventId} AND vid = {vid} FOR UPDATE")
                .ToListAsync(cancellationToken))
            .Count > 0;
    }

    /// <summary>A member's seats in these events, each with its activity, past every filter: their own page, their mail, and the erasure.</summary>
    public static async Task<List<(EventActivitySeat Seat, EventActivity Activity)>> OfAsync(
        EventsDbContext database,
        IReadOnlyCollection<long> eventIds,
        int vid,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        return (await (
                    from seat in CrudSource.BackOffice<EventActivitySeat>(database).AsNoTracking()
                    join activity in CrudSource.BackOffice<EventActivity>(database).AsNoTracking() on seat.ActivityId equals activity.Id
                    where seat.Vid == vid && eventIds.Contains(seat.EventId)
                    orderby seat.StartsAtUtc, seat.Id
                    select new { seat, activity })
                .ToListAsync(cancellationToken))
            .Select(entry => (entry.seat, entry.activity))
            .ToList();
    }

    /// <summary>
    /// Takes the member's seats of the event away in the caller's unit of work, tracked, so that the audit keeps that each went: the
    /// registration they need is going too. Returns how many.
    /// </summary>
    public static async Task<int> RemoveAsync(EventsDbContext database, long eventId, int vid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var seats = await CrudSource.BackOffice<EventActivitySeat>(database)
            .Where(seat => seat.EventId == eventId && seat.Vid == vid)
            .ToListAsync(cancellationToken);
        database.Seats.RemoveRange(seats);

        return seats.Count;
    }
}
