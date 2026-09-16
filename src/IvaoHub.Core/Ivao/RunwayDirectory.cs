using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The runways of the airports the hub actually uses. There are about 45 000 airports in the world
/// and one call per airport to fetch runways, so fetching them all would be 45 000 calls for data
/// almost nobody reads: instead, whoever needs an airport says so, and the runways of that airport
/// are fetched once and kept.
/// </summary>
public interface IRunwayDirectory
{
    /// <summary>
    /// Makes sure these airports have their runways, fetching the ones that are missing. Returns how
    /// many were fetched. It never throws: an airport IVAO cannot answer for stays without runways,
    /// and the checks that need them say "not available" rather than failing a pilot.
    /// </summary>
    Task<int> EnsureAsync(IReadOnlyCollection<string> icaos, CancellationToken cancellationToken = default);

    /// <summary>The runways of one airport, as they are now: it does not fetch.</summary>
    Task<IReadOnlyList<IvaoRunway>> GetAsync(string icao, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class RunwayDirectory(
    HubDbContext database,
    IIvaoApiClient ivao,
    IClock clock,
    ILogger<RunwayDirectory> logger) : IRunwayDirectory
{
    public async Task<int> EnsureAsync(
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
            return 0;
        }

        var known = await database.IvaoRunways
            .AsNoTracking()
            .Where(runway => wanted.Contains(runway.AirportIcao))
            .Select(runway => runway.AirportIcao)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missing = wanted.Except(known, StringComparer.Ordinal).ToArray();
        if (missing.Length == 0)
        {
            return 0;
        }

        var fetched = 0;
        foreach (var icao in missing)
        {
            var runways = await ivao.GetRunwaysAsync(icao, cancellationToken);
            if (runways is null || runways.Count == 0)
            {
                // Not an error: plenty of small fields have no published runway data at all.
                logger.LogInformation("IVAO has no runways for {Icao}.", icao);
                continue;
            }

            foreach (var runway in runways)
            {
                runway.SyncedAt = clock.UtcNow;
                database.IvaoRunways.Add(runway);
            }

            fetched += runways.Count;
        }

        if (fetched > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Fetched {Count} runway(s) for {Airports} airport(s).", fetched, missing.Length);
        }

        return fetched;
    }

    public async Task<IReadOnlyList<IvaoRunway>> GetAsync(
        string icao,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);

        var code = icao.ToUpperInvariant();
        return await database.IvaoRunways
            .AsNoTracking()
            .Where(runway => runway.AirportIcao == code)
            .OrderBy(runway => runway.Designator)
            .ToListAsync(cancellationToken);
    }
}
