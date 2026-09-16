using System.Text.Json;
using IvaoHub.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IvaoHub.Core.Airspace;

/// <summary>
/// "Which flight information regions is this point in?" — the one question the hub asks of the
/// outlines. The tours module uses it to work out which regions a flight crossed, so that the ATC
/// who were online there can be <b>proposed</b> to the pilot (design M2 section 3.3); nothing it
/// answers decides anything by itself.
/// <para>A point can be in more than one: oceanic regions overlap continental ones.</para>
/// </summary>
public interface IFirLocator
{
    /// <summary>The regions containing the point, upper case. Empty when the table is empty.</summary>
    Task<IReadOnlyList<string>> LocateAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The distinct regions a track passes through, in the order they are first entered. Sampling is
    /// the caller's business: it passes the points it wants looked at.
    /// </summary>
    Task<IReadOnlyList<string>> LocateAlongAsync(
        IReadOnlyList<(double Latitude, double Longitude)> points,
        CancellationToken cancellationToken = default);

    /// <summary>Called when the boundaries change, so the shapes are read again.</summary>
    void Invalidate();
}

/// <inheritdoc />
public sealed class FirLocator(HubDbContext database, IMemoryCache cache) : IFirLocator
{
    private const string CacheKey = "airspace:fir-shapes";

    /// <summary>
    /// Long: the outlines move when the weekly job runs, and that one clears the cache itself. They
    /// are a few megabytes of rings, so reading them per request would be the expensive half of
    /// answering a question that is otherwise arithmetic.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public async Task<IReadOnlyList<string>> LocateAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var shapes = await ShapesAsync(cancellationToken);
        return [.. shapes.Where(shape => shape.Contains(latitude, longitude)).Select(shape => shape.Id)];
    }

    public async Task<IReadOnlyList<string>> LocateAlongAsync(
        IReadOnlyList<(double Latitude, double Longitude)> points,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);

        var shapes = await ShapesAsync(cancellationToken);
        var crossed = new List<string>();

        foreach (var (latitude, longitude) in points)
        {
            foreach (var shape in shapes)
            {
                if (shape.Contains(latitude, longitude) && !crossed.Contains(shape.Id, StringComparer.Ordinal))
                {
                    crossed.Add(shape.Id);
                }
            }
        }

        return crossed;
    }

    public void Invalidate() => cache.Remove(CacheKey);

    private async Task<IReadOnlyList<FirShape>> ShapesAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<FirShape>? cached) && cached is not null)
        {
            return cached;
        }

        var rows = await database.Firs.AsNoTracking().ToListAsync(cancellationToken);
        IReadOnlyList<FirShape> shapes = [.. rows.Select(FirShape.From)];

        cache.Set(CacheKey, shapes, Lifetime);
        return shapes;
    }
}

/// <summary>
/// One outline, ready to be asked about a point: the rectangle that contains it, and its rings.
/// <para>The rectangle is the whole trick. A thousand regions times a point is a thousand ray
/// castings over rings of thousands of positions; four comparisons throw away all but a handful
/// first, and only those few are actually walked.</para>
/// </summary>
public sealed class FirShape
{
    private readonly IReadOnlyList<IReadOnlyList<(double Longitude, double Latitude)>> _rings;

    private FirShape(
        string id,
        double minLatitude,
        double maxLatitude,
        double minLongitude,
        double maxLongitude,
        IReadOnlyList<IReadOnlyList<(double Longitude, double Latitude)>> rings)
    {
        Id = id;
        MinLatitude = minLatitude;
        MaxLatitude = maxLatitude;
        MinLongitude = minLongitude;
        MaxLongitude = maxLongitude;
        _rings = rings;
    }

    public string Id { get; }

    private double MinLatitude { get; }

    private double MaxLatitude { get; }

    private double MinLongitude { get; }

    private double MaxLongitude { get; }

    /// <summary>How many rings the outline is made of; a test reads it, nothing else does.</summary>
    public int RingCount => _rings.Count;

    public static FirShape From(FirBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);

        using var document = JsonDocument.Parse(boundary.GeometryJson);
        var rings = GeoJson.Rings(document.RootElement).ToArray();

        return new FirShape(
            boundary.Id,
            boundary.MinLatitude,
            boundary.MaxLatitude,
            boundary.MinLongitude,
            boundary.MaxLongitude,
            rings);
    }

    /// <summary>
    /// Ray casting: a point is inside when a line drawn east from it crosses the outline an odd
    /// number of times. A region is made of several rings (islands, enclaves), and being inside any
    /// one of them is being inside the region — these outlines have no holes.
    /// </summary>
    public bool Contains(double latitude, double longitude)
    {
        if (latitude < MinLatitude || latitude > MaxLatitude
            || longitude < MinLongitude || longitude > MaxLongitude)
        {
            return false;
        }

        foreach (var ring in _rings)
        {
            if (ContainsInRing(ring, latitude, longitude))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInRing(
        IReadOnlyList<(double Longitude, double Latitude)> ring,
        double latitude,
        double longitude)
    {
        var inside = false;
        for (int index = 0, previous = ring.Count - 1; index < ring.Count; previous = index++)
        {
            var (currentLon, currentLat) = ring[index];
            var (previousLon, previousLat) = ring[previous];

            if (currentLat > latitude != previousLat > latitude
                && longitude < ((previousLon - currentLon) * (latitude - currentLat)
                    / (previousLat - currentLat)) + currentLon)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
