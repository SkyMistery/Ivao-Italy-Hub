using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.People;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>What a row of the roster is: a piece nobody holds, a shift the job proposed, or one the staff wrote.</summary>
public enum RosterRowState
{
    Uncovered,
    Proposed,
    Staff,
}

/// <summary>
/// What a shift does not keep of the rules of the proposal (design M4 §4.3, §4.4: «an assignment outside the rules is a warning, not a
/// refusal»): not available for the whole of it — or never available —, below the setting's least rating, below the position's minimum for
/// those hours (the core's words: lift the FRA on the network), below the preferred rating of its kind, in the same hours as another shift
/// of the controller — of another event: in one event the correction refuses it (Carmine's answer on #259) —, beyond the shifts in a row.
/// </summary>
public enum ShiftWarning
{
    NotAvailable,
    BelowMinimum,
    BelowPositionMinimum,
    BelowPreferred,
    Overlapping,
    TooManyInARow,
}

/// <summary>
/// One row of the roster as the staff read it (§4.4, §7.2, E11b), by position and hour: the position, the hours, what the row is, and for a
/// shift its controller (named by the core's reader), their ATC rating as the staff say it, its why and its warnings; how many could take it
/// by the rules that do not depend on the other shifts, and how many of them are free for it — for an uncovered piece, the reason it is (the
/// review of #262, point 3); and whether the reader may write it — add on an uncovered piece, move and take away a shift —, as the one
/// handler answers on the row (point 5).
/// </summary>
public sealed record RosterRowDto(
    long PositionId,
    string Callsign,
    string? Fir,
    DateTime FromUtc,
    DateTime ToUtc,
    RosterRowState State,
    long? ShiftId,
    EventMemberDto? Controller,
    string? Rating,
    ShiftReason? Reason,
    IReadOnlyList<ShiftWarning> Warnings,
    int Candidates,
    int Free,
    bool Editable);

/// <summary>
/// The roster of an event as the staff of the ATC read it (§4.4, §7.2; note 2026-10-13-la-proposta-del-roster, readings 2, 6 and 7): every
/// shift, and every piece of a position that no shift meets — uncovered —, by callsign and hour. The pieces are the positions in pieces of
/// the shift's length (<see cref="RosterProposer.Pieces(DateTime, DateTime, int)"/>), so a position opened, moved or taken away after the
/// proposal says its uncovered ones as well. The rules are the proposer's own: a warning is what the proposer would not have done.
/// </summary>
public sealed class StaffRoster(EventsDbContext database, HubDbContext hub, RosterData data, RatingVocabulary vocabulary)
{
    /// <summary>
    /// The roster of these positions of an event — the ones the reader may read —, in the order of the callsigns and the hours, each row
    /// with what <paramref name="mayWrite"/> answers for it: a shift as it is, an uncovered piece as a shift on it would be.
    /// </summary>
    public async Task<IReadOnlyList<RosterRowDto>> RowsAsync(
        Event row,
        IReadOnlyList<EventAtcPosition> positions,
        EventsSettings settings,
        Func<EventAtcShift, Task<bool>> mayWrite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(mayWrite);

        var rules = RosterData.Rules(row, settings);
        var shifts = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
            .Where(shift => shift.EventId == row.Id)
            .ToListAsync(cancellationToken);
        var availabilities = (await data.AvailabilitiesAsync(row.Id, cancellationToken)).ToDictionary(availability => availability.Vid);

        // The controllers' shifts of the other events in this one's hours: a warning, never a refusal (Carmine's answer on #259).
        var controllers = shifts.Select(shift => shift.ControllerVid).Distinct().ToList();
        var elsewhere = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
            .Where(shift => shift.EventId != row.Id
                && controllers.Contains(shift.ControllerVid)
                && shift.FromUtc < row.EndsAtUtc
                && row.StartsAtUtc < shift.ToUtc)
            .ToListAsync(cancellationToken);

        var vids = shifts.Select(shift => shift.ControllerVid).Concat(availabilities.Keys).Distinct().ToList();
        var ratings = await data.RatingsAsync(vids, cancellationToken);
        var names = await MemberNames.ReadAsync(hub, shifts.Select(shift => shift.ControllerVid), cancellationToken);
        int? RatingOf(int vid) => ratings.TryGetValue(vid, out var rating) ? rating : null;
        List<(DateTime FromUtc, DateTime ToUtc)> HeldBy(int vid) =>
            [.. shifts.Where(shift => shift.ControllerVid == vid).Select(shift => (shift.FromUtc, shift.ToUtc))];

        var rows = new List<RosterRowDto>();
        foreach (var position in await data.PositionsAsync(positions, cancellationToken))
        {
            var stored = positions.First(candidate => candidate.Id == position.Id);
            var own = shifts.Where(shift => shift.PositionId == position.Id).ToList();
            List<RosterAvailability> Able(DateTime fromUtc, DateTime toUtc) =>
                [.. availabilities.Values.Where(availability => RosterProposer.CanTake(RatingOf(availability.Vid), availability.Windows, position, fromUtc, toUtc, rules, vocabulary))];

            foreach (var shift in own)
            {
                var rating = RatingOf(shift.ControllerVid);
                rows.Add(new RosterRowDto(
                    position.Id,
                    position.Callsign,
                    stored.Fir,
                    shift.FromUtc,
                    shift.ToUtc,
                    shift.Origin == ShiftOrigin.Proposed ? RosterRowState.Proposed : RosterRowState.Staff,
                    shift.Id,
                    EventsPeople.Member(shift.ControllerVid, names),
                    vocabulary.Find(RatingKind.Atc, rating)?.ShortName,
                    shift.Reason,
                    Warnings(shift, rating, availabilities.GetValueOrDefault(shift.ControllerVid)?.Windows ?? [], position, [.. shifts, .. elsewhere], rules, vocabulary),
                    Able(shift.FromUtc, shift.ToUtc).Count,
                    Free: 0,
                    await mayWrite(shift)));
            }

            foreach (var (fromUtc, toUtc) in RosterProposer.Pieces(position, rules.ShiftMinutes))
            {
                foreach (var gap in Gaps(fromUtc, toUtc, own))
                {
                    var able = Able(gap.FromUtc, gap.ToUtc);
                    var probe = new EventAtcShift
                    {
                        EventId = row.Id,
                        PositionId = position.Id,
                        Fir = stored.Fir,
                        FromUtc = gap.FromUtc,
                        ToUtc = gap.ToUtc,
                        OwnerDepartment = stored.OwnerDepartment,
                        OwnerDepartmentMask = stored.OwnerDepartmentMask,
                    };

                    rows.Add(new RosterRowDto(
                        position.Id,
                        position.Callsign,
                        stored.Fir,
                        gap.FromUtc,
                        gap.ToUtc,
                        RosterRowState.Uncovered,
                        ShiftId: null,
                        Controller: null,
                        Rating: null,
                        Reason: null,
                        Warnings: [],
                        able.Count,
                        able.Count(availability => RosterProposer.IsFree(HeldBy(availability.Vid), gap.FromUtc, gap.ToUtc, rules)),
                        await mayWrite(probe)));
                }
            }
        }

        return
        [
            .. rows
                .OrderBy(entry => entry.Callsign, StringComparer.Ordinal)
                .ThenBy(entry => entry.PositionId)
                .ThenBy(entry => entry.FromUtc),
        ];
    }

