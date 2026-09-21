using IvaoHub.Core.Division;

namespace IvaoHub.Modules.FlightOps.Shape;

/// <summary>
/// One hub of a <see cref="Tours.TourKind.Hub"/> tour (design M2 §1.3), <c>fo_hubs</c>: an airport the pilot flies
/// rotations out of and back to. Once per tour; in the order <see cref="Sort"/> gives.
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class TourHub : ITourChild, IAuditable
{
    public long Id { get; set; }

    public long TourId { get; set; }

    /// <summary>An airport the hub knows, upper case.</summary>
    public string Icao { get; set; } = string.Empty;

    public int Sort { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}

/// <summary>
/// One rotation of a hub (design M2 §1.3), <c>fo_rotations</c>: <see cref="Size"/> legs out of the hub and back, flown
/// in order (answer 10). Its legs say which rotation they are in (<c>fo_legs.rotation_id</c>); a rotation with a report
/// is retired whole, never one leg at a time (§1.4.1).
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class Rotation : ITourChild, IAuditable
{
    /// <summary>The sizes a rotation may have (design M2 §1.3).</summary>
    public static readonly IReadOnlyList<int> Sizes = [2, 4, 6];

    public long Id { get; set; }

    public long TourId { get; set; }

    public long HubId { get; set; }

    /// <summary>The order inside the hub: with <c>Fixed</c> the order the pilot flies them in.</summary>
    public int Sort { get; set; }

    /// <summary>How many legs it has: 2, 4 or 6.</summary>
    public int Size { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}
