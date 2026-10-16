using System.Text.Json;
using System.Text.Json.Serialization;
using IvaoHub.Core.Division;
using IvaoHub.Core.Privacy;

namespace IvaoHub.Modules.Events;

/// <summary>Where a shift comes from (design M4 §1.7): proposed by the roster's job, or written by the staff.</summary>
public enum ShiftOrigin
{
    Proposed,
    Staff,
}

/// <summary>How a shift went (design M4 §4.5): not known yet, attended, a no-show the check proposes, confirmed, or excused.</summary>
public enum ShiftAttendance
{
    Unknown,
    Attended,
    NoShowProposed,
    NoShow,
    Excused,
}

/// <summary>
/// One shift of the roster of an event (design M4 §1.7, §4.3, §4.4, E11b; note 2026-10-13-la-proposta-del-roster), <c>evt_atc_shifts</c>:
/// a controller on a position of the event from when to when — proposed by <c>events-roster</c> at the close of the availabilities,
/// with the why (<see cref="ReasonJson"/>), or written by the staff —, and how it went (E13a, E13b). <b>The shifts stay for good</b> (c3):
/// they are the register of reliability.
/// <para>A row of the staff of its event (<see cref="IEventChild"/>), in the area of the ATC, with the FIR of its position copied before the
/// permission is asked (<see cref="IHasFir"/>): with <c>firStaffScope: own</c> the staff of a FIR correct the shifts of their positions
/// alone. About the controller (<see cref="IHasStakeholder"/>): <c>EventAtc.Edit</c> is denied to whoever the row is about, so nobody of
/// the staff adds, moves or takes away a shift of their own, the super administrator included.</para>
/// <para>A shift nobody holds is not a row: an uncovered piece of a position is what no shift meets, read on the roster.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.AtcArea)]
public sealed class EventAtcShift : IEventChild, IAuditable, IHasResourceScope, IHasFir, IHasStakeholder
{
    /// <summary>The longest note of the staff on how a shift went: a sentence or two.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>The why as the database keeps it: the criterion as its word, readable without the code next to it, like the enums of the rows.</summary>
    private static readonly JsonSerializerOptions ReasonJsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public long Id { get; set; }

    public long EventId { get; set; }

    public long PositionId { get; set; }

    /// <summary>The controller: whoever the row is about.</summary>
    public int ControllerVid { get; set; }

    /// <summary>The FIR of its position, copied from it at every write: never the directory's, never the client's.</summary>
    public string? Fir { get; set; }

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    public ShiftOrigin Origin { get; set; }

    /// <summary>
    /// Why the job gave the shift to its controller (§4.3), as data the page writes in words: none for a shift of the staff, and none for
    /// one the staff changed since — the audit keeps the proposal's. Emptied with the controller's data on a shift kept
    /// (<see cref="ErasedWithAttribute"/>): their numbers.
    /// </summary>
    [ErasedWith(nameof(ControllerVid))]
    public string? ReasonJson { get; set; }

    /// <summary>When the controller was told of it (E12): the job's bookkeeping, not a change of the shift.</summary>
    [NotAudited]
    public DateTime? NotifiedAt { get; set; }

    public ShiftAttendance Attendance { get; set; } = ShiftAttendance.Unknown;

    /// <summary>The VID of whoever of the staff decided the attendance (E13b).</summary>
    public int? AttendanceBy { get; set; }

    public DateTime? AttendanceAt { get; set; }

    /// <summary>What the staff wrote about how it went, about the controller: emptied with their data (E10n).</summary>
    [ErasedWith(nameof(ControllerVid))]
    public string? AttendanceNote { get; set; }

    /// <summary>
    /// Who covered the position in the shift's hours when its controller did not (§4.5, E13a; note 2026-10-16-le-verifiche-dopo-l-evento,
    /// reading 5): written by <c>events-after</c> next to a no-show it proposes, from the archive of the sessions; empty when nobody did, or
    /// when only the tracker answered.
    /// </summary>
    public int? CoveredByVid { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    public int? StakeholderVid => ControllerVid;

    /// <summary>The event's: a permission granted on one event reaches its shifts.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);

    /// <summary>The why of the proposal, read; none for a shift of the staff.</summary>
    public ShiftReason? Reason
    {
        get => ReasonJson is null ? null : JsonSerializer.Deserialize<ShiftReason>(ReasonJson, ReasonJsonOptions);
        set => ReasonJson = value is null ? null : JsonSerializer.Serialize(value, ReasonJsonOptions);
    }
}

/// <summary>What decided who holds a proposed shift (§4.3): the first rule the controller chosen won on, in the design's order.</summary>
public enum ShiftCriterion
{
    /// <summary>Nobody else could take it.</summary>
    Only,
    Preferred,
    Experience,
    Penalty,
    FewerShifts,
    EarlierAvailability,

    /// <summary>Everything equal, down to the instant they gave their availability: the lower VID, so that the database decides nothing.</summary>
    Vid,
}

/// <summary>
/// Why the job gave a shift to its controller (§4.3; note 2026-10-13-la-proposta-del-roster, reading 4): how many could take it, what
/// decided, and the controller's numbers — whether they hold the preferred rating of the position's kind, the connections they opened on its
/// callsign and on its kind (none when the archive did not answer for everybody: the criterion was left out), their penalty, and the
/// shifts they already had in the roster. Numbers and a word of a closed set: the page writes them in the language on screen.
/// </summary>
public sealed record ShiftReason(
    int Candidates,
    ShiftCriterion DecidedBy,
    bool Preferred,
    int? CallsignConnections,
    int? TypeConnections,
    double Penalty,
    int ShiftsBefore);
