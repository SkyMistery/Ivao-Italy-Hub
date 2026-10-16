using System.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// The publication of a roster and the mails of its shifts (design M4 §4.4, §8.4, E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione,
/// readings 1–3): the second half of <c>events-roster</c>, and what the correction asks after a save.
/// <list type="bullet">
/// <item><b>Publishing</b>: at the start less <c>rosterPublishDays</c>, an event published, with a roster proposed, not cancelled, not over, is
/// marked <c>roster_published_at</c>, once — under the lock of its row, read again under it, the mark written as the proposal's is, without the
/// row version moving and without a row in the audit. Once published it stays published.</item>
/// <item><b>Telling</b>: every shift of a published roster whose controller was not told yet (<c>notified_at</c> empty) is told, one mail per
/// controller per event with all of theirs — under the lock of the event's row, as a correction holds it, and of those shifts, the mark first
/// and the mail after it, as the reminder of the
/// bookings: a mail that fails to be queued is lost, never sent twice. A shift the correction failed to tell is told by the next run.</item>
/// </list>
/// </summary>
public sealed class RosterPublication(EventsDbContext database, EventsMail mail)
{
    /// <summary>When the roster of an event is published (§4.4): <c>rosterPublishDays</c> before its start.</summary>
    public static DateTime PublishesAt(Event row, EventsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(settings);

        return row.StartsAtUtc.AddDays(-settings.RosterPublishDays);
    }

    /// <summary>Whether an event's roster is to be published now: published, with a roster proposed, not cancelled, not over, its moment passed, never published.</summary>
    public static bool IsDue(Event row, EventsSettings settings, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row is { Status: PublishStatus.Published, HasRoster: true, CancelledAt: null, RosterPublishedAt: null, RosterProposedAt: not null }
            && PublishesAt(row, settings) <= now
            && !EventState.IsOver(row, now);
    }

    /// <summary>Publishes the roster of <paramref name="eventId"/> if it is due; false when it was not — published already, by another run among them.</summary>
    public async Task<bool> PublishAsync(long eventId, EventsSettings settings, DateTime now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var locked = await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {eventId} AND roster_published_at IS NULL FOR UPDATE")
            .ToListAsync(cancellationToken);
        var row = locked.Count == 0
            ? null
            : await CrudSource.BackOffice<Event>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        if (row is null || !IsDue(row, settings, now))
        {
            return false;
        }

        // The mark, with the row version written as it is so that it does not move (reading 1, as the proposal's).
        await database.Database.ExecuteSqlAsync(
            $"UPDATE evt_events SET roster_published_at = {now}, row_version = row_version WHERE id = {eventId} AND roster_published_at IS NULL",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Who is to be told of their shifts now: each controller with a shift not told yet in a published roster of an event not cancelled and
    /// not over, with the event — one mail each.
    /// </summary>
    public async Task<IReadOnlyList<(long EventId, int Vid)>> UntoldAsync(DateTime now, CancellationToken cancellationToken) =>
    [
        .. (await (
                from shift in CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
                join row in CrudSource.BackOffice<Event>(database).AsNoTracking() on shift.EventId equals row.Id
                where shift.NotifiedAt == null
                    && shift.ControllerVid > 0
                    && row.RosterPublishedAt != null
                    && row.CancelledAt == null
                    && row.EndsAtUtc > now
                select new { shift.EventId, shift.ControllerVid })
            .Distinct()
            .ToListAsync(cancellationToken))
        .Select(entry => (entry.EventId, entry.ControllerVid)),
    ];

    /// <summary>
    /// Tells one controller of their shifts of one event not told yet, in one mail (reading 2): the shifts locked and read again as committed,
    /// marked, and the mail queued once the mark is saved. Returns how many shifts it carries — none when another run, or the correction, told
    /// them first, or the roster is not to be told any more.
    /// </summary>
    public async Task<int> TellAsync(long eventId, int vid, DateTime now, CancellationToken cancellationToken)
    {
        Event? row;
        List<(string Callsign, DateTime FromUtc, DateTime ToUtc)> shifts;

        await using (var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            // The event's row first, the lock a correction of the roster holds (ShiftLock, E11b): a correction and this mail never cross —
            // the one that comes second reads what the first committed (the review of #268, point 1).
            await database.Database
                .SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {eventId} FOR UPDATE")
                .ToListAsync(cancellationToken);

            var locked = await database.Database
                .SqlQuery<long>($"SELECT id AS `Value` FROM evt_atc_shifts WHERE event_id = {eventId} AND controller_vid = {vid} AND notified_at IS NULL FOR UPDATE")
                .ToListAsync(cancellationToken);

            row = await CrudSource.BackOffice<Event>(database).AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == eventId && candidate.RosterPublishedAt != null && candidate.CancelledAt == null, cancellationToken);
            if (row is null || locked.Count == 0 || EventState.IsOver(row, now))
            {
                return 0;
            }

            shifts = await ShiftsAsync(locked, cancellationToken);

            // The job's bookkeeping alone, as the proposal's mark: no audit, and the row version of the shift does not move.
            await database.Database.ExecuteSqlAsync(
                $"UPDATE evt_atc_shifts SET notified_at = {now}, row_version = row_version WHERE event_id = {eventId} AND controller_vid = {vid} AND notified_at IS NULL",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await mail.AtcShiftAssignedAsync(row, vid, shifts, cancellationToken);
        return shifts.Count;
    }

    /// <summary>
    /// Tells the controller of one shift of a published roster, now — a shift the correction added or gave to somebody (reading 3) —: the shift
    /// is claimed (its mark written only if it was empty), and told once the claim is saved. False when it was told already, or the roster is
    /// not published.
    /// </summary>
    public async Task<bool> TellOneAsync(Event row, long shiftId, DateTime now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.RosterPublishedAt is null || row.CancelledAt is not null)
        {
            return false;
        }

        var claimed = await database.Database.ExecuteSqlAsync(
            $"UPDATE evt_atc_shifts SET notified_at = {now}, row_version = row_version WHERE id = {shiftId} AND notified_at IS NULL",
            cancellationToken);
        if (claimed == 0)
        {
            return false;
        }

        var shift = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking().FirstAsync(candidate => candidate.Id == shiftId, cancellationToken);
        await mail.AtcShiftAssignedAsync(row, shift.ControllerVid, await ShiftsAsync([shift.Id], cancellationToken), cancellationToken);
        return true;
    }

    /// <summary>These shifts as a mail names them: the callsign of their position and their hours, by their start.</summary>
    public async Task<List<(string Callsign, DateTime FromUtc, DateTime ToUtc)>> ShiftsAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken) =>
    [
        .. (await (
                from shift in CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
                join position in database.AtcPositions.AsNoTracking() on shift.PositionId equals position.Id
                where ids.Contains(shift.Id)
                orderby shift.FromUtc
                select new { position.Callsign, shift.FromUtc, shift.ToUtc })
            .ToListAsync(cancellationToken))
        .Select(entry => (entry.Callsign, entry.FromUtc, entry.ToUtc)),
    ];
}
