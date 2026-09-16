namespace IvaoHub.Core.Ivao;

/// <summary>
/// One runway end of an airport, as IVAO publishes it: <c>RW07</c> is a threshold, and a runway with
/// two usable directions is two rows. Measured on 16 September 2026 against
/// <c>/v2/airports/LIRF/runways</c>, which answers six rows for three runways with
/// <c>{ id, airportIcao, runway, length, bearing, latitude, longitude, elevation, width }</c>.
/// <para>The coordinates are <b>of the threshold</b>, which is the whole reason this table exists:
/// the check that says whether a pilot took off from the threshold or from an intersection measures
/// the distance from here (design M2 section 6.4).</para>
/// <para>Not every airport in the world is fetched: the runways of the airports a tour or a report
/// actually touches are, on demand, through <see cref="IRunwayDirectory"/>.</para>
/// </summary>
public sealed class IvaoRunway
{
    /// <summary>ICAO of the airport, for example <c>LIRF</c>.</summary>
    public string AirportIcao { get; set; } = string.Empty;

    /// <summary>The designator as IVAO writes it, for example <c>RW07</c> or <c>RW16L</c>.</summary>
    public string Designator { get; set; } = string.Empty;

    /// <summary>Metres, when IVAO publishes it.</summary>
    public int? LengthMetres { get; set; }

    public int? WidthMetres { get; set; }

    /// <summary>True bearing of the runway, in degrees.</summary>
    public int? Bearing { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? ElevationFeet { get; set; }

    public DateTime SyncedAt { get; set; }
}
