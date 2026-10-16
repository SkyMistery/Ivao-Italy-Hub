using IvaoHub.Core.Division;
using IvaoHub.Core.Privacy;

namespace IvaoHub.Modules.Events;

/// <summary>Where a request to hand a shift over stands (design M4 §4.4-bis): waiting for the staff, approved, refused, or withdrawn by its controller.</summary>
public enum TransferStatus
{
    Pending,
    Approved,
    Refused,
    Withdrawn,
}

/// <summary>
/// A controller's request to hand their shift over (design M4 §4.4-bis, E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione,
/// reading 7), <c>evt_atc_shift_transfers</c>: the shift, who hands it over, to whom — a member they name, a trainee of theirs, or nobody: «I
/// am looking for a substitute» —, their note, and what the staff decided. <b>The shift stays theirs until the staff decide</b>: changing who
/// holds a shift is not a change of one's own row.
/// <para>A row of the member, as an availability is: sent by them (<see cref="ISubmittedByMembers"/>) and about them
/// (<see cref="IHasStakeholder"/>), in the area of the ATC with the event's care and scope and the FIR of its shift (<see cref="IHasFir"/>),
/// so that the staff of a FIR decide the requests of their positions. <c>EventAtc.Edit</c> is denied to whoever hands over: nobody approves
/// their own request, the super administrator included. <b>Not</b> withdrawn by deleting it: it carries the staff's decision, so the member
/// takes a pending request back by changing its state (the core's rule of a row a member sent, T11), and the audit keeps every step.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.AtcArea)]
public sealed class EventAtcTransfer : IEventChild, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope, IHasFir
{
    /// <summary>The longest note, of the controller or of the staff: a sentence or two.</summary>
    public const int MaxNoteLength = 500;

    public long Id { get; set; }

    public long EventId { get; set; }

    public long ShiftId { get; set; }

    /// <summary>Who hands the shift over: whoever the row is about.</summary>
    public int ControllerVid { get; set; }

    /// <summary>The member the shift is handed to, as the controller named them; none: they look for a substitute.</summary>
    public int? ToVid { get; set; }

    /// <summary>The FIR of the shift, copied from it when the request is made: never the client's.</summary>
    public string? Fir { get; set; }

    /// <summary>Why, in the controller's own words: emptied with their data on a request kept, and in the audit of one gone (E10n).</summary>
    [ErasedWith(nameof(ControllerVid))]
    public string? Note { get; set; }

    public TransferStatus Status { get; set; } = TransferStatus.Pending;

    /// <summary>The VID of whoever of the staff decided it.</summary>
    public int? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    /// <summary>What the staff wrote to the controller with their decision — why it is refused —: about the controller, emptied with their data.</summary>
    [ErasedWith(nameof(ControllerVid))]
    public string? DecisionNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>A withdrawal and a decision at once meet here: the second is told it changed meanwhile.</summary>
    public DateTime RowVersion { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>Any member, as the global filter reads it; which member is the endpoints' business, as for an availability.</summary>
    public Visibility Visibility
    {
        get => Visibility.Members;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public int? StakeholderVid => ControllerVid;

    /// <summary>The event's: a permission granted on one event reaches its requests.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
