using System.Text.Json;
using IvaoHub.Core.Division;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>What a leg is in its tour (design M2 §1.3): an ordinary leg, or the link between two hubs, which counts as one.</summary>
public enum LegKind
{
    Normal,
    HubConnection,
}

/// <summary>
/// One leg of a tour (design M2 §1.4), <c>fo_legs</c>. Its airports are frozen at the write — the coordinates copied
/// from the core's airports and the great circle distance computed by the server — so a later snapshot of the world
/// never moves a leg somebody has already flown (ADR-024 of Toursystem).
/// <para>It belongs to its tour: department and departments in its care are the tour's, copied at every write
/// (Carmine, 18 September 2026), so the one handler and the interceptor's guard read the same answer on the leg as on
/// the tour.</para>
/// <para>A pilot's report points at a leg by its identifier, never by its number: renumbering touches no report
/// (§1.4.1).</para>
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class Leg : ITourChild, IAuditable
{
    private static readonly JsonSerializerOptions ColumnJson = new(JsonSerializerDefaults.Web);

    public long Id { get; set; }

    public long TourId { get; set; }

    /// <summary>The order in the tour, from 1, kept without holes by the server.</summary>
    public int Number { get; set; }

    public LegKind Kind { get; set; }

    /// <summary>The rotation of a hub tour the leg belongs to; none for a connection between hubs (design M2 §1.3).</summary>
    public long? RotationId { get; set; }

    /// <summary>Its place in the rotation, kept by the server from the order of the legs (<see cref="LegBook.SequenceRotations"/>).</summary>
    public int? SeqInRotation { get; set; }

    public string DepartureIcao { get; set; } = string.Empty;

    public string ArrivalIcao { get; set; } = string.Empty;

    public double DepartureLatitude { get; set; }

    public double DepartureLongitude { get; set; }

    public double ArrivalLatitude { get; set; }

    public double ArrivalLongitude { get; set; }

    /// <summary>The great circle between the two airports, to a tenth of a mile.</summary>
    public decimal DistanceNm { get; set; }

    /// <summary>
    /// ⚠️ No longer written (T8): the suggested callsigns are <see cref="Callsigns"/>. The column stays mapped until a later
    /// release drops it — migrations are additive, and a column is dropped only after a release that no longer uses it.
    /// </summary>
    public string? RealCallsign { get; set; }

    /// <summary>⚠️ No longer written (T8): see <see cref="FlightNumbers"/>, and <see cref="RealCallsign"/> for why it stays.</summary>
    public string? FlightNumber { get; set; }

    /// <summary>The callsigns of the real flights, as a JSON array: the column.</summary>
    public string CallsignsJson { get; set; } = "[]";

    /// <summary>The flight numbers of the real flights, as a JSON array: the column.</summary>
    public string FlightNumbersJson { get; set; } = "[]";

    /// <summary>
    /// The callsigns a pilot may use, suggested from the real flights — more than one when the route is flown several
    /// times a day (Carmine, 22 September 2026). Information: the constraint is §1.6.
    /// </summary>
    public IReadOnlyList<string> Callsigns
    {
        get => JsonSerializer.Deserialize<List<string>>(CallsignsJson, ColumnJson) ?? [];
        set => CallsignsJson = JsonSerializer.Serialize(value ?? [], ColumnJson);
    }

    /// <summary>The flight numbers of the real flights, suggested like the callsigns.</summary>
    public IReadOnlyList<string> FlightNumbers
    {
        get => JsonSerializer.Deserialize<List<string>>(FlightNumbersJson, ColumnJson) ?? [];
        set => FlightNumbersJson = JsonSerializer.Serialize(value ?? [], ColumnJson);
    }

    /// <summary>The aircraft this leg admits, as a JSON object: the column. Empty admits the tour's.</summary>
    public string AircraftJson { get; set; } = JsonSerializer.Serialize(AllowedAircraft.All, ColumnJson);

    /// <summary><see cref="AircraftJson"/> as the record it is: types and groups, like the tour's.</summary>
    public AllowedAircraft Aircraft
    {
        get => JsonSerializer.Deserialize<AllowedAircraft>(AircraftJson, ColumnJson) ?? AllowedAircraft.All;
        set => AircraftJson = JsonSerializer.Serialize(value ?? AllowedAircraft.All, ColumnJson);
    }

    /// <summary>A release of its own, inside the tour's period; none, the tour's.</summary>
    public DateTime? ReleaseAt { get; set; }

    /// <summary>Retired: no longer flown, gone from the public side, its reports kept (§1.4.1).</summary>
    public DateTime? RetiredAt { get; set; }

    public string? RetiredReason { get; set; }

    /// <summary>Why a leg with reports was changed, or restored: required then, and kept by the audit.</summary>
    public string? ChangeReason { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    public GeoPoint Departure => new(DepartureLatitude, DepartureLongitude);

    public GeoPoint Arrival => new(ArrivalLatitude, ArrivalLongitude);

    public bool IsRetired => RetiredAt is not null;
}
