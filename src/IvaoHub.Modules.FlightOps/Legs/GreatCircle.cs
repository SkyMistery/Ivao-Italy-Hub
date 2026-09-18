namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>A position on the surface of the earth, in decimal degrees.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// The great circle distance between two points, in nautical miles: the number a leg carries, the distance tours
/// count and the estimated time divides (design M2 §1.4, §1.5, §2.6). It is the GCD between the two airports and
/// <b>not</b> the miles flown — whoever stretches the route earns nothing, whoever shortens it loses nothing.
/// Taken from Toursystem (<c>TourSystem.Application.Authoring.GreatCircle</c>) with its tests.
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

    /// <summary>Rounded to a tenth, as <c>fo_legs.distance_nm</c> keeps it (<c>DECIMAL(7,1)</c>).</summary>
    public static decimal DistanceNmRounded(GeoPoint from, GeoPoint to) =>
        Math.Round((decimal)DistanceNm(from, to), 1);
}

/// <summary>
/// The estimated time of a leg (design M2 §1.5): <c>minutes = 60 × GCD × (1 + k) / speed + c</c>, with <c>k</c> and
/// <c>c</c> from the settings and the speed of the tour's reference aircraft. Computed at every read and never
/// stored, so a new speed or a new pair of numbers changes every tour at once, published ones included.
/// Information for the pilot, never a constraint of a report.
/// </summary>
public static class EstimatedTime
{
    public static int Minutes(decimal distanceNm, int cruiseTasKt, decimal factor, int fixedMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cruiseTasKt);

        return (int)Math.Round((60m * distanceNm * (1 + factor) / cruiseTasKt) + fixedMinutes, MidpointRounding.AwayFromZero);
    }
}
