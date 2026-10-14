using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>
/// A position an event opens (design M4 §1.7, §4.1, E11a), <c>evt_atc_positions</c>: a callsign of the core's positions of the division,
/// the hours it is open — the event's window, unless the staff say otherwise — and a note of the staff. The roster of the event (E11b)
/// cuts it into shifts.
/// <para>A row of the staff that belongs to its event (<see cref="IEventChild"/>), in the area of the ATC, <c>EventAtc</c>, so that
/// whoever decides the positions — the ATC operations and the staff of the FIRs (§6.2) — does not write the event. It says its FIR
/// (<see cref="IHasFir"/>), the one the core's directory gives its callsign, written by the server before the permission is asked:
/// with <c>firStaffScope: own</c> the staff of a FIR see and decide the positions of their FIR alone, through the one handler, the
/// write guard and the generated list (note 2026-09-27-i-capi-fir-sul-loro-fir). It is the first row of the area that says its FIR,
/// so with it the division's grants to the team of a FIR on the area take effect at the start (<c>PositionGrantSeeder</c>).</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.AtcArea)]
public sealed class EventAtcPosition : IEventChild, IAuditable, IHasResourceScope, IHasFir
{
    /// <summary>The longest callsign of a position, as the network spells them (<c>LIRF_N_APP</c>), with room to spare.</summary>
    public const int MaxCallsignLength = 16;

    /// <summary>The longest note of the staff on a position: a sentence or two.</summary>
    public const int MaxNoteLength = 500;

    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>A callsign of the core's positions of the division, as the core spells it.</summary>
    public string Callsign { get; set; } = string.Empty;

    /// <summary>The FIR of the position, as the core's directory says it; none for a position the directory gives no FIR.</summary>
    public string? Fir { get; set; }

    /// <summary>When it opens: inside the event's window.</summary>
    public DateTime FromUtc { get; set; }

    /// <summary>When it closes: inside the event's window, after it opens.</summary>
    public DateTime ToUtc { get; set; }

    /// <summary>What the staff note about it, for the staff: one language, not on the event's page.</summary>
    public string? Note { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>The event's: a permission granted on one event reaches its positions.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