    /// <summary>
    /// The warnings of a shift (§4.4): each rule of the proposal it does not keep, read on the roster as it is now — so a proposed shift whose
    /// position's FRA changed since says so too. Pure, so that a test reads them without a database.
    /// </summary>
    public static IReadOnlyList<ShiftWarning> Warnings(
        EventAtcShift shift,
        int? rating,
        IReadOnlyList<AtcWindowDto> windows,
        RosterPosition position,
        IReadOnlyList<EventAtcShift> roster,
        RosterRules rules,
        RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(shift);
        ArgumentNullException.ThrowIfNull(roster);

        var warnings = new List<ShiftWarning>();
        if (!AtcRules.Cover(windows, shift.FromUtc, shift.ToUtc))
        {
            warnings.Add(ShiftWarning.NotAvailable);
        }

        if (!RosterProposer.IsAtLeastSetting(rating, rules, vocabulary))
        {
            warnings.Add(ShiftWarning.BelowMinimum);
        }

        if (!RosterProposer.IsAtLeastPosition(rating, position, shift.FromUtc, shift.ToUtc, vocabulary))
        {
            warnings.Add(ShiftWarning.BelowPositionMinimum);
        }

        if (position.Preferred is not null && !RosterProposer.IsPreferred(rating, position, vocabulary))
        {
            warnings.Add(ShiftWarning.BelowPreferred);
        }

        // Another shift of theirs at the same hours — of another event, since in one event the correction refuses it (Carmine's answer on
        // #259) —; the shifts in a row are counted in this event's roster.
        var others = roster.Where(other => other.ControllerVid == shift.ControllerVid && other.Id != shift.Id).ToList();
        var theirs = others.Where(other => other.EventId == shift.EventId).Select(other => (other.FromUtc, other.ToUtc)).ToList();
        if (others.Any(other => AtcRules.Meet(other.FromUtc, other.ToUtc, shift.FromUtc, shift.ToUtc)))
        {
            warnings.Add(ShiftWarning.Overlapping);
        }
        else if (RosterProposer.LongestRun([.. theirs, (shift.FromUtc, shift.ToUtc)], rules, (shift.FromUtc, shift.ToUtc)) > rules.MaxConsecutiveShifts)
        {
            warnings.Add(ShiftWarning.TooManyInARow);
        }

        return warnings;
    }

    /// <summary>The parts of a piece of a position that none of its shifts meets, in their order.</summary>
    public static IReadOnlyList<(DateTime FromUtc, DateTime ToUtc)> Gaps(DateTime fromUtc, DateTime toUtc, IEnumerable<EventAtcShift> shifts)
    {
        ArgumentNullException.ThrowIfNull(shifts);

        var gaps = new List<(DateTime, DateTime)>();
        var reached = fromUtc;
        foreach (var shift in shifts.Where(shift => AtcRules.Meet(shift.FromUtc, shift.ToUtc, fromUtc, toUtc)).OrderBy(shift => shift.FromUtc))
        {
            if (shift.FromUtc > reached)
            {
                gaps.Add((reached, shift.FromUtc));
            }

            if (shift.ToUtc > reached)
            {
                reached = shift.ToUtc;
            }
        }

        if (reached < toUtc)
        {
            gaps.Add((reached, toUtc));
        }

        return gaps;
    }
}
