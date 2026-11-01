using System.Data;
using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.People;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// A request to hand a shift over as the staff of the ATC list it (§4.4-bis, §7.2, E12): the shift — its callsign and hours —, who hands it
/// over and to whom, named by the core's reader, the note, the state and the decision; for one waiting, whether its shift is still theirs
/// (<c>Stale</c> when the staff gave it to somebody else meanwhile), the warnings of the rules of the proposal on whoever would take it,
/// and whether the reader may decide it — the one handler asked on it, so never their own (<c>Decidable</c>, as the roster's <c>Editable</c>).
/// </summary>
public sealed record EventAtcTransferDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    long ShiftId,
    string Callsign,
    DateTime FromUtc,
    DateTime ToUtc,
    EventMemberDto From,
    EventMemberDto? To,
    string? Note,
    TransferStatus Status,
    EventMemberDto? DecidedBy,
    DateTime? DecidedAt,
    string? DecisionNote,
    DateTime CreatedAt,
    bool Stale,
    IReadOnlyList<ShiftWarning> Warnings,
    bool Decidable);

/// <summary>What the staff send to approve a request (§4.4-bis): who takes the shift — the one named, when empty — and a note.</summary>
public sealed record TransferApproval(int? ToVid, string? Note);

/// <summary>What the staff send to refuse a request: why, which whoever asked reads in a mail.</summary>
public sealed record TransferRefusal(string? Note);

