using System.Data;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.People;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>What a verb on a request to hand a shift over came to: done, refused field by field, nothing there, or changed meanwhile — read it again.</summary>
public enum TransferResult
{
    Done,
    Refused,
    NotFound,
    Changed,
    Forbidden,
}

/// <summary>What a controller sends to hand a shift over (§4.4-bis): to whom — none, they look for a substitute — and why.</summary>
public sealed record TransferRequest(int? ToVid, string? Note);

/// <summary>
/// A request to hand a shift over, as its controller reads it under the shift (§4.4-bis): to whom — named by the core's reader —, its
/// state, the notes, and when it was asked and decided.
/// </summary>
public sealed record MyTransferDto(
    long Id,
    EventMemberDto? To,
    TransferStatus Status,
    string? Note,
    string? DecisionNote,
    DateTime CreatedAt,
    DateTime? DecidedAt);

/// <summary>
/// A shift as its controller reads it on <c>/events/mine</c> and in the block <c>events.myEvents</c> (§4.4, §7.1, E12): the event, the
/// position's callsign, the hours, whether it may be handed over now — a published roster, before the event's start, not cancelled, no
/// request waiting —, and the last request to hand it over that is still about them: waiting, or refused.
/// </summary>
public sealed record MyShiftDto(
    long Id,
    long EventId,
    string EventSlug,
    Localized<string> EventTitle,
    EventStateKind EventState,
    string Callsign,
    DateTime FromUtc,
    DateTime ToUtc,
    bool Transferable,
    MyTransferDto? Transfer);

/// <summary>
/// A controller's shifts (design M4 §4.4, §4.4-bis, §7.1, E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, readings 6 and 7):
/// reading their own — of the published rosters only: a shift of a roster not published is not theirs to read yet —, asking to hand one over
/// and taking the request back. Any signed in member, on their own shifts alone.
/// <para>A request is a row of the member (<see cref="EventAtcTransfer"/>): created by them, changed by them only to take it back — its state,
/// never deleted, since it carries the staff's decision —, under the lock of its shift, so one request waits per shift.</para>
/// </summary>
public sealed class MemberShifts(EventsDbContext database, HubDbContext hub, ICurrentUser currentUser, IClock clock)
{
    /// <summary>A shift is handed over until the start of its event, on a published roster of an event not cancelled.</summary>
    public const string ClosedKey = "events:errors.transferClosed";

    /// <summary>A request is waiting already for this shift.</summary>
    public const string PendingKey = "events:errors.transferPending";

    /// <summary>A shift handed to its own controller.</summary>
    public const string ToYourselfKey = "events:errors.transferToYourself";

    /// <summary>A receiver who never signed in, as the correction says it (E11b).</summary>
    public const string NeverSignedInKey = "events:errors.controllerNeverSignedIn";

    /// <summary>Whether a shift of this event may be handed over now (reading 7): a published roster, not cancelled, before the start.</summary>
    public static bool IsTransferable(Event row, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.RosterPublishedAt is not null && row.CancelledAt is null && now < row.StartsAtUtc;
    }

