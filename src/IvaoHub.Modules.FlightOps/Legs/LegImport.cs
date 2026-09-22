using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>
/// What an import does with the legs the file does not contain (design M2 §8.4, ADR-051 of Toursystem): "merge" leaves
/// them where they are, "replace" deletes those without reports and retires those with reports (§1.4.1).
/// </summary>
public enum LegImportMode
{
    Merge,
    Replace,
}

/// <summary>
/// One row of the file, as the browser read it (T8, Carmine 22 September 2026: the file is read in the browser). The
/// file names aircraft types only: the groups of a leg are the editor's, and a leg the file matches keeps its own.
/// </summary>
public sealed record LegImportRowDto(
    string DepartureIcao,
    string ArrivalIcao,
    string? RealCallsign,
    string? FlightNumber,
    IReadOnlyList<string>? AircraftTypes,
    DateTime? ReleaseAt);

/// <summary>
/// The rows of the file, in their order — which is the order of the tour — and what to do with the legs it does not
/// contain. <c>Reason</c> is owed when the import retires, restores or changes a leg with reports; <c>Fingerprint</c>
/// is the one the preview answered, so that what is applied is what was looked at.
/// </summary>
public sealed record LegImportRequest(
    IReadOnlyList<LegImportRowDto> Rows,
    LegImportMode Mode,
    string? Reason,
    string? Fingerprint);

/// <summary>What the import does to one leg.</summary>
public enum LegImportOutcome
{
    /// <summary>A row of the file no leg matches: a new leg.</summary>
    Added,

    /// <summary>The same leg, with a callsign, a flight number, aircraft types or a release of the file's.</summary>
    Changed,

    /// <summary>The same leg, as it is.</summary>
    Unchanged,

    /// <summary>A retired leg the file names again: back in the tour (ADR-051, Carmine 22 September 2026).</summary>
    Restored,

    /// <summary>Not in the file, and left alone: "merge", or a leg already retired.</summary>
    Kept,

    /// <summary>Not in the file, without reports, on "replace": deleted.</summary>
    Deleted,

    /// <summary>Not in the file, with reports, on "replace": retired with the import's reason.</summary>
    Retired,
}

/// <summary>
/// One line of the preview: the outcome, the row of the file (from 0) if there is one, the leg if there is one, its
/// number now and after, and the fields that change.
/// </summary>
public sealed record LegImportLineDto(
    LegImportOutcome Outcome,
    int? Row,
    long? LegId,
    int? NumberBefore,
    int? NumberAfter,
    string DepartureIcao,
    string ArrivalIcao,
    IReadOnlyList<string> Changes,
    bool HasReports);

/// <summary>The differences, computed by the server and written nowhere; the fingerprint the apply must carry back.</summary>
public sealed record LegImportPreviewDto(
    IReadOnlyList<LegImportLineDto> Lines,
    bool ReasonRequired,
    string Fingerprint);

/// <summary>
/// The comparison of the file with the legs of the tour, and nothing else: a pure function, unit tested (T8).
/// <para><b>The same leg</b> is the same departure and arrival; when the pair is in the tour more than once, they are
/// paired in their order (Carmine, 22 September 2026: no column of numbers — the order of the rows is the order of the
/// tour). A row no leg matches is a new leg, in its place.</para>
/// <para><b>The order after</b> is the file's; a leg the file does not contain and that stays — every one on "merge",
/// the retired ones on "replace" — keeps its place after the leg that came before it.</para>
/// </summary>
public sealed class LegImportPlan
{
    private LegImportPlan(IReadOnlyList<Step> steps) => Steps = steps;

    /// <summary>Every leg of the tour after the import, in its order, deleted ones last with no number.</summary>
    public IReadOnlyList<Step> Steps { get; }

    public bool ReasonRequired => Steps.Any(step =>
        step.Outcome is LegImportOutcome.Retired or LegImportOutcome.Restored
        || (step.Outcome == LegImportOutcome.Changed && step.HasReports));

    public bool Restores => Steps.Any(step => step.Outcome == LegImportOutcome.Restored);

