namespace IvaoHub.Core.Ivao;

/// <summary>
/// The airspace of the division, as the snapshot of the reference data describes it: the centres
/// and the airports that make a connection "one of ours".
/// <para>It exists as one value rather than as two loose sets because the client caches an answer
/// against it, and a key recomputed by joining a few hundred identifiers on every page view would
/// cost more than the call it is saving. <see cref="CacheKey"/> is built once, when the directory
/// builds the airspace, and lives exactly as long as the sets do.</para>
/// </summary>
public sealed class IvaoAirspace
{
    public IvaoAirspace(IReadOnlySet<string> centers, IReadOnlySet<string> airports)
    {
        ArgumentNullException.ThrowIfNull(centers);
        ArgumentNullException.ThrowIfNull(airports);

        Centers = centers;
        Airports = airports;

        // The centres alone, with the two counts: a division has a handful of them, and an airport
        // never appears without the snapshot that brought its centre.
        CacheKey = $"{centers.Count}/{airports.Count}/{string.Join(',', centers.Order(StringComparer.Ordinal))}";
    }

    /// <summary>ICAO of the FIRs, upper case.</summary>
    public IReadOnlySet<string> Centers { get; }

    /// <summary>ICAO of the airports, upper case.</summary>
    public IReadOnlySet<string> Airports { get; }

    /// <summary>Stable for as long as the snapshot is, which is what makes it a cache key.</summary>
    public string CacheKey { get; }

    /// <summary>Nothing is in the area, which is what an installation with no snapshot yet has.</summary>
    public static IvaoAirspace Empty { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Which station a callsign is worked from: everything before the first underscore, which is
    /// how the network names a position (<c>LIRR_CTR</c>, <c>LIML_APP</c>). A callsign with no
    /// underscore is its own station.
    /// </summary>
    public static string StationOf(string callsign)
    {
        ArgumentNullException.ThrowIfNull(callsign);

        var underscore = callsign.IndexOf('_', StringComparison.Ordinal);
        return (underscore < 0 ? callsign : callsign[..underscore]).ToUpperInvariant();
    }

    /// <summary>Whether a controller working this station is controlling in this airspace.</summary>
    public bool Covers(string station) => Centers.Contains(station) || Airports.Contains(station);

    /// <summary>Whether a flight that starts or ends here is a flight of this airspace.</summary>
    public bool Serves(string? departure, string? arrival) =>
        (departure is not null && Airports.Contains(departure))
        || (arrival is not null && Airports.Contains(arrival));
}

/// <summary>A controller online: as little of them as a page needs to say who is on frequency.</summary>
/// <param name="Callsign">For example <c>LIRR_CTR</c>.</param>
/// <param name="Station">The centre or the airport it is worked from, upper case.</param>
/// <param name="Frequency">In megahertz, already written out; null when the network did not say.</param>
public sealed record IvaoNetworkPosition(string Callsign, string Station, string? Frequency);

/// <summary>
/// Who is connected, right now. Counted twice on purpose: once for the whole network and once for
/// the airspace of the division, because "four controllers" and "four controllers out of two
/// hundred" are different sentences and a page may want either.
/// </summary>
/// <param name="UpdatedAt">
/// When the network says it counted, not when the hub asked. Null means the answer could not be
/// had at all, which is what tells a page to say so instead of drawing four zeroes.
/// </param>
/// <param name="NetworkAtc">Controllers connected anywhere.</param>
/// <param name="NetworkPilots">Pilots connected anywhere.</param>
/// <param name="AreaAtc">Controllers working a station of this airspace.</param>
/// <param name="AreaPilots">Pilots whose flight plan starts or ends in it.</param>
/// <param name="Positions">Those controllers, by callsign, so a page can say who is on frequency.</param>
public sealed record IvaoNetworkStatus(
    DateTime? UpdatedAt,
    int NetworkAtc,
    int NetworkPilots,
    int AreaAtc,
    int AreaPilots,
    IReadOnlyList<IvaoNetworkPosition> Positions)
{
    /// <summary>What a caller gets when the network cannot be reached. Never an exception.</summary>
    public static IvaoNetworkStatus Unknown { get; } = new(null, 0, 0, 0, 0, []);
}
