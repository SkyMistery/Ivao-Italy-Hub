using System.Text.Json.Nodes;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Shape;

namespace IvaoHub.Modules.FlightOps.Rules;

/// <summary>
/// The automatic checks a rule or an error may name (design M2 §6.4), and the parameters each takes with the values a rule
/// starts with (§6.2). Born in T9 with no logic behind it; since T17 each key the server runs has one <c>IFlightCheck</c>
/// (<c>Checks/</c>), which reads its fields from here — one catalogue, not a second one next to the code.
/// <para>The numbers of the regulation are parameters of the rules, not settings (§1.7). The one exception is the tolerance
/// of <see cref="TakeoffFromThreshold"/>: one for the whole system, changed by the coordinators in the settings (answer 15),
/// so the check takes none here. The values without a figure in the design are Carmine's of 22 September 2026, to be
/// calibrated on the real flights by T17.</para>
/// </summary>
public static class CheckCatalog
{
    public const string Callsign = "callsign";
    public const string Aircraft = "aircraft";
    public const string LandingAtArrival = "landingAtArrival";
    public const string Disconnections = "disconnections";
    public const string Parking = "parking";
    public const string Speed250 = "speed250";
    public const string SimRate = "simRate";
    public const string Alternate = "alternate";
    public const string Equipment = "equipment";
    public const string TakeoffFromThreshold = "takeoffFromThreshold";
    public const string Vmc = "vmc";
    public const string RepeatedRoute = "repeatedRoute";

    /// <summary>
    /// The three the controllers' own PIREPs added (note 2026-09-24-i-controlli-dai-pirep-veri §4): the flight rules of the
    /// plan, a plan filed before the take-off, and the form of the plan.
    /// </summary>
    public const string FlightRules = "flightRules";

    public const string PlanAtTakeoff = "planAtTakeoff";

    public const string FlightPlanForm = "flightPlanForm";

    /// <summary>The highest altitude flown, a limit for each flight rule (note 2026-09-24-i-controlli-dai-pirep-veri §4; T18).</summary>
    public const string MaxAltitude = "maxAltitude";

    /// <summary>The two that run on the validator's own computer, with its navigation data (§6.6).</summary>
    public const string SemicircularLevels = "semicircularLevels";

    public const string AtcCoverage = "atcCoverage";

    /// <summary>The letters of item 10a of the flight plan an <see cref="Equipment"/> rule may require.</summary>
    public static readonly IReadOnlyList<string> EquipmentLetters =
    [
        "A", "B", "C", "D", "E1", "E2", "E3", "F", "G", "H", "I", "J1", "J2", "J3", "J4", "J5", "J6", "J7", "K", "L",
        "M1", "M2", "M3", "O", "P1", "P2", "P3", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
    ];

    /// <summary>The letters of item 10b — the transponder and the surveillance —, the same way (Carmine, 24 September 2026).</summary>
    public static readonly IReadOnlyList<string> TransponderLetters =
    [
        "N", "A", "C", "E", "H", "I", "L", "P", "S", "X", "B1", "B2", "U1", "U2", "V1", "V2", "D1", "G1",
    ];

    /// <summary>The flight rules of item 8: IFR, VFR, IFR then VFR, VFR then IFR.</summary>
    public static readonly IReadOnlyList<string> FlightRuleLetters = ["I", "V", "Y", "Z"];