    /// <summary>
    /// What happens to every leg. <paramref name="incoming"/> are the rows of the file already made legs by the server
    /// (airports frozen, values normalised), in the file's order.
    /// </summary>
    public static LegImportPlan Make(
        IReadOnlyList<Leg> existing,
        IReadOnlyList<Leg> incoming,
        LegImportMode mode,
        IReadOnlySet<long> withReports)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(withReports);

        var ordered = existing.OrderBy(leg => leg.Number).ThenBy(leg => leg.Id).ToList();

        // The pairs of the tour, each a queue in the order of the legs: a row takes the first one still free.
        var byPair = ordered
            .GroupBy(Pair)
            .ToDictionary(group => group.Key, group => new Queue<Leg>(group), StringComparer.Ordinal);

        var matched = new Leg?[incoming.Count];
        for (var row = 0; row < incoming.Count; row++)
        {
            if (byPair.TryGetValue(Pair(incoming[row]), out var free) && free.Count > 0)
            {
                matched[row] = free.Dequeue();
            }
        }

        var taken = matched.OfType<Leg>().ToHashSet();

        // A leg the file does not contain hangs after the last leg before it that the file does contain.
        var hanging = new Dictionary<Leg, List<Leg>>();
        var atStart = new List<Leg>();
        var gone = new List<Step>();
        Leg? anchor = null;
        foreach (var leg in ordered)
        {
            if (taken.Contains(leg))
            {
                anchor = leg;
                continue;
            }

            var reports = withReports.Contains(leg.Id);
            if (mode == LegImportMode.Replace && !reports)
            {
                gone.Add(new Step(LegImportOutcome.Deleted, null, leg, null, [], false));
                continue;
            }

            if (anchor is null)
            {
                atStart.Add(leg);
            }
            else
            {
                (hanging.TryGetValue(anchor, out var list) ? list : hanging[anchor] = []).Add(leg);
            }
        }

        var steps = new List<Step>();
        steps.AddRange(atStart.Select(leg => Absent(leg, mode, withReports)));

        for (var row = 0; row < incoming.Count; row++)
        {
            var file = incoming[row];
            if (matched[row] is not { } leg)
            {
                steps.Add(new Step(LegImportOutcome.Added, row, file, file, [], false));
                continue;
            }

            var changes = Changes(leg, file);
            var outcome = leg.IsRetired
                ? LegImportOutcome.Restored
                : changes.Count > 0 ? LegImportOutcome.Changed : LegImportOutcome.Unchanged;
            steps.Add(new Step(outcome, row, leg, file, changes, withReports.Contains(leg.Id)));

            if (hanging.TryGetValue(leg, out var after))
            {
                steps.AddRange(after.Select(absent => Absent(absent, mode, withReports)));
            }
        }

        var number = 1;
        foreach (var step in steps)
        {
            step.NumberAfter = number++;
        }

        steps.AddRange(gone);

