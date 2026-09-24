using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Shape;

/// <summary>
/// The filters and the sequence rules of an <see cref="TourKind.Open"/> tour (design M2 §2.6.1, note
/// <c>2026-09-22-il-tour-open</c>). <c>NoRepeatedRoute</c> is always on and is not a row; <c>AircraftTypes</c> is the tour's
/// <see cref="AllowedAircraft"/>. Stored by name.
/// </summary>
public enum TourConstraintKind
{
    DepartureOrArrivalIn,
    DepartureIn,
    ArrivalIn,
    TouchesAirport,
    DistanceBetween,
    AircraftCategory,
    ArrivalRunwayMax,
    ArrivalElevationMin,
    FlightRules,
    Chained,
    Eastbound,
    Westbound,
    IncreasingDistance,
    MinFlightsAt,
}

/// <summary>What a parameter holds.</summary>
public enum ParameterType
{
    /// <summary>A whole number between <see cref="ParameterField.Min"/> and <see cref="ParameterField.Max"/>.</summary>
    Whole,

    /// <summary>One airport the hub knows.</summary>
    Airport,

    /// <summary>Airports the hub knows, between <see cref="ParameterField.Min"/> and <see cref="ParameterField.Max"/> of them.</summary>
    Airports,

    /// <summary>Countries with at least one airport the hub knows, two letters each.</summary>
    Countries,

    /// <summary>Flight information regions with an outline.</summary>
    Firs,

    /// <summary>One of <see cref="ParameterField.Options"/>.</summary>
    Choice,

    /// <summary>Some of <see cref="ParameterField.Options"/>.</summary>
    Choices,
}

/// <summary>
/// One parameter of a goal or of a constraint: its name in the JSON object and in the form, what it holds, whether it is
/// required, and its bounds — the value of a number, the length of a list. <paramref name="Mark"/> is how the list of
/// constraints shows the value, units and signs only: no word, so no language. <paramref name="Default"/> is the value a
/// rule starts with when it names the check the field belongs to (T9, <c>CheckCatalog</c>); the goals and the constraints
/// have none.
/// </summary>
public sealed record ParameterField(
    string Name,
    ParameterType Type,
    bool Required,
    int Min,
    int Max,
    IReadOnlyList<string>? Options = null,
    string Mark = "{0}",
    int? Default = null)
{
    /// <summary>The codes a list starts with, as <see cref="Default"/> is the number a number starts with (T17).</summary>
    public IReadOnlyList<string>? DefaultCodes { get; init; }
}

/// <summary>
/// The one catalogue of what the goals and the constraints of an <see cref="TourKind.Open"/> tour take (note
/// <c>2026-09-22-il-tour-open</c> §4): for each, its fields, and the rules between two fields. <c>Read</c> keeps only
/// the fields of the kind, normalized — upper case, each once — and says what is wrong with each, filed under its name. A
/// pure function: whether an airport, a country or a region exists is <see cref="OpenParameterCheck"/>'s question.
/// </summary>
public static partial class OpenCatalog
{
    /// <summary>Beyond this many, a list is a typo or a job for another kind.</summary>
    public const int MaxListLength = 200;

    public static readonly IReadOnlyList<string> WakeCategories = ["L", "M", "H", "J"];

    public static readonly IReadOnlyList<string> FlightRuleLetters = ["I", "V"];

    [GeneratedRegex("^[A-Z]{2}$")]
    public static partial Regex CountryPattern();

    [GeneratedRegex("^[A-Z0-9_-]{2,12}$")]
    public static partial Regex FirPattern();

    public static IReadOnlyList<ParameterField> Fields(OpenGoal goal) => goal switch
    {
        OpenGoal.Distance => [Whole("nm", 1, TourValidation.MaxRequiredNm, mark: "{0} NM")],
        OpenGoal.FlightCount => [Whole("count", 1, 1000)],
        OpenGoal.DistinctAirports => [Whole("count", 1, 1000)],
        OpenGoal.DistinctCountries => [Whole("count", 1, 250)],
        OpenGoal.CollectList =>
            [List("airports", ParameterType.Airports, required: true), Whole("count", 1, MaxListLength, required: false)],
        OpenGoal.CollectRegions =>
        [
            List("countries", ParameterType.Countries, required: false),
            List("firs", ParameterType.Firs, required: false),
            Whole("count", 1, MaxListLength, required: false),
        ],
        _ => [],
    };