    /// <summary>Every key, in the order the form offers them.</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        Callsign, Aircraft, FlightRules, PlanAtTakeoff, FlightPlanForm, Alternate, Equipment, LandingAtArrival, Disconnections,
        Parking, Speed250, SimRate, MaxAltitude, TakeoffFromThreshold, Vmc, RepeatedRoute, SemicircularLevels, AtcCoverage,
    ];

    /// <summary>The checks the server never runs: without an agent they stay <c>Unavailable</c> (§6.6).</summary>
    public static readonly IReadOnlyList<string> AgentKeys = [SemicircularLevels, AtcCoverage];

    public static bool Exists(string? key) => key is not null && Keys.Contains(key, StringComparer.Ordinal);

    /// <summary>The name of the parameter that holds the letters of item 10a required with these flight rules.</summary>
    public static string LettersFor(string flightRules) => $"letters{flightRules}";

    /// <summary>The name of the parameter that holds the letters of item 10b required with these flight rules.</summary>
    public static string TransponderFor(string flightRules) => $"transponder{flightRules}";

    /// <summary>The name of the parameter that holds the highest altitude allowed with these flight rules.</summary>
    public static string MaxFeetFor(string flightRules) => $"maxFeet{flightRules}";

    public static IReadOnlyList<ParameterField> Fields(string? key) => key switch
    {
        LandingAtArrival => [Whole("radiusNm", 1, 50, 5, "{0} NM")],
        Disconnections =>
        [
            Whole("maxSingleDisconnectMinutes", 0, 240, 15, "{0} min"),
            Whole("maxTotalDisconnectMinutes", 0, 480, 25, "Σ {0} min"),
        ],
        Parking =>
        [
            Whole("minParkingMinutesBefore", 0, 120, 2, "{0} min"),
            Whole("minParkingMinutesAfter", 0, 120, 2, "{0} min"),
        ],
        Speed250 => [Whole("toleranceKt", 0, 100, 10, "± {0} kt")],
        SimRate => [Whole("tolerancePercent", 0, 100, 10, "± {0} %")],
        // Letters for each flight rule, and the letters wanted only above a level (Carmine, 24 September 2026): W for RVSM and J1
        // for the data link mandate, both above FL285 — a letter the tour does not require stays not required up there too.
        Equipment =>
        [
            .. FlightRuleLetters.Select(rules => Letters(LettersFor(rules), EquipmentLetters, $"{rules}·{{0}}")),
            .. FlightRuleLetters.Select(rules => Letters(TransponderFor(rules), TransponderLetters, $"{rules}·{{0}}")),
            Letters("highLevelLetters", EquipmentLetters, "↑{0}") with { DefaultCodes = ["W", "J1"] },
            Whole("highLevelFl", 100, 600, 285, "> FL{0}"),
        ],
        FlightRules =>
        [
            new ParameterField("rules", ParameterType.Choices, Required: true, 1, FlightRuleLetters.Count, FlightRuleLetters)
            {
                DefaultCodes = FlightRuleLetters,
            },
        ],
        // One limit for each flight rule, like the letters of the equipment (Carmine, 24 September 2026): the system of today's
        // 19 500 ft for VFR and 66 000 for the rest. Y and Z take the IFR limit: where the rules change is on the route, which
        // only the agent reads.
        MaxAltitude =>
        [
            .. FlightRuleLetters.Select(rules =>
                Whole(MaxFeetFor(rules), 1000, 66_000, rules == "V" ? 19_500 : 66_000, $"{rules} ≤ {{0}} ft")),
        ],
        Vmc =>
        [
            Whole("minVisibilityMeters", 0, 20_000, 5000, "≥ {0} m"),
            Whole("minCloudBaseFeet", 0, 10_000, 1500, "≥ {0} ft"),
        ],
        _ => [],
    };

    /// <summary>
    /// The parameters of a rule out of what was sent, normalized, and what is wrong with them. A rule that stands on its
    /// own — a general one, or a tour's that amends none — starts from the check's values for what it does not say;
    /// an amendment keeps only what it changes, and takes the rest from the rule it amends as that rule is read (Carmine,
    /// 22 September 2026): nothing it leaves out is required.
    /// </summary>
    public static (JsonObject Parameters, IReadOnlyList<ShapeProblem> Problems) Read(string? key, JsonNode? input, bool amending)
    {
        var fields = Fields(key);
        var source = (input as JsonObject)?.DeepClone() as JsonObject ?? [];

        if (!amending)
        {
            foreach (var field in fields.Where(field => source[field.Name] is null))
            {
                source[field.Name] = field switch
                {
                    { Default: { } number } => number,
                    { DefaultCodes: { } codes } => new JsonArray([.. codes.Select(code => JsonValue.Create(code))]),
                    _ => null,
                };
            }
        }

        return OpenCatalog.Read(amending ? [.. fields.Select(field => field with { Required = false })] : fields, source);
    }

    /// <summary>The parameters of a rule with an amendment's over them: what the amendment says wins, the rest is the rule's.</summary>
    public static JsonObject Merge(JsonObject amended, JsonObject amendment)
    {
        ArgumentNullException.ThrowIfNull(amended);
        ArgumentNullException.ThrowIfNull(amendment);

        var merged = (JsonObject)amended.DeepClone();
        foreach (var (name, value) in amendment)
        {
            merged[name] = value?.DeepClone();
        }

        return merged;
    }

    private static ParameterField Whole(string name, int min, int max, int fallback, string mark) =>
        new(name, ParameterType.Whole, Required: true, min, max, Mark: mark, Default: fallback);

    private static ParameterField Letters(string name, IReadOnlyList<string> options, string mark) =>
        new(name, ParameterType.Choices, Required: false, 0, options.Count, options, mark);
}
