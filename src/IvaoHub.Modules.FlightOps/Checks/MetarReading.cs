using System.Globalization;
using System.Text.RegularExpressions;

namespace IvaoHub.Modules.FlightOps.Checks;

/// <summary>The two numbers of a METAR <c>vmc</c> reads: the visibility, and the ceiling when there is one.</summary>
public sealed record MetarWeather(int VisibilityMetres, int? CeilingFeet);

/// <summary>
/// Reads the visibility and the ceiling out of a raw METAR (ICAO Annex 3; the US form in statute miles too): the hub keeps
/// the bulletins as they were published and never parses them (T16), so the one check that needs numbers reads the few it
/// needs here. Only the observation counts: a trend (<c>TEMPO</c>, <c>BECMG</c>, <c>NOSIG</c>) and the remarks are not read.
/// <para>The ceiling is the lowest broken or overcast layer, or the vertical visibility of an obscured sky; a sky without one
/// (<c>FEW</c>, <c>SCT</c>, <c>NSC</c>, <c>NCD</c>, <c>CAVOK</c>) has none.</para>
/// </summary>
public static partial class MetarReading
{
    /// <summary>What <c>9999</c> and <c>CAVOK</c> mean: ten kilometres or more.</summary>
    public const int TenKilometres = 10_000;

    private const double MetresPerStatuteMile = 1609.344;

    /// <summary>The weather of a METAR; null when there is no visibility to read.</summary>
    public static MetarWeather? Read(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var start = Array.FindIndex(tokens, token => Time().IsMatch(token));
        int? visibility = null;
        int? ceiling = null;

        for (var index = start + 1; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token is "TEMPO" or "BECMG" or "NOSIG" or "RMK" or "TREND")
            {
                break;
            }

            if (token == "CAVOK")
            {
                visibility ??= TenKilometres;
                continue;
            }

            if (visibility is null && Metres().Match(token) is { Success: true } metres)
            {
                var value = int.Parse(metres.Groups[1].Value, CultureInfo.InvariantCulture);
                visibility = value == 9999 ? TenKilometres : value;
                continue;
            }

            if (visibility is null && Miles().Match(token) is { Success: true } miles)
            {
                // "1 1/2SM" arrives as two tokens: the whole mile is the one before.
                var whole = index > start + 1 && int.TryParse(tokens[index - 1], NumberStyles.None, CultureInfo.InvariantCulture, out var before) && before < 10
                    ? before
                    : 0;
                visibility = (int)Math.Round((whole + Fraction(miles.Groups[2].Value)) * MetresPerStatuteMile);
                continue;
            }

            if (Layer().Match(token) is { Success: true } layer)
            {
                var height = layer.Groups[2].Value == "///" ? 0 : int.Parse(layer.Groups[2].Value, CultureInfo.InvariantCulture) * 100;
                if (layer.Groups[1].Value is "BKN" or "OVC" or "VV")
                {
                    ceiling = ceiling is { } lower ? Math.Min(lower, height) : height;
                }
            }
        }

        return visibility is { } seen ? new MetarWeather(seen, ceiling) : null;
    }

    private static double Fraction(string text)
    {
        var parts = text.Split('/');
        return parts.Length == 2
            ? double.Parse(parts[0], CultureInfo.InvariantCulture) / double.Parse(parts[1], CultureInfo.InvariantCulture)
            : double.Parse(text, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^\d{6}Z$")]
    private static partial Regex Time();

    /// <summary>The prevailing visibility in metres, maybe with a direction (<c>4000NE</c>).</summary>
    [GeneratedRegex(@"^(\d{4})(NDV|N|NE|E|SE|S|SW|W|NW)?$")]
    private static partial Regex Metres();

    /// <summary>In statute miles: <c>10SM</c>, <c>P6SM</c>, <c>1/2SM</c>, <c>M1/4SM</c>.</summary>
    [GeneratedRegex(@"^([PM])?(\d+(?:/\d+)?)SM$")]
    private static partial Regex Miles();

    [GeneratedRegex(@"^(FEW|SCT|BKN|OVC|VV)(\d{3}|///)")]
    private static partial Regex Layer();
}