    public static IReadOnlyList<ParameterField> Fields(TourConstraintKind kind) => kind switch
    {
        TourConstraintKind.DepartureOrArrivalIn or TourConstraintKind.DepartureIn or TourConstraintKind.ArrivalIn =>
            [List("countries", ParameterType.Countries, required: true)],
        TourConstraintKind.TouchesAirport => [List("airports", ParameterType.Airports, required: true)],
        TourConstraintKind.DistanceBetween =>
        [
            Whole("minNm", 0, 20_000, required: false, mark: "≥ {0} NM"),
            Whole("maxNm", 1, 20_000, required: false, mark: "≤ {0} NM"),
        ],
        TourConstraintKind.AircraftCategory =>
            [new ParameterField("categories", ParameterType.Choices, Required: true, 1, WakeCategories.Count, WakeCategories)],
        TourConstraintKind.ArrivalRunwayMax => [Whole("meters", 100, 6000, mark: "≤ {0} m")],
        TourConstraintKind.ArrivalElevationMin => [Whole("feet", 0, 15_000, mark: "≥ {0} ft")],
        TourConstraintKind.FlightRules => [new ParameterField("rules", ParameterType.Choice, Required: true, 1, 1, FlightRuleLetters)],
        TourConstraintKind.MinFlightsAt =>
            [new ParameterField("airport", ParameterType.Airport, Required: true, 1, 1), Whole("count", 1, 100, mark: "× {0}")],
        _ => [],
    };

    /// <summary>The kinds a tour may have more than one row of: one per airport (note 2026-09-22-il-tour-open §2.3).</summary>
    public static bool Repeats(TourConstraintKind kind) => kind == TourConstraintKind.MinFlightsAt;

    /// <summary>The goal's parameters, normalized, and what is wrong with them.</summary>
    public static (JsonObject Parameters, IReadOnlyList<ShapeProblem> Problems) Read(OpenGoal goal, JsonNode? input)
    {
        var (parameters, problems) = Read(Fields(goal), input);

        if (goal is OpenGoal.CollectList or OpenGoal.CollectRegions)
        {
            var listed = Codes(parameters, "airports").Count + Codes(parameters, "countries").Count + Codes(parameters, "firs").Count;

            // Countries or regions, never both (§4): a list of either, and "how many" no more than it holds.
            if (goal == OpenGoal.CollectRegions && (Codes(parameters, "countries").Count > 0) == (Codes(parameters, "firs").Count > 0))
            {
                problems.Add(new("countries", "flightops:errors.regionsOneKind"));
            }

            if (Number(parameters, "count") is { } count && listed > 0 && count > listed)
            {
                problems.Add(new("count", "flightops:errors.countAboveList"));
            }
        }

        return (parameters, problems);
    }

    /// <summary>A constraint's parameters, normalized, and what is wrong with them.</summary>
    public static (JsonObject Parameters, IReadOnlyList<ShapeProblem> Problems) Read(TourConstraintKind kind, JsonNode? input)
    {
        var (parameters, problems) = Read(Fields(kind), input);

        if (kind == TourConstraintKind.DistanceBetween)
        {
            var min = Number(parameters, "minNm");
            var max = Number(parameters, "maxNm");

            if (min is null && max is null)
            {
                problems.Add(new("minNm", "flightops:errors.distanceBoundRequired"));
            }
            else if (min > max)
            {
                problems.Add(new("maxNm", "flightops:errors.distanceBounds"));
            }
        }

        return (parameters, problems);
    }

    /// <summary>What a list shows of the parameters: each value with its mark, in the order of the fields.</summary>
    public static IReadOnlyList<string> Describe(IReadOnlyList<ParameterField> fields, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(parameters);

        var shown = new List<string>();

        foreach (var field in fields)
        {
            if (field.Type == ParameterType.Whole)
            {
                if (Number(parameters, field.Name) is { } number)
                {
                    shown.Add(string.Format(CultureInfo.InvariantCulture, field.Mark, number));
                }
            }
            else
            {
                shown.AddRange(Codes(parameters, field.Name).Select(code => string.Format(CultureInfo.InvariantCulture, field.Mark, code)));
            }
        }

        return shown;
    }

