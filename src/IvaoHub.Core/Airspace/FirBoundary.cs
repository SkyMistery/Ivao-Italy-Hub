namespace IvaoHub.Core.Airspace;

/// <summary>
/// The outline of one flight information region. It is <b>not</b> IVAO data — IVAO publishes the
/// list of centres, never their shape — so it lives here and not under <c>Ivao/</c>, and the source
/// is named in one file only (decision note of 16 September 2026).
/// <para>The bounding box is kept alongside the polygon so that "which FIR is this point in" can
/// throw away almost every row with four comparisons before testing a single ring.</para>
/// </summary>
public sealed class FirBoundary
{
    /// <summary>The identifier of the region, for example <c>LIRR</c> or <c>KZBW</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Oceanic regions overlap continental ones, and a point can be in both.</summary>
    public bool IsOceanic { get; set; }

    /// <summary>The region of the network it belongs to, as the source labels it (EMEA, NAM…).</summary>
    public string? Region { get; set; }

    /// <summary>GeoJSON geometry, <c>Polygon</c> or <c>MultiPolygon</c>, as it came.</summary>
    public string GeometryJson { get; set; } = "{}";

    public double MinLatitude { get; set; }

    public double MaxLatitude { get; set; }

    public double MinLongitude { get; set; }

    public double MaxLongitude { get; set; }

    public DateTime SyncedAt { get; set; }
}
