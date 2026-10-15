using System.Data;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Public;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// A member's registrations to the events in person (design M4 §4-bis.2, E16): registering with the answers to the event's questions,
/// changing them, withdrawing, and reading their own — all of it on <c>/events/mine</c> and on the event's page. Any signed in member,
/// on an event they see; nobody else's registration, ever: the staff's side is <see cref="RegistrationEndpoints"/>' «take away».
/// <para>One registration per member per event is the unique index of the database: two requests of the same member in the same
/// instant meet there, and the second becomes a change of the registration the first made. Nothing else is shared between members, so
/// nothing is locked — but withdrawing, which takes the member's seats in the activities with it (E17), under the registration's lock.</para>
/// </summary>
public sealed class MemberRegistrations(
    EventsDbContext database,
    MemberSeats seats,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>
    /// Registers the member to an event, or changes their answers (§4-bis.2): an event in person they see, not cancelled, before its
    /// start; the answers checked by the type of each question, each required one present. The member is told what they answered
    /// (<see cref="EventsNotifications.RegistrationReceived"/>), once the registration is saved — at every change, so that the last
    /// mail says the answers as they are.
    /// </summary>
    public async Task<(BookingResult Result, MyRegistrationDto? Registration, bool Created, Refusals Problems)> SaveAsync(
        long eventId,
        RegistrationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Refusals();
        var now = clock.UtcNow;
        var row = await SeenAsync(eventId, now, cancellationToken);
        if (row is null || !row.InPerson)
        {
            return (BookingResult.NotFound, null, false, problems);
        }

        if (!RegistrationRules.IsOpen(row, now))
        {
            return (BookingResult.Refused, null, false, problems.Add("id", RegistrationRules.ClosedKey));
        }

        var questions = await QuestionsAsync(row.Id, cancellationToken);
        var answers = RegistrationAnswers.Check(questions, request.Answers, problems);
        if (!problems.IsEmpty)
        {
            return (BookingResult.Refused, null, false, problems);
        }

        var vid = currentUser.Vid;
        var registration = await MineAsync(row.Id, cancellationToken);
        var created = registration is null;

        if (registration is null)
        {
            // In the event's care: the departments and the scope of every row of the event (§1.1).
            registration = new EventRegistration
            {
                EventId = row.Id,
                Vid = vid,
                CreatedAt = now,
                OwnerDepartment = row.OwnerDepartment,
                OwnerDepartmentMask = row.OwnerDepartmentMask,
            };
            database.Registrations.Add(registration);
        }

        registration.AnswersJson = answers.ToJsonString();
        registration.UpdatedAt = now;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.Duplicated(exception))
        {
            // The same member registered from another window in the same instant, and the unique index answered: their registration is
            // there now, and this request is a change of it — the last answers sent are the ones kept.
            database.Entry(registration).State = EntityState.Detached;
            registration = await MineAsync(row.Id, cancellationToken);
            if (registration is null)
            {
                return (BookingResult.NotFound, null, false, problems);
            }

            created = false;
            registration.AnswersJson = answers.ToJsonString();
            registration.UpdatedAt = now;

            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (BookingResult.NotFound, null, false, problems);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            // Withdrawn from another window, or taken away by the staff, while this change was on its way.
            return (BookingResult.NotFound, null, false, problems);
        }
        // With the turns the member holds (E17): the mail says the registration as it is now.
        await seats.TellAsync(row, vid, cancellationToken);
        var held = await RegistrationSeats.OfAsync(database, [row.Id], vid, cancellationToken);

        return (BookingResult.Done, Mine(registration, row, [.. questions.Select(PublicQuestionDto.Of)], held, now), created, problems);
    }

    /// <summary>
    /// Withdraws the member from an event (§4-bis.2): until it starts — a cancelled one too. The row is deleted, as the core lets the
    /// member who sent it do (<c>[WithdrawnByStakeholder]</c>, E10h), loaded first so that the guard reads whose it is; the audit
    /// keeps who withdrew. Somebody else's registration is not found.
    /// <para>The member's seats in the activities go with it (E17): a seat needs a registration. Under the registration's lock
    /// (<see cref="RegistrationSeats.LockAsync"/>), so that a seat taken in the same instant is either seen here and goes, or refused there.</para>
    /// </summary>
    public async Task<(BookingResult Result, Refusals Problems)> WithdrawAsync(long eventId, CancellationToken cancellationToken)
    {
        var problems = new Refusals();
        var vid = currentUser.Vid;

        var row = await database.Events.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (!await RegistrationSeats.LockAsync(database, eventId, vid, cancellationToken)
            || await MineAsync(eventId, cancellationToken) is not { } registration)
        {
            return (BookingResult.NotFound, problems);
        }

        if (row is null || !RegistrationRules.IsWithdrawable(row, clock.UtcNow))
        {
            return (BookingResult.Refused, problems.Add("id", RegistrationRules.ClosedKey));
        }

        await RegistrationSeats.RemoveAsync(database, eventId, vid, cancellationToken);
        database.Registrations.Remove(registration);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (BookingResult.Done, problems);
    }

    /// <summary>The member's own registrations, past ones too, by the start of their events. A visitor has none.</summary>
    public async Task<IReadOnlyList<MyRegistrationDto>> MineAsync(CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var now = clock.UtcNow;

        var rows = await database.Registrations.AsNoTracking()
            .Where(registration => registration.Vid == vid)
            .Join(database.Events.AsNoTracking(), registration => registration.EventId, row => row.Id, (registration, row) => new { registration, row })
            .ToListAsync(cancellationToken);

        var eventIds = rows.Select(entry => entry.row.Id).Distinct().ToList();
        var questions = (await database.Questions.AsNoTracking()
                .Where(question => eventIds.Contains(question.EventId))
                .OrderBy(question => question.Ordinal)
                .ThenBy(question => question.Id)
                .ToListAsync(cancellationToken))
            .ToLookup(question => question.EventId);
        var seats = (await RegistrationSeats.OfAsync(database, eventIds, vid, cancellationToken)).ToLookup(entry => entry.Seat.EventId);

        return
        [
            .. rows
                .OrderBy(entry => entry.row.StartsAtUtc)
                .ThenBy(entry => entry.registration.Id)
                .Select(entry => Mine(
                    entry.registration,
                    entry.row,
                    [.. questions[entry.row.Id].Select(PublicQuestionDto.Of)],
                    [.. seats[entry.row.Id]],
                    now)),
        ];
    }

    /// <summary>The questions of an event, in the order of its form.</summary>
    public Task<List<EventQuestion>> QuestionsAsync(long eventId, CancellationToken cancellationToken) =>
        database.Questions.AsNoTracking()
            .Where(question => question.EventId == eventId)
            .OrderBy(question => question.Ordinal)
            .ThenBy(question => question.Id)
            .ToListAsync(cancellationToken);

    /// <summary>The event when the reader sees it (§2.1): an event not seen is not found, as its page is not.</summary>
    private Task<Event?> SeenAsync(long eventId, DateTime now, CancellationToken cancellationToken) =>
        database.Events.AsNoTracking().Where(EventState.Seen(now)).FirstOrDefaultAsync(row => row.Id == eventId, cancellationToken);

    /// <summary>The member's own registration of an event, tracked: the one their change writes, or none.</summary>
    private Task<EventRegistration?> MineAsync(long eventId, CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        return database.Registrations.FirstOrDefaultAsync(candidate => candidate.EventId == eventId && candidate.Vid == vid, cancellationToken);
    }

    private static MyRegistrationDto Mine(
        EventRegistration registration,
        Event row,
        IReadOnlyList<PublicQuestionDto> questions,
        IReadOnlyList<(EventActivitySeat Seat, EventActivity Activity)> seats,
        DateTime now) =>
        new(
            registration.Id,
            row.Id,
            row.Slug,
            row.Title,
            EventState.Of(row, now),
            row.StartsAtUtc,
            row.EndsAtUtc,
            row.Venue,
            questions,
            RegistrationAnswers.Of(registration),
            RegistrationRules.IsOpen(row, now),
            RegistrationRules.IsWithdrawable(row, now),
            registration.CreatedAt,
            registration.UpdatedAt,
            [.. seats.Select(entry => MemberSeats.Of(entry.Seat, entry.Activity))]);
}