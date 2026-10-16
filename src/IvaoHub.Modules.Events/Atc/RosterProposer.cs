using IvaoHub.Core.Ivao;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// A position of the roster as the proposer reads it: its hours, its kind as the network spells it, the least rating that may connect to it
/// (its FRAs on the network, through the core) and the rating that comes first on its kind (the division's, through the vocabulary).
/// </summary>
public sealed record RosterPosition(
    long Id,
    string Callsign,
    string? Type,
    DateTime FromUtc,
    DateTime ToUtc,
    AtcPositionMinimum Minimum,
    Rating? Preferred);

/// <summary>
/// The experience of a controller (§4.3): how many connections they opened in the last <c>experienceMonths</c>, by callsign and by kind of
/// position, both in any case.
/// </summary>
public sealed record RosterExperience(IReadOnlyDictionary<string, int> ByCallsign, IReadOnlyDictionary<string, int> ByType)
{
    public static RosterExperience None { get; } = new(
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// A controller who gave their availability (§4.2), as the proposer reads them: their ATC rating — the network's number —, their windows,
/// when they gave it, their experience (none when the archive did not answer: then nobody's counts) and their penalty (§4.5).
/// </summary>
public sealed record RosterCandidate(
    int Vid,
    int? Rating,
    IReadOnlyList<AtcWindowDto> Windows,
    DateTime AvailableSince,
    RosterExperience? Experience,
    double Penalty);

/// <summary>A shift somebody already holds: the proposer leaves its hours alone, and counts it for its controller.</summary>
public sealed record RosterHeld(long PositionId, int Vid, DateTime FromUtc, DateTime ToUtc);

/// <summary>The numbers of the roster (§1.12): the length of a shift, the least rating, the shifts in a row and the break after them.</summary>
public sealed record RosterRules(int ShiftMinutes, int? MinimumAtcRating, int MaxConsecutiveShifts, int BreakShifts);

/// <summary>A shift the proposer gives — to a controller with its why, or to nobody: uncovered, with how many could have taken it.</summary>
public sealed record ProposedShift(long PositionId, DateTime FromUtc, DateTime ToUtc, int? Vid, ShiftReason? Reason, int Candidates);

/// <summary>
/// The proposal of the roster (design M4 §4.3; note 2026-09-29-il-roster-atc §2.1, note 2026-10-13-la-proposta-del-roster, reading 3): pure
/// and deterministic — the same data, the same roster —, saying for every shift why it gave it to that person. No database, no clock: the
/// job reads what it needs and hands it here, and a test reads the rules without either.
/// <list type="number">
/// <item><b>The shifts</b>: every position from its opening, in pieces of the shift's length; the last one shorter where the position
/// closes.</item>
/// <item><b>Who can</b>: available for the whole shift, rated at least the setting's least and the position's minimum for those hours, in no
/// other shift that meets it, and not beyond the shifts in a row — two shifts are in a row when less than the break separates them.</item>
/// <item><b>Who first</b>: the preferred rating of the position's kind; the connections on its callsign, then on its kind; the lower
/// penalty; fewer shifts in this roster; the availability given earlier; the lower VID.</item>
/// <item><b>The order of the shifts</b>: those fewer can take first — the rules that do not depend on the other shifts —, then the earlier,
/// then the callsign, then the position.</item>
/// </list>
/// </summary>
public static class RosterProposer
{
    /// <summary>The weight of a no-show while the setting is not there (E13b brings <c>noShowWeight</c>; design §1.12: 1).</summary>
    public const double DefaultNoShowWeight = 1;

    /// <summary>
    /// The roster of these positions, in the order of the positions' callsigns and of the hours: every piece not held already, given to a
    /// controller or left uncovered.
    /// </summary>
    public static IReadOnlyList<ProposedShift> Propose(
        IReadOnlyList<RosterPosition> positions,
        IReadOnlyList<RosterCandidate> candidates,
        IReadOnlyList<RosterHeld> held,
        RosterRules rules,
        RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(vocabulary);

        // The experience counts only when it was read for everybody: never half a roster with it and half without (reading 3).
        var experienceKnown = candidates.All(candidate => candidate.Experience is not null);

        var busy = held.GroupBy(shift => shift.Vid).ToDictionary(group => group.Key, group => group.Select(shift => (shift.FromUtc, shift.ToUtc)).ToList());
        var pieces = positions
            .SelectMany(position => Pieces(position, rules.ShiftMinutes)
                .Where(piece => !held.Any(shift => shift.PositionId == position.Id && AtcRules.Meet(shift.FromUtc, shift.ToUtc, piece.FromUtc, piece.ToUtc)))
                .Select(piece => (Position: position, piece.FromUtc, piece.ToUtc, Able: candidates.Where(candidate => CanTake(candidate.Rating, candidate.Windows, position, piece.FromUtc, piece.ToUtc, rules, vocabulary)).ToList())))
            .OrderBy(piece => piece.Able.Count)
            .ThenBy(piece => piece.FromUtc)
            .ThenBy(piece => piece.Position.Callsign, StringComparer.Ordinal)
            .ThenBy(piece => piece.Position.Id)
            .ToList();

        var proposed = new List<ProposedShift>();
        foreach (var piece in pieces)
        {
            var free = piece.Able
                .Where(candidate => IsFree(busy.GetValueOrDefault(candidate.Vid) ?? [], piece.FromUtc, piece.ToUtc, rules))
                .Select(candidate => (Candidate: candidate, Key: KeyOf(candidate, piece.Position, busy, experienceKnown, vocabulary)))
                .OrderBy(entry => entry.Key)
                .ToList();

            if (free.Count == 0)
            {
                proposed.Add(new ProposedShift(piece.Position.Id, piece.FromUtc, piece.ToUtc, Vid: null, Reason: null, piece.Able.Count));
                continue;
            }

            var (chosen, key) = free[0];
            var decidedBy = free.Count == 1 ? ShiftCriterion.Only : key.DecidedAgainst(free[1].Key);
            var reason = new ShiftReason(
                piece.Able.Count,
                decidedBy,
                key.Preferred == 0,
                experienceKnown ? -key.CallsignConnections : null,
                experienceKnown ? -key.TypeConnections : null,
                chosen.Penalty,
                key.Shifts);

            proposed.Add(new ProposedShift(piece.Position.Id, piece.FromUtc, piece.ToUtc, chosen.Vid, reason, piece.Able.Count));
            if (!busy.TryGetValue(chosen.Vid, out var own))
            {
                busy[chosen.Vid] = own = [];
            }

            own.Add((piece.FromUtc, piece.ToUtc));
        }

        var callsigns = positions.ToDictionary(position => position.Id, position => position.Callsign);
        return
        [
            .. proposed
                .OrderBy(shift => callsigns[shift.PositionId], StringComparer.Ordinal)
                .ThenBy(shift => shift.PositionId)
                .ThenBy(shift => shift.FromUtc),
        ];
    }

    /// <summary>A position in pieces of <paramref name="shiftMinutes"/> from its opening; the last one ends where it closes.</summary>
    public static IReadOnlyList<(DateTime FromUtc, DateTime ToUtc)> Pieces(RosterPosition position, int shiftMinutes) =>
        Pieces(position?.FromUtc ?? throw new ArgumentNullException(nameof(position)), position.ToUtc, shiftMinutes);

    /// <summary>Hours in pieces of <paramref name="shiftMinutes"/> from their start; the last one ends where they end.</summary>
    public static IReadOnlyList<(DateTime FromUtc, DateTime ToUtc)> Pieces(DateTime fromUtc, DateTime toUtc, int shiftMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(shiftMinutes, 1);

        var pieces = new List<(DateTime, DateTime)>();
        for (var start = fromUtc; start < toUtc; start = start.AddMinutes(shiftMinutes))
        {
            var end = start.AddMinutes(shiftMinutes);
            pieces.Add((start, end < toUtc ? end : toUtc));
        }

        return pieces;
    }

    /// <summary>
    /// Whether a controller can take a shift by the rules that do not depend on the other shifts (§4.3, point 2): available for all of it,
    /// rated at least the setting's least and at least the position's minimum over those hours.
    /// </summary>
    public static bool CanTake(
        int? rating,
        IReadOnlyList<AtcWindowDto> windows,
        RosterPosition position,
        DateTime fromUtc,
        DateTime toUtc,
        RosterRules rules,
        RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(rules);

        return AtcRules.Cover(windows, fromUtc, toUtc) && IsRatedFor(rating, position, fromUtc, toUtc, rules, vocabulary);
    }

    /// <summary>Whether a rating is at least the setting's least and the position's minimum over these hours; no least, no limit.</summary>
    public static bool IsRatedFor(int? rating, RosterPosition position, DateTime fromUtc, DateTime toUtc, RosterRules rules, RatingVocabulary vocabulary) =>
        IsAtLeastSetting(rating, rules, vocabulary) && IsAtLeastPosition(rating, position, fromUtc, toUtc, vocabulary);

    public static bool IsAtLeastSetting(int? rating, RosterRules rules, RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(vocabulary);

        return rules.MinimumAtcRating is not { } least || vocabulary.IsAtLeast(RatingKind.Atc, rating, least);
    }

    public static bool IsAtLeastPosition(int? rating, RosterPosition position, DateTime fromUtc, DateTime toUtc, RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(vocabulary);

        return position.Minimum.Over(fromUtc, toUtc) is not { } minimum || vocabulary.IsAtLeast(RatingKind.Atc, rating, minimum);
    }

    /// <summary>Whether a rating is at least the one that comes first on the position's kind; a kind with none puts nobody first.</summary>
    public static bool IsPreferred(int? rating, RosterPosition position, RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(vocabulary);

        return position.Preferred is { } preferred && vocabulary.IsAtLeast(RatingKind.Atc, rating, preferred.Number);
    }

    /// <summary>
    /// Whether a controller with these shifts may take one more at these hours (§4.3, point 2): no shift of theirs meets it, and the run of
    /// shifts in a row it would belong to is no longer than the setting's.
    /// </summary>
    public static bool IsFree(IReadOnlyCollection<(DateTime FromUtc, DateTime ToUtc)> own, DateTime fromUtc, DateTime toUtc, RosterRules rules)
    {
        ArgumentNullException.ThrowIfNull(own);

        return !own.Any(shift => AtcRules.Meet(shift.FromUtc, shift.ToUtc, fromUtc, toUtc))
            && LongestRun([.. own, (fromUtc, toUtc)], rules, (fromUtc, toUtc)) <= rules.MaxConsecutiveShifts;
    }

    /// <summary>
    /// How many shifts in a row the run of <paramref name="of"/> counts among <paramref name="shifts"/>: two follow each other when less than
    /// <c>breakShifts</c> shifts of break separate them — a shorter pause is no break —, so with no break there is no run of more than one.
    /// </summary>
    public static int LongestRun(IReadOnlyCollection<(DateTime FromUtc, DateTime ToUtc)> shifts, RosterRules rules, (DateTime FromUtc, DateTime ToUtc) of)
    {
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(rules);

        var pause = TimeSpan.FromMinutes((double)rules.BreakShifts * rules.ShiftMinutes);
        var sorted = shifts.OrderBy(shift => shift.FromUtc).ThenBy(shift => shift.ToUtc).ToList();

        var run = new List<(DateTime FromUtc, DateTime ToUtc)>();
        foreach (var shift in sorted)
        {
            if (run.Count > 0 && shift.FromUtc - run.Max(previous => previous.ToUtc) >= pause)
            {
                if (run.Contains(of))
                {
                    return run.Count;
                }

                run.Clear();
            }

            run.Add(shift);
        }

        return run.Contains(of) ? run.Count : 0;
    }

    /// <summary>The penalty of reliability (§4.5): <c>weight × no-shows ÷ (attended + no-shows + 2)</c>.</summary>
    public static double Penalty(int noShows, int attended, double weight = DefaultNoShowWeight) =>
        weight * noShows / (attended + noShows + 2);

    private static Key KeyOf(
        RosterCandidate candidate,
        RosterPosition position,
        IReadOnlyDictionary<int, List<(DateTime FromUtc, DateTime ToUtc)>> busy,
        bool experienceKnown,
        RatingVocabulary vocabulary)
    {
        var experience = experienceKnown ? candidate.Experience! : RosterExperience.None;

        return new Key(
            IsPreferred(candidate.Rating, position, vocabulary) ? 0 : 1,
            -experience.ByCallsign.GetValueOrDefault(position.Callsign),
            -(position.Type is null ? 0 : experience.ByType.GetValueOrDefault(position.Type)),
            candidate.Penalty,
            busy.GetValueOrDefault(candidate.Vid)?.Count ?? 0,
            candidate.AvailableSince,
            candidate.Vid);
    }

    /// <summary>Who comes first, lowest first: each component one rule of §4.3, in its order.</summary>
    private readonly record struct Key(
        int Preferred,
        int CallsignConnections,
        int TypeConnections,
        double Penalty,
        int Shifts,
        DateTime AvailableSince,
        int Vid) : IComparable<Key>
    {
        public int CompareTo(Key other)
        {
            var preferred = Preferred.CompareTo(other.Preferred);
            if (preferred != 0)
            {
                return preferred;
            }

            var experience = (CallsignConnections, TypeConnections).CompareTo((other.CallsignConnections, other.TypeConnections));
            if (experience != 0)
            {
                return experience;
            }

            var penalty = Penalty.CompareTo(other.Penalty);
            if (penalty != 0)
            {
                return penalty;
            }

            var shifts = Shifts.CompareTo(other.Shifts);
            return shifts != 0 ? shifts
                : AvailableSince != other.AvailableSince ? AvailableSince.CompareTo(other.AvailableSince)
                : Vid.CompareTo(other.Vid);
        }

        /// <summary>The first rule this key wins on against the next one.</summary>
        public ShiftCriterion DecidedAgainst(Key next) =>
            Preferred != next.Preferred ? ShiftCriterion.Preferred
            : (CallsignConnections, TypeConnections) != (next.CallsignConnections, next.TypeConnections) ? ShiftCriterion.Experience
            : !Penalty.Equals(next.Penalty) ? ShiftCriterion.Penalty
            : Shifts != next.Shifts ? ShiftCriterion.FewerShifts
            : AvailableSince != next.AvailableSince ? ShiftCriterion.EarlierAvailability
            : ShiftCriterion.Vid;
    }
}
