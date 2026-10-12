using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>
/// A controller's availability for an event (design M4 §1.7, §4.2, E11a), <c>evt_atc_availability</c>: one per member per event — the
/// unique index is the database's —, with the hours they can control, as windows of their own (<see cref="EventAtcWindow"/>), and a
/// note. The roster proposed at the close of the availabilities (E11b) is made of these.
/// <para>A row of a member, as a booking is: sent by them (<see cref="ISubmittedByMembers"/>) and about them
/// (<see cref="IHasStakeholder"/>), in the area of the ATC with the event's departments and scope, so that the staff of the ATC reach it
/// and nobody else does (§1.1). The member changes it (the core's rule of a row a member sent, T11) and withdraws it until the close;
/// withdrawing deletes it (<see cref="WithdrawnByStakeholderAttribute"/>, E10h): <b>an availability carries no decision of the staff
/// about the member</b> — the roster's decision is a shift, a row of its own (E11b), and nothing on the availability is approved,
/// refused or confirmed; the staff only read it —, so deleting it deletes nothing but what the member sent (Carmine's rule on #232).
/// After the close nobody withdraws: what the roster was proposed from stays. The audit of the core keeps who did what.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.AtcArea)]
[WithdrawnByStakeholder]
public sealed class EventAtcAvailability : IEventChild, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope
{
    /// <summary>The longest note of a controller: a sentence or two — "not before 19:00 on the APP", "with my trainee".</summary>
    public const int MaxNoteLength = 500;

    /// <summary>The most windows of one availability: an evening cut into more pieces than this is not an availability.</summary>
    public const int MaxWindows = 12;

    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>The controller: whoever the row is about.</summary>
    public int ControllerVid { get; set; }

    /// <summary>What the controller adds, in their own words.</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the controller last changed it.</summary>
    public DateTime UpdatedAt { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>Any member, as the global filter reads it; which member is the endpoints' business, as for a booking.</summary>
    public Visibility Visibility
    {
        get => Visibility.Members;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public int? StakeholderVid => ControllerVid;

    /// <summary>The event's: a permission granted on one event reaches its availabilities.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}

/// <summary>
/// One window of a controller's availability (design M4 §1.7), <c>evt_atc_availability_windows</c>: from when to when they can control,
/// inside the event's window. A row of the member as its availability is — sent by them, about them, taken back by them —, carrying the
/// controller and the event so that the write guard reads them on the row itself, as it reads the availability's.
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.AtcArea)]
[WithdrawnByStakeholder]
public sealed class EventAtcWindow : IEventChild, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope
{
    public long Id { get; set; }

    public long AvailabilityId { get; set; }

    public long EventId { get; set; }

    /// <summary>The controller, as on the availability: whoever the row is about.</summary>
    public int ControllerVid { get; set; }

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>Any member, as the global filter reads it, as its availability.</summary>
    public Visibility Visibility
    {
        get => Visibility.Members;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public int? StakeholderVid => ControllerVid;

    /// <summary>The event's, as its availability's.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
