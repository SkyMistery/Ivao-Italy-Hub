namespace IvaoHub.Core.Ivao;

/// <summary>An airport of the division, snapshot of <c>/v2/airports/all</c> with its runways.</summary>
public sealed class IvaoAirport
{
    public string Icao { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CountryId { get; set; } = string.Empty;

    /// <summary>The centre it belongs to; a plain column, the modules never join across contexts.</summary>
    public string? CenterId { get; set; }

    public string? RunwaysJson { get; set; }

    public string RawJson { get; set; } = "{}";

    public DateTime SyncedAt { get; set; }
}
