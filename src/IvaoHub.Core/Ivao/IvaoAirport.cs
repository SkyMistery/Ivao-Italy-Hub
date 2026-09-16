namespace IvaoHub.Core.Ivao;

/// <summary>
/// An airport, snapshot of <c>/v2/airports/all</c>. Since T1 of M2 the snapshot is of the
/// <b>world</b> and not of the division: a tour goes anywhere, and a leg needs the coordinates of
/// both of its ends. Measured on 16 September 2026: 44 689 airports of 235 countries, 13.9 MB, and
/// every one of them carries a latitude and a longitude (7732 also carry an IATA code).
/// <para>Whatever reads "the airports of this division" therefore has to filter by country now,
/// which it did not have to when the table only held them.</para>
/// </summary>
public sealed class IvaoAirport
{
    public string Icao { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CountryId { get; set; } = string.Empty;

    /// <summary>The IATA code where there is one: a pilot recognises FCO faster than LIRF.</summary>
    public string? Iata { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Feet. The filter "airports above this height" of an Open tour reads it.</summary>
    public int? ElevationFeet { get; set; }

    /// <summary>The centre it belongs to; a plain column, the modules never join across contexts.</summary>
    public string? CenterId { get; set; }

    public string? RunwaysJson { get; set; }

    public string RawJson { get; set; } = "{}";

    public DateTime SyncedAt { get; set; }
}
