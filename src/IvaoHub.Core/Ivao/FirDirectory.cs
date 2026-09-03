using IvaoHub.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The FIRs of the division, from the snapshot. It is what tells a staff position such as
/// <c>LIRR-CH</c> apart from a position of somewhere else, so it is asked on every login and cached
/// rather than read from the database each time.
/// </summary>
public interface IFirDirectory
{
    /// <summary>The FIR identifiers, upper case. Empty until the snapshot has been taken.</summary>
    Task<IReadOnlySet<string>> GetFirIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Called by the synchronisation when the snapshot changes.</summary>
    void Invalidate();
}

public sealed class FirDirectory(HubDbContext database, IMemoryCache cache) : IFirDirectory
{
    private const string CacheKey = "ivao:fir-ids";

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

    public void Invalidate() => cache.Remove(CacheKey);
}
