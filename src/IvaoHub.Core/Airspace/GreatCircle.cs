namespace IvaoHub.Core.Airspace;

/// <summary>A position on the surface of the earth, in decimal degrees, latitude first, as every airport row has it.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// The great circle distance between two points, in nautical miles: the distance between two airports as the hub counts
/// it — the number a leg of a tour carries and its estimated time divides (design M2 §1.4, §1.5), the distance of a flight
/// in an event's report and the one its award rules compare (design M4 §1.8, §1.9). It is the distance between the two
/// airports and <b>not</b> the miles flown: whoever stretches the route earns nothing, whoever shortens it loses nothing.
/// <para>A module asks <c>IAirportDirectory</c> where the airports are and measures here, so the calculation is written
/// once for every module (note 2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo). It is arithmetic on a sphere, not
/// data of the network, so it lives beside the outlines of the regions and not under <c>Ivao/</c>. Written in the tours
/// module first (T7a, taken from Toursystem with its tests) and brought here with the same numbers when the events needed
/// it too (E10e).</para>
/// </summary>
public static class GreatCircle
{
    /// <summary>
    /// Mean earth radius in nautical miles (6371 km / 1.852). The earth is not a sphere, but over an aeronautical
    /// distance the gap to the ellipsoidal formula is a few tenths of a mile in a thousand: below the precision the
    /// number is used with.
    /// </summary>
    private const double EarthRadiusNm = 3440.0647948164;

    /// <summary>The haversine formula: stable on short distances too.</summary>
    public static double DistanceNm(GeoPoint from, GeoPoint to)
    {
        var lat1 = double.DegreesToRadians(from.Latitude);
        var lat2 = double.DegreesToRadians(to.Latitude);
        var deltaLat = lat2 - lat1;
        var deltaLon = double.DegreesToRadians(to.Longitude - from.Longitude);

        var sinLat = Math.Sin(deltaLat / 2);
        var sinLon = Math.Sin(deltaLon / 2);

        var h = (sinLat * sinLat) + (Math.Cos(lat1) * Math.Cos(lat2) * sinLon * sinLon);

        // Asin rather than the Atan2 of the cosine: on short distances — two airports ten miles apart — that one
        // loses significant digits.
        return 2 * EarthRadiusNm * Math.Asin(Math.Sqrt(Math.Min(1, h)));
    }

    /// <summary>
    /// Rounded to a tenth, the precision the hub keeps a distance with (a <c>DECIMAL(…, 1)</c> column, as the legs of the
    /// tours have it). The rounding of <see cref="Math.Round(decimal, int)"/>: a half goes to the even tenth.
    /// </summary>
    public static decimal DistanceNmRounded(GeoPoint from, GeoPoint to) =>
        Math.Round((decimal)DistanceNm(from, to), 1);
}
