using IvaoHub.Core.Division;
using IvaoHub.Modules.FlightOps.Shape;

namespace IvaoHub.Modules.FlightOps.Threads;

/// <summary>Where a pilot's report of a problem on a leg is. Stored by name.</summary>
public enum LegIssueStatus
{
    /// <summary>Just written: in the list of the tours' staff and counted on the dashboard.</summary>
    Open,

    /// <summary>Looked at and dealt with — the leg corrected, or nothing to correct.</summary>
    Resolved,
}

/// <summary>
/// A pilot's report of a problem on a leg (design M2 §3.11; note 2026-09-23-contestazioni-chiarimenti-segnalazioni §3.3),
/// <c>fo_leg_issues</c>: an airport closed, a route that does not exist, a callsign that does not fit. It reaches the mailbox of
/// the tour's department (<c>flightops.legIssueReported</c>), and the tours' staff close it from <c>/staff/tours/issues</c>.
/// <para>Any signed in member writes one (<see cref="ISubmittedByMembers"/>): the pilot is <see cref="CreatedBy"/>. Afterwards
/// it is an ordinary row of the tour, in the care of its departments (<see cref="ITourChild"/>), and closing it asks for
/// <c>Tours.Edit</c> (§7.1).</para>
/// </summary>
[PermissionArea(TourPermissions.Area)]
public sealed class LegIssue : ITourChild, IAuditable, ISubmittedByMembers
{
    public const int MaxBodyLength = 2000;

    public long Id { get; set; }

    public long TourId { get; set; }

    public long LegId { get; set; }

    /// <summary>What the pilot wrote. One language: theirs.</summary>
    public string Body { get; set; } = string.Empty;

    public LegIssueStatus Status { get; set; } = LegIssueStatus.Open;

    /// <summary>What the staff wrote closing it; the pilot never reads it.</summary>
    public string? StaffNote { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>When the pilot wrote it, and who they are.</summary>
    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}
