using IvaoHub.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The airspace of the division, from the snapshot. It is what tells a staff position such as
/// <c>LIRR-CH</c> apart from a position of somewhere else, so it is asked on every login and cached
/// rather than read from the database each time.
/// </summary>
public interface IFirDirectory
{
    /// <summary>The FIR identifiers, upper case. Empty until the snapshot has been taken.</summary>
    Task<IReadOnlySet<string>> GetFirIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The same snapshot read as an airspace — the FIRs and the airports together — which is what
    /// says whether a connection on the network is one of this division's. Cached as one value so
    /// that the answer built from it can be cached against it (see <see cref="IvaoAirspace"/>).
    /// </summary>
    Task<IvaoAirspace> GetAirspaceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The same snapshot with the names, for a list somebody chooses from (G14: the ICAO and the
    /// FIR of an operational document). Cached like the rest, and dropped with the rest.
    /// </summary>
    Task<AirspaceListingDto> GetListingAsync(CancellationToken cancellationToken = default);

    /// <summary>Called by the synchronisation when the snapshot changes.</summary>
    void Invalidate();
}

public sealed class FirDirectory(HubDbContext database, IMemoryCache cache) : IFirDirectory
{
    private const string CacheKey = "ivao:fir-ids";
    private const string AirspaceCacheKey = "ivao:airspace";
    private const string ListingCacheKey = "ivao:airspace-listing";

    /// <summary>
    /// Long, because the set only moves when the daily synchronisation runs, and that one clears
    /// the cache itself.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    public async Task<IReadOnlySet<string>> GetFirIdsAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlySet<string>? cached) && cached is not null)
        {
            return cached;
        }

        var ids = await database.IvaoCenters
            .AsNoTracking()
            .Select(center => center.Id)
            .ToListAsync(cancellationToken);

        IReadOnlySet<string> set = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        cache.Set(CacheKey, set, Lifetime);
        return set;
    }

    public async Task<IvaoAirspace> GetAirspaceAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(AirspaceCacheKey, out IvaoAirspace? cached) && cached is not null)
        {
            return cached;
        }

        var centers = await GetFirIdsAsync(cancellationToken);
        var airports = await database.IvaoAirports
            .AsNoTracking()
            .Select(airport => airport.Icao)
            .ToListAsync(cancellationToken);

        var airspace = new IvaoAirspace(centers, airports.ToHashSet(StringComparer.OrdinalIgnoreCase));
        cache.Set(AirspaceCacheKey, airspace, Lifetime);
        return airspace;
    }

    public async Task<AirspaceListingDto> GetListingAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(ListingCacheKey, out AirspaceListingDto? cached) && cached is not null)
        {
            return cached;
        }

        var airports = await database.IvaoAirports
            .AsNoTracking()
            .OrderBy(airport => airport.Icao)
            .Select(airport => new AirspaceEntryDto(airport.Icao, airport.Name))
            .ToListAsync(cancellationToken);

        var centers = await database.IvaoCenters
            .AsNoTracking()
            .OrderBy(center => center.Id)
            .Select(center => new AirspaceEntryDto(center.Id, center.Name))
            .ToListAsync(cancellationToken);

        var listing = new AirspaceListingDto(airports, centers);
        cache.Set(ListingCacheKey, listing, Lifetime);
        return listing;
    }

    public void Invalidate()
    {
        cache.Remove(CacheKey);
        cache.Remove(AirspaceCacheKey);
        cache.Remove(ListingCacheKey);
    }
}