        return new LegImportPlan(steps);
    }

    /// <summary>What the preview shows.</summary>
    public IReadOnlyList<LegImportLineDto> Lines() =>
    [
        .. Steps.Select(step => new LegImportLineDto(
            step.Outcome,
            step.Row,
            step.Outcome == LegImportOutcome.Added ? null : step.Leg.Id,
            step.Outcome == LegImportOutcome.Added ? null : step.Leg.Number,
            step.NumberAfter,
            step.Leg.DepartureIcao,
            step.Leg.ArrivalIcao,
            step.Changes,
            step.HasReports)),
    ];

    /// <summary>
    /// The plan on the legs: the file's values on the legs it matches (their groups kept), the retired ones it names
    /// back, the absent ones deleted or retired, every number the plan's. Returns the new legs, to be added, and the
    /// deleted ones, to be removed; the caller owns the context.
    /// </summary>
    public (IReadOnlyList<Leg> Added, IReadOnlyList<Leg> Deleted) Apply(DateTime now, string? reason)
    {
        var added = new List<Leg>();
        var deleted = new List<Leg>();

        foreach (var step in Steps)
        {
            var leg = step.Leg;
            switch (step.Outcome)
            {
                case LegImportOutcome.Added:
                    added.Add(leg);
                    break;
                case LegImportOutcome.Changed or LegImportOutcome.Restored:
                    Take(leg, step.File!);
                    if (step.Outcome == LegImportOutcome.Restored)
                    {
                        leg.RetiredAt = null;
                        leg.RetiredReason = null;
                    }

                    leg.ChangeReason = step.HasReports || step.Outcome == LegImportOutcome.Restored ? reason : null;
                    break;
                case LegImportOutcome.Retired:
                    leg.RetiredAt = now;
                    leg.RetiredReason = reason;
                    break;
                case LegImportOutcome.Deleted:
                    deleted.Add(leg);
                    break;
            }

            if (step.NumberAfter is { } number && leg.Number != number)
            {
                leg.Number = number;
            }
        }

        return (added, deleted);
    }

    /// <summary>
    /// The state of the legs the preview looked at: every leg and its version. The apply carries it back, and a tour
    /// whose legs changed in between is a 409, not an import of something nobody saw.
    /// </summary>
    public static string Fingerprint(IEnumerable<Leg> legs)
    {
        ArgumentNullException.ThrowIfNull(legs);

        var text = string.Join(';', legs.OrderBy(leg => leg.Id).Select(leg => $"{leg.Id}:{leg.RowVersion.Ticks}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    private static Step Absent(Leg leg, LegImportMode mode, IReadOnlySet<long> withReports)
    {
        var reports = withReports.Contains(leg.Id);
        var outcome = mode == LegImportMode.Replace && reports && !leg.IsRetired
            ? LegImportOutcome.Retired
            : LegImportOutcome.Kept;

        return new Step(outcome, null, leg, null, [], reports);
    }

    private static string Pair(Leg leg) => $"{leg.DepartureIcao}>{leg.ArrivalIcao}";

    /// <summary>The fields of the file that differ from the leg's, by the names of the payload.</summary>
    private static List<string> Changes(Leg leg, Leg file)
    {
        var changes = new List<string>();
        if (leg.RealCallsign != file.RealCallsign)
        {
            changes.Add("realCallsign");
        }

        if (leg.FlightNumber != file.FlightNumber)
        {
            changes.Add("flightNumber");
        }

        if (!leg.Aircraft.Types.SequenceEqual(file.Aircraft.Types, StringComparer.Ordinal))
        {
            changes.Add("aircraft");
        }

        if (leg.ReleaseAt != file.ReleaseAt)
        {
            changes.Add("releaseAt");
        }

        return changes;
    }

    private static void Take(Leg leg, Leg file)
    {
        leg.RealCallsign = file.RealCallsign;
        leg.FlightNumber = file.FlightNumber;
        leg.Aircraft = new AllowedAircraft(file.Aircraft.Types, leg.Aircraft.GroupIds);
        leg.ReleaseAt = file.ReleaseAt;
    }

    /// <summary>One leg in the plan: the tour's (or the file's, when added), and the row of the file that names it.</summary>
    public sealed class Step(LegImportOutcome outcome, int? row, Leg leg, Leg? file, IReadOnlyList<string> changes, bool hasReports)
    {
        public LegImportOutcome Outcome { get; } = outcome;

        public int? Row { get; } = row;

        public Leg Leg { get; } = leg;

        public Leg? File { get; } = file;

        public IReadOnlyList<string> Changes { get; } = changes;

        public bool HasReports { get; } = hasReports;

        public int? NumberAfter { get; internal set; }
    }
}

/// <summary>The limits of one request: rows and the reason. What each row says is checked row by row by the server.</summary>
public sealed class LegImportRequestValidator : AbstractValidator<LegImportRequest>
{
    /// <summary>A tour of the division has tens of legs; a thousand rows is a file of something else.</summary>
    public const int MaxRows = 1000;

    public LegImportRequestValidator()
    {
        RuleFor(request => request.Rows).NotNull().WithMessage("errors.required");
        RuleFor(request => request.Rows.Count).LessThanOrEqualTo(MaxRows).When(request => request.Rows is not null)
            .OverridePropertyName("rows").WithMessage("flightops:errors.importTooManyRows");
        RuleFor(request => request.Mode).IsInEnum().WithMessage("errors.required");
        RuleFor(request => request.Reason).MaximumLength(LegValidation.MaxReasonLength).WithMessage("errors.text.tooLong");
    }
}
