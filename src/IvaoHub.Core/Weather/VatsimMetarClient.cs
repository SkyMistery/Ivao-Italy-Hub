using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Weather;

/// <summary>
/// The last link of the chain, and the smallest: VATSIM serves the current METAR of an airport as a
/// line of text. No key, no history, no TAF — it is there so that a division whose flights are
/// being validated does not lose the weather entirely when both NOAA and IVAO are down.
/// <para>One call per airport, so the chain only ever gets here for the few airports the other two
/// could not answer for.</para>
/// </summary>
public sealed class VatsimMetarClient(HttpClient http, ILogger<VatsimMetarClient> logger)
{
    /// <summary>The name of this source on a saved bulletin.</summary>
    public const string SourceName = "vatsim";

    public async Task<WeatherReport?> GetMetarAsync(
        string icao,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);

        try
        {
            using var response = await http.GetAsync(
                new Uri($"/metar.php?id={Uri.EscapeDataString(icao.ToUpperInvariant())}", UriKind.Relative),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("VATSIM answered the METAR of {Icao} with {Status}.", icao, (int)response.StatusCode);
                return null;
            }

            var raw = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

            // An airport it knows nothing about answers with an empty body, and one that is not an
            // airport at all with a line of prose: a METAR starts with the code it is about.
            if (raw.Length == 0 || !raw.StartsWith(icao, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new WeatherReport(icao.ToUpperInvariant(), WeatherReportKind.Metar, now, raw, SourceName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning(exception, "VATSIM could not be reached for the METAR of {Icao}.", icao);
            return null;
        }
    }
}
