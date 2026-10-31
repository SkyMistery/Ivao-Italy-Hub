using IvaoHub.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The stamp that tells a cookie it is out of date. It changes whenever the grants or the super
/// administrator flag of a user change, and the cookie carrying the old value is rejected on the
/// next request (design M0 section 3.3).
/// <para>Cached for a minute so that the check does not become a database call per request, and
/// invalidated on the spot by whoever writes, so that a revoked permission bites immediately — in the
/// process that wrote. The cache is the process's own: when Passenger keeps two processes alive
/// together (it does, <c>diagnostics/starts.txt</c> shows it), the other one goes on accepting the
/// old stamp until its minute runs out. So a revoked grant stops working at once where it was
/// revoked and within a minute everywhere, as design M0 §3 says (issue #320, item 5).</para>
/// </summary>
public interface ISecurityStampCache
{
    Task<string?> GetAsync(int vid, CancellationToken cancellationToken = default);

    void Invalidate(int vid);
}

public sealed class SecurityStampCache(IMemoryCache cache, HubDbContext database) : ISecurityStampCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Forgetting a stamp without holding the reader. The save changes interceptor needs exactly
    /// this and cannot take <see cref="ISecurityStampCache"/>: that one reads through
    /// <c>HubDbContext</c>, which is built with the interceptor in it, and asking for it there
    /// would be asking the container to build a context in order to build a context.
    /// <para>The key stays in one place, which is the only thing that matters.</para>
    /// </summary>
    public static void Forget(IMemoryCache cache, int vid)
    {
        ArgumentNullException.ThrowIfNull(cache);
        cache.Remove(Key(vid));
    }

    public async Task<string?> GetAsync(int vid, CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(Key(vid), out string? cached))
        {
            return cached;
        }

        var stamp = await database.Users
            .AsNoTracking()
            .Where(user => user.Vid == vid)
            .Select(user => user.SecurityStamp)
            .FirstOrDefaultAsync(cancellationToken);

        // A missing user is not cached: remembering "no such VID" for a minute would make the very
        // first request after a login race the row that login has just written.
        if (stamp is not null)
        {
            cache.Set(Key(vid), stamp, Lifetime);
        }

        return stamp;
    }

    public void Invalidate(int vid) => Forget(cache, vid);

    private static string Key(int vid) => $"security-stamp:{vid}";
}