    /// <summary>The member's own shifts of the published rosters, past ones too, by their start.</summary>
    public async Task<IReadOnlyList<MyShiftDto>> MineAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return [];
        }

        var vid = currentUser.Vid;
        var now = clock.UtcNow;

        // Their own rows, every one: the events read past who may see them now, as their availabilities are.
        var rows = await (
                from shift in CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
                join row in CrudSource.BackOffice<Event>(database).AsNoTracking() on shift.EventId equals row.Id
                join position in database.AtcPositions.AsNoTracking() on shift.PositionId equals position.Id
                where shift.ControllerVid == vid && row.RosterPublishedAt != null
                select new { shift, row, position.Callsign })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(entry => entry.shift.Id).ToList();
        var requests = (await CrudSource.BackOffice<EventAtcTransfer>(database).AsNoTracking()
                .Where(transfer => ids.Contains(transfer.ShiftId)
                    && transfer.ControllerVid == vid
                    && (transfer.Status == TransferStatus.Pending || transfer.Status == TransferStatus.Refused))
                .ToListAsync(cancellationToken))
            .GroupBy(transfer => transfer.ShiftId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(transfer => transfer.CreatedAt).ThenByDescending(transfer => transfer.Id).First());
        var names = await MemberNames.ReadAsync(hub, requests.Values.Select(transfer => transfer.ToVid), cancellationToken);

        return
        [
            .. rows
                .OrderBy(entry => entry.shift.FromUtc)
                .ThenBy(entry => entry.shift.Id)
                .Select(entry =>
                {
                    var request = requests.GetValueOrDefault(entry.shift.Id);
                    return new MyShiftDto(
                        entry.shift.Id,
                        entry.row.Id,
                        entry.row.Slug,
                        entry.row.Title,
                        EventState.Of(entry.row, now),
                        entry.Callsign,
                        entry.shift.FromUtc,
                        entry.shift.ToUtc,
                        IsTransferable(entry.row, now) && request?.Status != TransferStatus.Pending,
                        request is null ? null : Mine(request, names));
                }),
        ];
    }

    /// <summary>
    /// Asks to hand one of their shifts over (reading 7): their own shift, of a published roster, before its event's start, not cancelled,
    /// with no request waiting; to a member who signed in once and is not them, or to nobody. The shift stays theirs until the staff decide.
    /// </summary>
    public async Task<(TransferResult Result, MyTransferDto? Transfer, Refusals Problems)> AskAsync(
        long shiftId,
        TransferRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Refusals();
        var vid = currentUser.Vid;
        var now = clock.UtcNow;

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > EventAtcTransfer.MaxNoteLength)
        {
            problems.Add("note", "errors.text.tooLong");
        }

        if (request.ToVid is { } to)
        {
            if (to == vid)
            {
                problems.Add("toVid", ToYourselfKey);
            }
            else if (to <= 0 || !await hub.Users.AsNoTracking().AnyAsync(user => user.Vid == to, cancellationToken))
            {
                problems.Add("toVid", NeverSignedInKey);
            }
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // The shift locked: two requests of the same controller at once meet here, and the second reads the first's.
        var locked = await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_atc_shifts WHERE id = {shiftId} AND controller_vid = {vid} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var shift = locked.Count == 0
            ? null
            : await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == shiftId, cancellationToken);
        var row = shift is null
            ? null
            : await CrudSource.BackOffice<Event>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == shift.EventId, cancellationToken);
        if (shift is null || row?.RosterPublishedAt is null)
        {
            return (TransferResult.NotFound, null, problems);
        }

        if (!IsTransferable(row, now))
        {
            return (TransferResult.Refused, null, problems.Add("id", ClosedKey));
        }

        if (await CrudSource.BackOffice<EventAtcTransfer>(database).AnyAsync(
                transfer => transfer.ShiftId == shiftId && transfer.Status == TransferStatus.Pending,
                cancellationToken))
        {
            return (TransferResult.Refused, null, problems.Add("id", PendingKey));
        }

        if (!problems.IsEmpty)
        {
            return (TransferResult.Refused, null, problems);
        }

        // In the event's care and the shift's FIR, as every row of the ATC of the event (§1.1).
        var transfer = new EventAtcTransfer
        {
            EventId = row.Id,
            ShiftId = shift.Id,
            ControllerVid = vid,
            ToVid = request.ToVid,
            Fir = shift.Fir,
            Note = note,
            Status = TransferStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            OwnerDepartment = row.OwnerDepartment,
            OwnerDepartmentMask = row.OwnerDepartmentMask,
        };
        database.AtcTransfers.Add(transfer);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (TransferResult.Done, Mine(transfer, await MemberNames.ReadAsync(hub, [transfer.ToVid], cancellationToken)), problems);
    }

    /// <summary>
    /// Takes back the request waiting for one of their shifts (reading 7): its state becomes <see cref="TransferStatus.Withdrawn"/>, never
    /// deleted, and the audit keeps who did what. Nothing waiting is not found; one decided meanwhile is a 409.
    /// </summary>
    public async Task<TransferResult> WithdrawAsync(long shiftId, CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var transfer = await CrudSource.BackOffice<EventAtcTransfer>(database).FirstOrDefaultAsync(
            candidate => candidate.ShiftId == shiftId && candidate.ControllerVid == vid && candidate.Status == TransferStatus.Pending,
            cancellationToken);
        if (transfer is null)
        {
            return TransferResult.NotFound;
        }

        transfer.Status = TransferStatus.Withdrawn;
        transfer.UpdatedAt = clock.UtcNow;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Decided by the staff a moment before: they read it again.
            database.ChangeTracker.Clear();
            return TransferResult.Changed;
        }

        return TransferResult.Done;
    }

    private static MyTransferDto Mine(EventAtcTransfer transfer, IReadOnlyDictionary<int, string> names) => new(
        transfer.Id,
        transfer.ToVid is { } to ? EventsPeople.Member(to, names) : null,
        transfer.Status,
        transfer.Note,
        transfer.DecisionNote,
        transfer.CreatedAt,
        transfer.DecidedAt);
}
