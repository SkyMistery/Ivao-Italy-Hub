using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// What a correction of a published roster tells its controllers, at once (design M4 §4.4, E12; note
/// 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, reading 3), from the verbs of the CRUD engine — never a verb of its own: the save
/// says what the shift was (<see cref="Note"/>, before it is written), and once it is done the controllers are told (<see cref="TellAsync"/>).
/// <list type="bullet">
/// <item>added: <c>atcShiftAssigned</c>, and the shift's mark;</item>
/// <item>other hours or another position, the same controller: <c>atcShiftChanged</c> with how it was and how it is, if they knew of it —
/// otherwise <c>atcShiftAssigned</c> with the shift as it is;</item>
/// <item>given to another controller: <c>atcShiftRemoved</c> to whoever held it, if they knew of it, and <c>atcShiftAssigned</c> to the new
/// one — the mark emptied with the change and written again for them;</item>
/// <item>taken away (<see cref="RemoveAsync"/>, with its position too): <c>atcShiftRemoved</c>, if they knew of it.</item>
/// </list>
/// Before the publication nobody is told: the controllers learn their shifts when it comes. One per request, as the engine's save is.
/// </summary>
public sealed class ShiftNotices(EventsDbContext database, RosterPublication publication, EventsMail mail, IClock clock)
{
    private Before? _before;

    /// <summary>
    /// What the shift was before this save, read from the tracker; and, when it passes to another controller, its mark emptied, so that the new
    /// one is told. Nothing for a new shift.
    /// </summary>
    public void Note(EventAtcShift shift, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(shift);

        if (isNew)
        {
            _before = null;
            return;
        }

        var entry = database.Entry(shift);
        _before = new Before(
            (int)entry.Property(nameof(EventAtcShift.ControllerVid)).OriginalValue!,
            (long)entry.Property(nameof(EventAtcShift.PositionId)).OriginalValue!,
            (DateTime)entry.Property(nameof(EventAtcShift.FromUtc)).OriginalValue!,
            (DateTime)entry.Property(nameof(EventAtcShift.ToUtc)).OriginalValue!,
            entry.Property(nameof(EventAtcShift.NotifiedAt)).OriginalValue is not null);

        if (_before.ControllerVid != shift.ControllerVid)
        {
            shift.NotifiedAt = null;
        }
    }

    /// <summary>Tells, once the save is done, what <see cref="Note"/> read and the shift is now.</summary>
    public async Task TellAsync(EventAtcShift shift, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shift);

        var before = _before;
        _before = null;
        if (await PublishedAsync(shift.EventId, cancellationToken) is not { } row)
        {
            return;
        }

        var now = clock.UtcNow;
        if (before is null)
        {
            await publication.TellOneAsync(row, shift.Id, now, cancellationToken);
            return;
        }

        if (before.ControllerVid != shift.ControllerVid)
        {
            if (before.Told)
            {
                await mail.AtcShiftRemovedAsync(row, before.ControllerVid, await DescribeAsync(before.PositionId, before.FromUtc, before.ToUtc, cancellationToken), cancellationToken);
            }

            await publication.TellOneAsync(row, shift.Id, now, cancellationToken);
            return;
        }

        if (before.PositionId == shift.PositionId && before.FromUtc == shift.FromUtc && before.ToUtc == shift.ToUtc)
        {
            return;
        }

        if (before.Told)
        {
            await mail.AtcShiftChangedAsync(
                row,
                shift.ControllerVid,
                await DescribeAsync(before.PositionId, before.FromUtc, before.ToUtc, cancellationToken),
                await DescribeAsync(shift.PositionId, shift.FromUtc, shift.ToUtc, cancellationToken),
                cancellationToken);
        }
        else
        {
            await publication.TellOneAsync(row, shift.Id, now, cancellationToken);
        }
    }

    /// <summary>
    /// Takes these shifts away with their requests to hand them over — each with its row in the audit —, saves, and tells whoever knew of one
    /// that it is gone: «take away» on the roster, and a position deleted with its shifts — the position in the same save, under the lock of
    /// the event's row the deletion holds (<paramref name="held"/>), committed before anybody is told. The engine's own save after it finds
    /// nothing left.
    /// </summary>
    public async Task RemoveAsync(
        IReadOnlyList<EventAtcShift> shifts,
        EventAtcPosition? position,
        ShiftLock? held,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shifts);

        var told = new List<(long EventId, int Vid, long PositionId, DateTime FromUtc, DateTime ToUtc)>();
        foreach (var shift in shifts)
        {
            if (shift.NotifiedAt is not null)
            {
                told.Add((shift.EventId, shift.ControllerVid, shift.PositionId, shift.FromUtc, shift.ToUtc));
            }
        }

        var described = new List<(long EventId, int Vid, (string, DateTime, DateTime) Shift)>();
        foreach (var gone in told)
        {
            described.Add((gone.EventId, gone.Vid, await DescribeAsync(gone.PositionId, gone.FromUtc, gone.ToUtc, cancellationToken)));
        }

        var ids = shifts.Select(shift => shift.Id).ToList();
        database.AtcTransfers.RemoveRange(await CrudSource.BackOffice<EventAtcTransfer>(database)
            .Where(transfer => ids.Contains(transfer.ShiftId))
            .ToListAsync(cancellationToken));
        database.AtcShifts.RemoveRange(shifts);
        if (position is not null)
        {
            database.AtcPositions.Remove(position);
        }

        await database.SaveChangesAsync(cancellationToken);
        if (held is not null)
        {
            await held.CommitAsync(cancellationToken);
        }

        foreach (var (eventId, vid, shift) in described)
        {
            if (await PublishedAsync(eventId, cancellationToken) is { } row)
            {
                await mail.AtcShiftRemovedAsync(row, vid, shift, cancellationToken);
            }
        }
    }

    /// <summary>The event, when its roster is published and it is not cancelled: only then is anybody told.</summary>
    private Task<Event?> PublishedAsync(long eventId, CancellationToken cancellationToken) =>
        CrudSource.BackOffice<Event>(database).AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == eventId && row.RosterPublishedAt != null && row.CancelledAt == null, cancellationToken);

    private async Task<(string Callsign, DateTime FromUtc, DateTime ToUtc)> DescribeAsync(long positionId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var callsign = await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking()
            .Where(position => position.Id == positionId)
            .Select(position => position.Callsign)
            .FirstOrDefaultAsync(cancellationToken);

        return (callsign ?? string.Empty, fromUtc, toUtc);
    }

    /// <summary>What a shift was before a save: its controller, its position, its hours, and whether its controller knew of it.</summary>
    private sealed record Before(int ControllerVid, long PositionId, DateTime FromUtc, DateTime ToUtc, bool Told);
}