/// <summary>
/// The staff's side of handing a shift over (design M4 §4.4-bis, E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, reading 7):
/// the list of the requests with their warnings, and the two decisions.
/// <list type="bullet">
/// <item><b>Approving</b>: <c>EventAtc.Edit</c> asked of the one handler on the request — denied to whoever hands over, the super administrator
/// included — and on the shift as it becomes — denied to its new controller, so nobody of the staff gives themselves a shift —; until the
/// event's start (Carmine's answer on issue #259); the shift still theirs; the new holder the one named, or another the staff choose — needed
/// for a request to nobody —, who signed in once and holds no other position of the event in those hours. The shift passes, its why goes
/// (the audit keeps who held it), and whoever asked and whoever takes it are told.</item>
/// <item><b>Refusing</b>: the same permission on the request, with a note, at any time while it waits — after the start too, so that a request
/// left waiting is closed —; whoever asked is told.</item>
/// </list>
/// Both under the lock of the event's row, as the correction of the roster is, so a decision and a correction of the same shift meet there.
/// </summary>
public sealed class StaffTransfers(
    EventsDbContext database,
    HubDbContext hub,
    EventChildren children,
    RosterData data,
    RosterPublication publication,
    EventsMail mail,
    ModuleSettingsStore settingsStore,
    RatingVocabulary vocabulary,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>A request decided already, or taken back.</summary>
    public const string NotPendingKey = "events:errors.transferNotPending";

    /// <summary>A request whose shift the staff gave to somebody else meanwhile.</summary>
    public const string NotTheirsKey = "events:errors.transferShiftNotTheirs";

    /// <summary>A request to nobody approved without naming who takes the shift.</summary>
    public const string SubstituteKey = "events:errors.transferNeedsSubstitute";

    /// <summary>From the event's start a shift is not given to another controller (E11b's word for the correction).</summary>
    public const string AfterStartKey = "events:errors.shiftAfterStart";

    /// <summary>A cancelled event, as the correction says it.</summary>
    public const string CancelledKey = "events:errors.alreadyCancelled";

    /// <summary>A controller who holds another position of the event in those hours (E11b's word for the correction).</summary>
    public const string BusyKey = "events:errors.controllerBusy";

    /// <summary>A page of the list, with the names in one query and the warnings read once per event.</summary>
    public async Task<IReadOnlyList<EventAtcTransferDto>> RowsAsync(IReadOnlyList<EventAtcTransfer> transfers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transfers);

        var shiftIds = transfers.Select(transfer => transfer.ShiftId).Distinct().ToList();
        var shifts = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
            .Where(shift => shiftIds.Contains(shift.Id))
            .ToDictionaryAsync(shift => shift.Id, cancellationToken);
        var positionIds = shifts.Values.Select(shift => shift.PositionId).Distinct().ToList();
        var positions = await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking()
            .Where(position => positionIds.Contains(position.Id))
            .ToDictionaryAsync(position => position.Id, cancellationToken);
        var names = await MemberNames.ReadAsync(
            hub,
            transfers.SelectMany(transfer => new int?[] { transfer.ControllerVid, transfer.ToVid, transfer.DecidedBy }),
            cancellationToken);

        var warnings = new Dictionary<long, IReadOnlyList<ShiftWarning>>();
        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        // Read once per event — its roster, its availabilities, the ratings and the positions of its requests (the second reading of #268,
        // point 3) —, then the warnings of each request on what was read.
        foreach (var group in transfers.Where(transfer => transfer.Status == TransferStatus.Pending && transfer.ToVid is not null).GroupBy(transfer => transfer.EventId))
        {
            if (await children.EventAsync(group.Key, cancellationToken) is not { } row)
            {
                continue;
            }

            var read = await ReadForWarningsAsync(
                row,
                [.. group.Select(transfer => transfer.ToVid!.Value)],
                [.. group.Select(transfer => shifts.GetValueOrDefault(transfer.ShiftId)?.PositionId).OfType<long>().Distinct().Select(id => positions[id])],
                settings,
                cancellationToken);

            foreach (var transfer in group)
            {
                if (shifts.GetValueOrDefault(transfer.ShiftId) is { } shift)
                {
                    warnings[transfer.Id] = read.Warnings(shift, transfer.ToVid!.Value, vocabulary);
                }
            }
        }

        var decidable = new HashSet<long>();
        foreach (var transfer in transfers.Where(transfer => transfer.Status == TransferStatus.Pending))
        {
            if (http.HttpContext is { } context && (await authorization.AuthorizeAsync(context.User, transfer, EventsPermissions.AtcEdit)).Succeeded)
            {
                decidable.Add(transfer.Id);
            }
        }

        return [.. transfers.Select(transfer => Row(transfer, shifts.GetValueOrDefault(transfer.ShiftId), positions, names, warnings.GetValueOrDefault(transfer.Id) ?? [], decidable.Contains(transfer.Id)))];
    }

    /// <summary>
    /// The warnings of the rules of the proposal (§4.3) on a member the staff choose to give a request's shift to (Carmine's answer on #268,
    /// reading 7: approving towards another member than the one asked, provided the warnings are shown for the holder chosen): the approval's
    /// dialog asks them before it approves. Null when the request is not found, or the reader may not decide it.
    /// </summary>
    public async Task<IReadOnlyList<ShiftWarning>?> WarningsForAsync(long id, int toVid, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var transfer = await CrudSource.BackOffice<EventAtcTransfer>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (transfer is null || !(await authorization.AuthorizeAsync(user, transfer, EventsPermissions.AtcEdit)).Succeeded)
        {
            return null;
        }

        var shift = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == transfer.ShiftId, cancellationToken);
        var position = shift is null
            ? null
            : await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == shift.PositionId, cancellationToken);
        if (shift is null || position is null || await children.EventAsync(transfer.EventId, cancellationToken) is not { } row)
        {
            return null;
        }

        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        return (await ReadForWarningsAsync(row, [toVid], [position], settings, cancellationToken)).Warnings(shift, toVid, vocabulary);
    }

    /// <summary>What the warnings of the holders of an event's requests are read from, once for the event.</summary>
    private async Task<WarningData> ReadForWarningsAsync(
        Event row,
        IReadOnlyCollection<int> vids,
        IReadOnlyList<EventAtcPosition> positions,
        EventsSettings settings,
        CancellationToken cancellationToken) =>
        new(
            await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking().Where(other => other.EventId == row.Id).ToListAsync(cancellationToken),
            (await data.AvailabilitiesAsync(row.Id, cancellationToken)).ToDictionary(availability => availability.Vid),
            await data.RatingsAsync(vids, cancellationToken),
            (await data.PositionsAsync(positions, cancellationToken)).ToDictionary(position => position.Id),
            RosterData.Rules(row, settings));

    /// <summary>
    /// An event's roster, availabilities, ratings and positions, read once: what a shift would not keep of the rules of the proposal in the
    /// hands of a member — the roster's own warnings.
    /// </summary>
    private sealed record WarningData(
        IReadOnlyList<EventAtcShift> Roster,
        IReadOnlyDictionary<int, RosterAvailability> Availabilities,
        IReadOnlyDictionary<int, int> Ratings,
        IReadOnlyDictionary<long, RosterPosition> Positions,
        RosterRules Rules)
    {
        public IReadOnlyList<ShiftWarning> Warnings(EventAtcShift shift, int vid, RatingVocabulary vocabulary) =>
            Positions.TryGetValue(shift.PositionId, out var position)
                ? StaffRoster.Warnings(
                    Handed(shift, vid),
                    Ratings.TryGetValue(vid, out var rating) ? rating : null,
                    Availabilities.GetValueOrDefault(vid)?.Windows ?? [],
                    position,
                    Roster,
                    Rules,
                    vocabulary)
                : [];
    }

    /// <summary>One request alone, with no names nor warnings: a row the engine reads by itself.</summary>
    public static EventAtcTransferDto Row(
        EventAtcTransfer transfer,
        EventAtcShift? shift,
        IReadOnlyDictionary<long, EventAtcPosition> positions,
        IReadOnlyDictionary<int, string> names,
        IReadOnlyList<ShiftWarning> warnings,
        bool decidable = false)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        ArgumentNullException.ThrowIfNull(positions);

        return new EventAtcTransferDto(
            transfer.Id,
            transfer.EventId,
            transfer.OwnerDepartment,
            transfer.ShiftId,
            shift is not null && positions.TryGetValue(shift.PositionId, out var position) ? position.Callsign : string.Empty,
            shift?.FromUtc ?? default,
            shift?.ToUtc ?? default,
            EventsPeople.Member(transfer.ControllerVid, names),
            transfer.ToVid is { } to ? EventsPeople.Member(to, names) : null,
            transfer.Note,
            transfer.Status,
            transfer.DecidedBy is { } by ? EventsPeople.Member(by, names) : null,
            transfer.DecidedAt,
            transfer.DecisionNote,
            transfer.CreatedAt,
            transfer.Status == TransferStatus.Pending && shift?.ControllerVid != transfer.ControllerVid,
            warnings,
            decidable);
    }

    /// <summary>Approves a request (reading 7): the shift passes to the new holder, and whoever asked and whoever takes it are told.</summary>
    public async Task<(TransferResult Result, Refusals Problems)> ApproveAsync(
        long id,
        TransferApproval approval,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);

        var problems = new Refusals();
        var note = Note(approval.Note, problems);
        var now = clock.UtcNow;

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var (result, transfer, row) = await ReadAsync(id, user, cancellationToken);
        if (result is not TransferResult.Done)
        {
            return (result, result == TransferResult.Refused ? problems.Add("id", NotPendingKey) : problems);
        }

        var shift = await CrudSource.BackOffice<EventAtcShift>(database).FirstAsync(candidate => candidate.Id == transfer!.ShiftId, cancellationToken);
        var holder = transfer!.ControllerVid;
        if (shift.ControllerVid != holder)
        {
            return (TransferResult.Refused, problems.Add("id", NotTheirsKey));
        }

        // A cancelled event hands nothing over (the review of #268, point 4): its requests are refused only.
        if (row!.CancelledAt is not null)
        {
            return (TransferResult.Refused, problems.Add("id", CancelledKey));
        }

        if (now >= row.StartsAtUtc)
        {
            return (TransferResult.Refused, problems.Add("id", AfterStartKey));
        }

        if ((approval.ToVid ?? transfer.ToVid) is not { } to)
        {
            return (TransferResult.Refused, problems.Add("toVid", SubstituteKey));
        }

        if (to == holder)
        {
            problems.Add("toVid", MemberShifts.ToYourselfKey);
        }
        else if (to <= 0 || !await hub.Users.AsNoTracking().AnyAsync(user => user.Vid == to, cancellationToken))
        {
            problems.Add("toVid", MemberShifts.NeverSignedInKey);
        }
        else if (await CrudSource.BackOffice<EventAtcShift>(database).AnyAsync(
                     other => other.EventId == shift.EventId
                         && other.Id != shift.Id
                         && other.ControllerVid == to
                         && other.FromUtc < shift.ToUtc
                         && shift.FromUtc < other.ToUtc,
                     cancellationToken))
        {
            // Two positions of one event at the same hours cannot be one controller's (Carmine's answer on #259).
            problems.Add("toVid", BusyKey);
        }

        if (!problems.IsEmpty)
        {
            return (TransferResult.Refused, problems);
        }

        // The shift as it becomes: the one handler asked on it, so that nobody of the staff gives themselves a shift.
        var before = (shift.FromUtc, shift.ToUtc);
        shift.ControllerVid = to;
        shift.Origin = ShiftOrigin.Staff;
        shift.Reason = null;
        shift.NotifiedAt = null;
        if (!(await authorization.AuthorizeAsync(user, shift, EventsPermissions.AtcEdit)).Succeeded)
        {
            return (TransferResult.Forbidden, problems);
        }

        transfer.ToVid = to;
        Decide(transfer, TransferStatus.Approved, note, now);

        if (await SaveAsync(transaction, cancellationToken) is { } changed)
        {
            return (changed, problems);
        }

        // Who took the shift, named as the hub names them, with the VID (the review of #268, point 7): it may be another than the one asked.
        var callsign = (await publication.ShiftsAsync([shift.Id], cancellationToken))[0].Callsign;
        var taker = (await MemberNames.ReadAsync(hub, [to], cancellationToken)).GetValueOrDefault(to) is { Length: > 0 } name
            ? $"{name} ({to.ToString(System.Globalization.CultureInfo.InvariantCulture)})"
            : to.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await mail.AtcShiftTransferDecidedAsync(row, holder, (callsign, before.FromUtc, before.ToUtc), (to, taker), note, cancellationToken);
        await publication.TellOneAsync(row, shift.Id, now, cancellationToken);

        return (TransferResult.Done, problems);
    }

    /// <summary>Refuses a request (reading 7), with a note: the shift stays its controller's, and they are told why.</summary>
    public async Task<(TransferResult Result, Refusals Problems)> RefuseAsync(
        long id,
        TransferRefusal refusal,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refusal);

        var problems = new Refusals();
        var note = Note(refusal.Note, problems);
        if (note is null && problems.IsEmpty)
        {
            problems.Add("note", "errors.required");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var (result, transfer, row) = await ReadAsync(id, user, cancellationToken);
        if (result is not TransferResult.Done)
        {
            return (result, result == TransferResult.Refused ? new Refusals().Add("id", NotPendingKey) : problems);
        }

        if (!problems.IsEmpty)
        {
            return (TransferResult.Refused, problems);
        }

        Decide(transfer!, TransferStatus.Refused, note, clock.UtcNow);
        if (await SaveAsync(transaction, cancellationToken) is { } changed)
        {
            return (changed, problems);
        }

        var shift = (await publication.ShiftsAsync([transfer!.ShiftId], cancellationToken))[0];
        await mail.AtcShiftTransferDecidedAsync(row!, transfer.ControllerVid, shift, taker: null, note, cancellationToken);

        return (TransferResult.Done, problems);
    }

    /// <summary>
    /// The request under the lock of its event's row, in its event's care as it is now, with <c>EventAtc.Edit</c> asked of the one handler on
    /// it — never its controller's —, and still waiting.
    /// </summary>
    private async Task<(TransferResult Result, EventAtcTransfer? Transfer, Event? Row)> ReadAsync(long id, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var eventId = await CrudSource.BackOffice<EventAtcTransfer>(database).AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => (long?)candidate.EventId)
            .FirstOrDefaultAsync(cancellationToken);
        if (eventId is null)
        {
            return (TransferResult.NotFound, null, null);
        }

        await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {eventId.Value} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var transfer = await CrudSource.BackOffice<EventAtcTransfer>(database).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (transfer is null || (await children.AdoptAsync(transfer, cancellationToken)).Event is not { } row)
        {
            return (TransferResult.NotFound, null, null);
        }

        if (!(await authorization.AuthorizeAsync(user, transfer, EventsPermissions.AtcEdit)).Succeeded)
        {
            return (TransferResult.Forbidden, null, null);
        }

        return transfer.Status == TransferStatus.Pending
            ? (TransferResult.Done, transfer, row)
            : (TransferResult.Refused, null, null);
    }

    private void Decide(EventAtcTransfer transfer, TransferStatus status, string? note, DateTime now)
    {
        transfer.Status = status;
        transfer.DecidedBy = currentUser.Vid;
        transfer.DecidedAt = now;
        transfer.DecisionNote = note;
        transfer.UpdatedAt = now;
    }

    /// <summary>The save and the commit; a 409 when the controller took the request back, or somebody changed the shift, a moment before.</summary>
    private async Task<TransferResult?> SaveAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            return TransferResult.Changed;
        }
    }

    private static string? Note(string? text, Refusals problems)
    {
        var note = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (note?.Length > EventAtcTransfer.MaxNoteLength)
        {
            problems.Add("note", "errors.text.tooLong");
        }

        return note;
    }

    /// <summary>The shift as it would be in the hands of <paramref name="vid"/>: what its warnings are read on.</summary>
    private static EventAtcShift Handed(EventAtcShift shift, int vid) => new()
    {
        Id = shift.Id,
        EventId = shift.EventId,
        PositionId = shift.PositionId,
        ControllerVid = vid,
        Fir = shift.Fir,
        FromUtc = shift.FromUtc,
        ToUtc = shift.ToUtc,
        Origin = ShiftOrigin.Staff,
    };
}