    /// <summary>The codes of a field: a list, or one value.</summary>
    public static IReadOnlyList<string> Codes(JsonObject parameters, string name)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return parameters[name] switch
        {
            JsonArray array => [.. array.Select(item => item?.GetValue<string>()).OfType<string>()],
            JsonValue value when value.TryGetValue<string>(out var text) => [text],
            _ => [],
        };
    }

    public static int? Number(JsonObject parameters, string name)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return parameters[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;
    }

    /// <summary>The parameters as an object, or an empty one: what a column holds is never read as anything else.</summary>
    public static JsonObject Parse(string? json)
    {
        try
        {
            return json is null ? [] : JsonNode.Parse(json) as JsonObject ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The fields of any catalogue read out of what was sent: only those, normalized, and what is wrong with each. The goals
    /// and the constraints read theirs with it, and so do the parameters of a rule (T9): one reader for every parameter.
    /// </summary>
    public static (JsonObject Parameters, List<ShapeProblem> Problems) Read(IReadOnlyList<ParameterField> fields, JsonNode? input)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var source = input as JsonObject ?? [];
        var parameters = new JsonObject();
        var problems = new List<ShapeProblem>();

        foreach (var field in fields)
        {
            var node = source[field.Name];

            if (field.Type == ParameterType.Whole)
            {
                ReadWhole(field, node, parameters, problems);
            }
            else
            {
                ReadCodes(field, node, parameters, problems);
            }
        }

        return (parameters, problems);
    }

    private static void ReadWhole(ParameterField field, JsonNode? node, JsonObject parameters, List<ShapeProblem> problems)
    {
        if (node is null)
        {
            if (field.Required)
            {
                problems.Add(new(field.Name, "errors.required"));
            }

            return;
        }

        if (node is not JsonValue value || !value.TryGetValue<int>(out var number))
        {
            // A number with decimals, or text: the same answer as one out of range.
            problems.Add(new(field.Name, "errors.number.range"));
            return;
        }

        if (number < field.Min || number > field.Max)
        {
            problems.Add(new(field.Name, "errors.number.range"));
        }

        parameters[field.Name] = number;
    }

    private static void ReadCodes(ParameterField field, JsonNode? node, JsonObject parameters, List<ShapeProblem> problems)
    {
        var single = field.Type is ParameterType.Airport or ParameterType.Choice;
        IEnumerable<string?> typed = node switch
        {
            JsonArray array => array.Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text : null),
            JsonValue value when value.TryGetValue<string>(out var text) => new[] { text },
            _ => Array.Empty<string?>(),
        };
        var codes = typed
            .Select(code => (code ?? string.Empty).Trim().ToUpperInvariant())
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (codes.Count == 0)
        {
            if (field.Required)
            {
                problems.Add(new(field.Name, "errors.required"));
            }

            return;
        }

        if (single ? codes.Count > 1 : codes.Count > field.Max)
        {
            problems.Add(new(field.Name, "errors.text.tooLong"));
        }

        if (codes.FirstOrDefault(code => !Written(field, code)) is not null)
        {
            problems.Add(new(field.Name, WrongKey(field.Type)));
        }

        parameters[field.Name] = single ? JsonValue.Create(codes[0]) : new JsonArray([.. codes.Select(code => JsonValue.Create(code))]);
    }

    /// <summary>Whether a code looks like one of its kind; whether it exists is asked of the core afterwards.</summary>
    private static bool Written(ParameterField field, string code) => field.Type switch
    {
        ParameterType.Airport or ParameterType.Airports => LegValidation.IcaoPattern().IsMatch(code),
        ParameterType.Countries => CountryPattern().IsMatch(code),
        ParameterType.Firs => FirPattern().IsMatch(code),
        _ => field.Options?.Contains(code, StringComparer.Ordinal) == true,
    };

    private static string WrongKey(ParameterType type) => type switch
    {
        ParameterType.Airport or ParameterType.Airports => "flightops:errors.icao",
        ParameterType.Countries => "flightops:errors.countryCode",
        ParameterType.Firs => "flightops:errors.firCode",
        _ => "flightops:errors.parameterChoice",
    };

    private static ParameterField Whole(string name, int min, int max, bool required = true, string mark = "{0}") =>
        new(name, ParameterType.Whole, required, min, max, Mark: mark);

    private static ParameterField List(string name, ParameterType type, bool required) =>
        new(name, type, required, 1, MaxListLength);
}
