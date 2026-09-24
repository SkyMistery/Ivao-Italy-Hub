using System.Globalization;
using System.Text.RegularExpressions;

namespace IvaoHub.Modules.FlightOps.Checks;

/// <summary>What a callsign looks like, for the <c>REG/</c> of the plan (note 2026-09-24-i-controlli-dai-pirep-veri §4).</summary>
public enum CallsignShape
{
    /// <summary>Three letters and a number, <c>RYR2599</c>: the aircraft is named by <c>REG/</c>.</summary>
    Airline,

    /// <summary>The aircraft's own marks, <c>ICELLO</c>, <c>N260MA</c>: no <c>REG/</c> needed.</summary>
    Registration,

    /// <summary>Neither for sure: the check says so and does not fail.</summary>
    Unclear,
}

/// <summary>
/// The pieces of a flight plan the checks read, out of the text the pilot filed: the letters of items 10a and 10b, the
/// indicators of item 18, the levels of item 15 and the procedures written in the route. Pure functions.
/// </summary>
public static partial class FlightPlanText
{
    /// <summary>The letters of item 10a or 10b as filed, <c>SDE2E3FG</c> read as S, D, E2, E3, F, G.</summary>
    public static IReadOnlyList<string> Letters(string? filed) =>
        [.. LetterPattern().Matches((filed ?? string.Empty).ToUpperInvariant()).Select(match => match.Value).Distinct(StringComparer.Ordinal)];

    /// <summary>Whether item 18 carries an indicator, <c>REG/</c> or <c>PBN/</c>, as a word of its own.</summary>
    public static bool HasIndicator(string? remarks, string indicator) =>
        (remarks ?? string.Empty).ToUpperInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Any(word => word.StartsWith($"{indicator}/", StringComparison.Ordinal));

    /// <summary>A <c>RMK</c> not followed by its slash, as in <c>RMK TCAS</c>.</summary>
    public static bool HasBareRemark(string? remarks) =>
        remarks is not null && BareRemarkPattern().IsMatch(remarks.ToUpperInvariant());

    /// <summary>
    /// The highest level the plan asks for, as a flight level: the cruising level of item 15 and every change of level
    /// written in the route (<c>ADASI/N0433F370</c>). Altitudes and metric levels are turned into flight levels; <c>VFR</c>
    /// is none. Null when the plan asks for no level at all.
    /// </summary>
    public static int? HighestFlightLevel(string? level, string? route)
    {
        var levels = new List<int>();
        if (LevelOf(level) is { } cruise)
        {
            levels.Add(cruise);
        }

        foreach (Match change in RouteLevelPattern().Matches((route ?? string.Empty).ToUpperInvariant()))
        {
            if (LevelOf(change.Groups["level"].Value) is { } value)
            {
                levels.Add(value);
            }
        }

        return levels.Count == 0 ? null : levels.Max();
    }

    /// <summary>A level of item 15 as a flight level: <c>F240</c> 240, <c>A045</c> 45, <c>S1130</c> and <c>M0840</c> from metres.</summary>
    public static int? LevelOf(string? text)
    {
        var match = LevelPattern().Match((text ?? string.Empty).Trim().ToUpperInvariant());
        if (!match.Success)
        {
            return null;
        }

        var number = int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
        return match.Groups["unit"].Value switch
        {
            "F" or "A" => number,

            // Tens of metres: S1130 is 11,300 m, about FL371.
            _ => (int)Math.Round(number * 10 * 3.28084 / 100),
        };
    }

    /// <summary>The points and airways of the route, without the speed and level written after a slash.</summary>
    public static IReadOnlyList<string> RouteTokens(string? route) =>
        [.. (route ?? string.Empty).ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Split('/')[0])
            .Where(token => token.Length > 0)];

    /// <summary>Whether a word of the route is written as a SID or a STAR is: a point, a digit, a letter — <c>OBUTI2W</c>.</summary>
    public static bool IsProcedure(string token) => ProcedurePattern().IsMatch(token);

    /// <summary>What the callsign says of the aircraft (note 2026-09-24-i-controlli-dai-pirep-veri §4).</summary>
    public static CallsignShape ShapeOf(string callsign)
    {
        var text = (callsign ?? string.Empty).Trim().ToUpperInvariant();
        if (AirlinePattern().IsMatch(text))
        {
            return CallsignShape.Airline;
        }

        return RegistrationPattern().IsMatch(text) || AmericanRegistrationPattern().IsMatch(text)
            ? CallsignShape.Registration
            : CallsignShape.Unclear;
    }

    [GeneratedRegex("[A-Z][0-9]?")]
    private static partial Regex LetterPattern();

    [GeneratedRegex(@"(?:^|\s)RMK(?!/)\b")]
    private static partial Regex BareRemarkPattern();

    [GeneratedRegex(@"/[NKM]\d{3,4}(?<level>[FA]\d{3}|[SM]\d{4})")]
    private static partial Regex RouteLevelPattern();

    [GeneratedRegex(@"^(?<unit>[FA])(?<value>\d{3})$|^(?<unit>[SM])(?<value>\d{4})$")]
    private static partial Regex LevelPattern();

    [GeneratedRegex("^[A-Z]{3,5}[0-9][A-Z]?$")]
    private static partial Regex ProcedurePattern();

    [GeneratedRegex("^[A-Z]{3}[0-9][A-Z0-9]{0,3}$")]
    private static partial Regex AirlinePattern();

    [GeneratedRegex("^[A-Z]{4,7}$")]
    private static partial Regex RegistrationPattern();

    [GeneratedRegex("^N[0-9][A-Z0-9]{0,4}$")]
    private static partial Regex AmericanRegistrationPattern();
}
