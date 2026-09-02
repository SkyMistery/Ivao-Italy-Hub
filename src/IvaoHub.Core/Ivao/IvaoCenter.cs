namespace IvaoHub.Core.Ivao;

/// <summary>
/// A FIR of the division, snapshot of <c>/v2/centers</c>. FIRs are not configuration: they come
/// from IVAO, and the snapshot keeps the hub working when the API is down.
/// </summary>
public sealed class IvaoCenter
{
    /// <summary>ICAO of the centre, for example <c>LIRR</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CountryId { get; set; } = string.Empty;

    public string RawJson { get; set; } = "{}";

    public DateTime SyncedAt { get; set; }
}
