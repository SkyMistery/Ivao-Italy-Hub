using System.Text.Json.Nodes;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Shape;

namespace IvaoHub.Modules.FlightOps.Rules;

/// <summary>
/// The automatic checks a rule or an error may name (design M2 §6.4), and the parameters each takes with the values a rule
/// starts with (§6.2). Born in T9 with no logic behind it: the checks themselves arrive with T17, which implements one
/// <c>IFlightCheck</c> per key and reads its fields from here — one catalogue, not a second one next to the code.
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

    /// <summary>The two that run on the validator's own computer, with its navigation data (§6.6).</summary>
    public const string SemicircularLevels = "semicircularLevels";

    public const string AtcCoverage = "atcCoverage";

    /// <summary>The letters of item 10a of the flight plan an <see cref="Equipment"/> rule may require.</summary>
    public static readonly IReadOnlyList<string> EquipmentLetters =
    [
        "A", "B", "C", "D", "E1", "E2", "E3", "F", "G", "H", "I", "J1", "J2", "J3", "J4", "J5", "J6", "J7", "K", "L",
        "M1", "M2", "M3", "O", "P1", "P2", "P3", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
    ];

    /// <summary>Every key, in the order the form offers them.</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        Callsign, Aircraft, LandingAtArrival, Disconnections, Parking, Speed250, SimRate, Alternate, Equipment,
        TakeoffFromThreshold, Vmc, RepeatedRoute, SemicircularLevels, AtcCoverage,
    ];

    public static bool Exists(string? key) => key is not null && Keys.Contains(key, StringComparer.Ordinal);

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
        Equipment =>
            [new ParameterField("letters", ParameterType.Choices, Required: true, 1, EquipmentLetters.Count, EquipmentLetters)],
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
            foreach (var field in fields.Where(field => field.Default is not null && source[field.Name] is null))
            {
                source[field.Name] = field.Default;
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
}
