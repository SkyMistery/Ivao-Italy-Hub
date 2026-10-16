using System.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// Proposes the roster of one event, once (design M4 §4.3, §8.4, E11b; note 2026-10-13-la-proposta-del-roster, reading 5): its data read
/// (<see cref="RosterData"/>), the proposal made (<see cref="RosterProposer"/>), the shifts written with their why, and
/// <c>roster_proposed_at</c> written with them, in one transaction under the lock of the event's row — read again under it, so two runs,
/// or two processes, never propose it twice.
/// <para>The mark is the job's bookkeeping: written without the row version of the event moving, so an editor open on the event does not
/// get a 409 for it, and without a row in the audit of the event; the shifts have theirs, written by nobody (a job).</para>
/// </summary>
public sealed class RosterProposal(EventsDbContext database, RosterData data, RatingVocabulary vocabulary)
{
    /// <summary>
    /// Whether an event's roster is due (§4.3): published — nobody gave availability to a draft —, with a roster, not cancelled, not over,
    /// its availabilities closed, never proposed.
    /// </summary>
    public static bool IsDue(Event row, EventsSettings settings, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row is { Status: PublishStatus.Published, HasRoster: true, CancelledAt: null, RosterProposedAt: null }
            && AtcRules.ClosesAt(row, settings) <= now
            && !EventState.IsOver(row, now);
    }

    /// <summary>
    /// The roster of <paramref name="eventId"/>, proposed now if it is due: the shifts written, or null when it was not due — proposed
    /// already, by another run a moment before among them.
    /// </summary>
    public async Task<int?> ProposeAsync(long eventId, EventsSettings settings, DateTime now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // The archive answers one controller at a time, and outside the lock: the controllers are fixed since the close.
        var controllers = (await data.AvailabilitiesAsync(eventId, cancellationToken)).Select(availability => availability.Vid).ToList();
        var experience = await data.ExperienceAsync(controllers, settings.ExperienceMonths, now, cancellationToken);

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var locked = await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {eventId} AND roster_proposed_at IS NULL FOR UPDATE")
            .ToListAsync(cancellationToken);
        var row = locked.Count == 0
            ? null
            : await CrudSource.BackOffice<Event>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        if (row is null || !IsDue(row, settings, now))
        {
            return null;
        }

        var positions = await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking()
            .Where(position => position.EventId == eventId)
            .ToListAsync(cancellationToken);
        var availabilities = await data.AvailabilitiesAsync(eventId, cancellationToken);
        var vids = availabilities.Select(availability => availability.Vid).ToList();
        var ratings = await data.RatingsAsync(vids, cancellationToken);
        var penalties = await data.PenaltiesAsync(vids, eventId, cancellationToken);
        var held = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
            .Where(shift => shift.EventId == eventId)
            .Select(shift => new RosterHeld(shift.PositionId, shift.ControllerVid, shift.FromUtc, shift.ToUtc))
            .ToListAsync(cancellationToken);

        var proposed = RosterProposer.Propose(
            await data.PositionsAsync(positions, cancellationToken),
            [
                .. availabilities.Select(availability => new RosterCandidate(
                    availability.Vid,
                    ratings.TryGetValue(availability.Vid, out var rating) ? rating : null,
                    availability.Windows,
                    availability.Since,
                    experience?.GetValueOrDefault(availability.Vid) ?? (experience is null ? null : RosterExperience.None),
                    penalties.GetValueOrDefault(availability.Vid))),
            ],
            held,
            RosterData.Rules(row, settings),
            vocabulary);

        var byId = positions.ToDictionary(position => position.Id);
        var shifts = proposed
            .Where(shift => shift.Vid is not null)
            .Select(shift => new EventAtcShift
            {
                EventId = eventId,
                PositionId = shift.PositionId,
                ControllerVid = shift.Vid!.Value,
                Fir = byId[shift.PositionId].Fir,
                FromUtc = shift.FromUtc,
                ToUtc = shift.ToUtc,
                Origin = ShiftOrigin.Proposed,
                Reason = shift.Reason,
                OwnerDepartment = row.OwnerDepartment,
                OwnerDepartmentMask = row.OwnerDepartmentMask,
            })
            .ToList();

        database.AtcShifts.AddRange(shifts);
        await database.SaveChangesAsync(cancellationToken);

        // The mark, with the row version written as it is so that it does not move (reading 5).
        await database.Database.ExecuteSqlAsync(
            $"UPDATE evt_events SET roster_proposed_at = {now}, row_version = row_version WHERE id = {eventId} AND roster_proposed_at IS NULL",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return shifts.Count;
    }
}
