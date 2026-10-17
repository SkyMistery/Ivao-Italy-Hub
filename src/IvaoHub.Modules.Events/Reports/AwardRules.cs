using IvaoHub.Core.Data.Crud;

namespace IvaoHub.Modules.Events.Reports;

/// <summary>The parameters a criterion takes (design M4 §1.9): the columns of a rule it reads, the others left empty.</summary>
[Flags]
public enum AwardParameters
{
    None = 0,
    Route = 1,
    Count = 2,
    Distance = 4,
    Minutes = 8,
    PositionType = 16,
}

/// <summary>
/// The rules of award that need nothing but their values (design M4 §1.9, §5.4, E14b; note 2026-10-17-le-regole-di-award): which kind of
/// report a criterion reads, which parameters it takes and what they may be, and whether a report's <b>verified</b> items meet it. Pure, and
/// tested criterion by criterion; <see cref="ReportDecisions"/> reads the rows and the kinds of the positions.
/// </summary>
public static class AwardRules
{
    /// <summary>The most a count may ask: more flights than a report holds is a rule nobody meets.</summary>
    public const int MaxCount = ReportRules.MaxItems;

    /// <summary>The longest leg a rule may ask, in nautical miles: half the world.</summary>
    public const int MaxDistanceNm = 10_800;

    /// <summary>The most minutes a rule may ask: every shift of a report at its longest.</summary>
    public const int MaxMinutes = 24 * 60;

    public const string CriterionKey = "events:errors.awardRuleCriterion";

    public const string AwardUnknownKey = "events:errors.awardRuleAwardUnknown";

    public const string TooManyKey = "events:errors.awardRulesTooMany";

    /// <summary>The kind of report a criterion reads: a pilot's flights, or a controller's shifts.</summary>
    public static ReportKind KindOf(AwardCriterion criterion) =>
        criterion is AwardCriterion.AnyShift or AwardCriterion.MinAtcMinutes ? ReportKind.Atc : ReportKind.Pilot;

    /// <summary>The parameters a criterion takes; <see cref="AwardParameters.PositionType"/> is the only optional one.</summary>
    public static AwardParameters ParametersOf(AwardCriterion criterion) => criterion switch
    {
        AwardCriterion.MinFlights => AwardParameters.Count,
        AwardCriterion.Route => AwardParameters.Route,
        AwardCriterion.RouteTimes => AwardParameters.Route | AwardParameters.Count,
        AwardCriterion.MinLegDistance => AwardParameters.Distance,
        AwardCriterion.MinAtcMinutes => AwardParameters.Minutes | AwardParameters.PositionType,
        _ => AwardParameters.None,
    };

    /// <summary>
    /// The parameters of a rule checked against its criterion, each refusal on its field (reading 2): a route has two airports that are not the
    /// same — whether the hub knows them is the save's —, a count from 1 to <see cref="MaxCount"/>, a distance from 1 to
    /// <see cref="MaxDistanceNm"/>, minutes from 1 to <see cref="MaxMinutes"/>. What the criterion does not take is not looked at: the save
    /// empties it.
    /// </summary>
    public static Refusals Check(
        AwardCriterion criterion,
        string? departureIcao,
        string? arrivalIcao,
        int? count,
        int? distanceNm,
        int? minutes,
        Refusals problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        if (!Enum.IsDefined(criterion))
        {
            return problems.Add("criterion", CriterionKey);
        }

        var takes = ParametersOf(criterion);
        if (takes.HasFlag(AwardParameters.Route))
        {
            var departure = ReportRules.Code(departureIcao);
            var arrival = ReportRules.Code(arrivalIcao);
            if (departure is null)
            {
                problems.Add("departureIcao", "errors.required");
            }

            if (arrival is null)
            {
                problems.Add("arrivalIcao", "errors.required");
            }
            else if (arrival == departure)
            {
                problems.Add("arrivalIcao", ReportRules.SameAirportKey);
            }
        }

        if (takes.HasFlag(AwardParameters.Count))
        {
            Range("count", count, MaxCount, problems);
        }

        if (takes.HasFlag(AwardParameters.Distance))
        {
            Range("distanceNm", distanceNm, MaxDistanceNm, problems);
        }

        if (takes.HasFlag(AwardParameters.Minutes))
        {
            Range("minutes", minutes, MaxMinutes, problems);
        }

        return problems;
    }

    /// <summary>
    /// Whether a report's items meet a rule (reading 3): only the <b>verified</b> ones count — an item the check did not find, or could not
    /// ask, is not a flight flown nor a shift controlled —, and only a report of the rule's kind. <paramref name="positionTypes"/> says the kind
    /// of each position's callsign the core knows, for <see cref="AwardCriterion.MinAtcMinutes"/> with a kind; a callsign it does not know is
    /// of no kind.
    /// </summary>
    public static bool IsMet(
        EventAwardRule rule,
        ReportKind kind,
        IEnumerable<EventReportItem> items,
        IReadOnlyDictionary<string, string> positionTypes)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(positionTypes);

        if (KindOf(rule.Criterion) != kind || rule.Kind != kind)
        {
            return false;
        }

        var verified = items.Where(item => item.Verified == true).ToList();

        return rule.Criterion switch
        {
            AwardCriterion.AnyFlight or AwardCriterion.AnyShift => verified.Count > 0,
            AwardCriterion.MinFlights => rule.Count is { } count && verified.Count >= count,
            AwardCriterion.Route => verified.Any(item => OnRoute(rule, item)),
            AwardCriterion.RouteTimes => rule.Count is { } times && verified.Count(item => OnRoute(rule, item)) >= times,
            AwardCriterion.MinLegDistance => rule.DistanceNm is { } miles && verified.Any(item => item.DistanceNm >= miles),
            AwardCriterion.MinAtcMinutes => rule.Minutes is { } minutes
                && verified
                    .Where(item => rule.PositionType is null
                        || (positionTypes.TryGetValue(item.Callsign, out var type)
                            && string.Equals(type, rule.PositionType, StringComparison.OrdinalIgnoreCase)))
                    .Sum(item => item.Minutes ?? 0) >= minutes,
            _ => false,
        };
    }

    private static bool OnRoute(EventAwardRule rule, EventReportItem item) =>
        string.Equals(item.DepartureIcao, rule.DepartureIcao, StringComparison.OrdinalIgnoreCase)
        && string.Equals(item.ArrivalIcao, rule.ArrivalIcao, StringComparison.OrdinalIgnoreCase);

    private static void Range(string field, int? value, int max, Refusals problems)
    {
        if (value is null)
        {
            problems.Add(field, "errors.required");
        }
        else if (value < 1 || value > max)
        {
            problems.Add(field, "errors.number.range");
        }
    }
}
