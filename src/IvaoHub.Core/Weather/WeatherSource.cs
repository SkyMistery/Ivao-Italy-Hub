using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Weather;

/// <summary>
/// The chain, measured in vIPI and confirmed here: NOAA first, then the METAR IVAO holds, then
/// VATSIM. The <b>TAF has no fallback</b> — no free source independent of NOAA publishes one — and a
/// missing forecast is simply missing, which a validator can see.
/// <para>The fallbacks are asked only for the airports the link before could not answer for, and
/// only for the present: neither IVAO nor VATSIM keeps history, so
/// <see cref="GetHistoryAsync"/> is NOAA or nothing.</para>
/// </summary>
public sealed class WeatherSource(
    NoaaWeatherClient noaa,
    IIvaoApiClient ivao,
    VatsimMetarClient vatsim,
    IClock clock,
    ILogger<WeatherSource> logger) : IWeatherSource
{
    /// <summary>The name of the IVAO link on a saved bulletin.</summary>
    public const string IvaoSourceName = "ivao";

    public async Task<IReadOnlyList<WeatherReport>> GetCurrentAsync(
        IReadOnlyCollection<string> icaos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(icaos);

        var wanted = icaos
            .Where(icao => !string.IsNullOrWhiteSpace(icao))
            .Select(icao => icao.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (wanted.Length == 0)
        {
            return [];
        }

        var reports = new List<WeatherReport>();
        reports.AddRange(await noaa.GetCurrentMetarsAsync(wanted, cancellationToken));
        reports.AddRange(await noaa.GetCurrentTafsAsync(wanted, cancellationToken));

        var missing = wanted
            .Except(
                reports.Where(report => report.Kind == WeatherReportKind.Metar).Select(report => report.Icao),
                StringComparer.Ordinal)
            .ToArray();

        if (missing.Length == 0)
        {
            return reports;
        }

        logger.LogInformation("NOAA had no METAR for {Count} airport(s); asking IVAO.", missing.Length);

        var stillMissing = new List<string>();
        foreach (var icao in missing)
        {
            var metar = await ivao.GetMetarAsync(icao, cancellationToken);
            if (metar is null)
            {
                stillMissing.Add(icao);
                continue;
            }

            reports.Add(new WeatherReport(
                metar.Icao,
                WeatherReportKind.Metar,
                metar.UpdatedAt ?? clock.UtcNow,
                metar.Raw,
                IvaoSourceName));
        }

        foreach (var icao in stillMissing)
        {
            if (await vatsim.GetMetarAsync(icao, clock.UtcNow, cancellationToken) is { } metar)
            {
                reports.Add(metar);
            }
        }

        return reports;
    }

    public Task<IReadOnlyList<WeatherReport>?> GetHistoryAsync(
        string icao,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);

        if (toUtc < fromUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return noaa.GetHistoryAsync(icao, fromUtc, toUtc, cancellationToken);
    }
}
