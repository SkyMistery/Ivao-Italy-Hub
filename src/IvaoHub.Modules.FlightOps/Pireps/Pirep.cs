using IvaoHub.Core.Division;
using IvaoHub.Modules.FlightOps.Shape;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>Where a report is (design M2 §3.1). Stored by name.</summary>
public enum PirepStatus
{
    /// <summary>Sent, sent again after a correction, or reopened: waiting for a validator. The leg is pending.</summary>
    Queued,

    /// <summary>Taken by a validator (T13). The leg is pending.</summary>
    InReview,

    Accepted,

    /// <summary>The pilot corrects everything and sends it again; until then no other leg of the tour is reported.</summary>
    ToModify,

    /// <summary>The leg is flown again; it blocks the next ones (§2.5) until it is disputed.</summary>
    Rejected,

    /// <summary>By the pilot from <see cref="Queued"/>, or by the module after a correction never came. The session is free.</summary>
    Withdrawn,
}

/// <summary>Why a flight ended somewhere else (design M2 §3.4). Stored by name.</summary>
public enum DiversionReason
{
    Weather,
    Technical,
    Medical,
    AtcInstruction,
    Other,
}

/// <summary>
/// A pilot's report of one leg of a tour, or of one flight of an <c>Open</c> tour (design M2 §1.8), <c>fo_pireps</c>.
/// <para>The pilot sends it (<see cref="ISubmittedByMembers"/>) and it is about them (<see cref="IHasStakeholder"/>): nobody
/// decides their own reports, super administrator included (§7.3), and they keep changing it themselves — withdraw it,
/// correct it — through the one exception of the interceptor's guard. It is in the care of its tour's departments, which it
/// follows as the tour's other rows do (<see cref="ITourChild"/>), and a validator enabled on the tour is enabled on it
/// (<see cref="IHasResourceScope"/>).</para>
/// <para>What it is judged against is frozen at the first send: the rules in force with their parameters and errors
/// (§5.4), and the leg as it was (§3.2 point 6). A correction does not freeze them again.</para>
/// </summary>
[PermissionArea(TourPermissions.Area)]
public sealed class Pirep : ITourChild, IAuditable, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope
{
    public long Id { get; set; }

    public long TourId { get; set; }

    /// <summary>Null only on an <c>Open</c> tour, where the flight is the route (§2.6).</summary>
    public long? LegId { get; set; }

    public int Vid { get; set; }

    public PirepStatus Status { get; set; }

    /// <summary>A flag on a <see cref="PirepStatus.Rejected"/>, not a state: the leg no longer blocks the next ones (§3.8).</summary>
    public bool IsDisputed { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ResubmittedAt { get; set; }

    /// <summary>The route the report is judged on: the leg's as it was, or the flight's on an <c>Open</c> tour.</summary>
    public string DepartureIcao { get; set; } = string.Empty;

    public string ArrivalIcao { get; set; } = string.Empty;

    /// <summary>The great circle of the route, what a <c>Distance</c> or an <c>Open</c> tour counts (§2.6).</summary>
    public decimal DistanceNm { get; set; }

    /// <summary>
    /// When the first flight left the ground: the UTC day of the daily limits (§3.6), the order of a chain, and the
    /// grace after a rejection (§2.5). Copied from the flight so that counting is a query.
    /// </summary>
    public DateTime TakeoffAt { get; set; }

    /// <summary><c>I</c>, <c>V</c>, <c>Y</c> or <c>Z</c>, from the plan valid at take-off.</summary>
    public string FlightRules { get; set; } = string.Empty;

    public string? Sid { get; set; }

    public string? Star { get; set; }

    public string? Approach { get; set; }

    /// <summary>The controllers contacted (§3.3): a list of <see cref="AtcContactDto"/>, the proposed ones the pilot removed included.</summary>
    public string AtcContactsJson { get; set; } = "[]";

    /// <summary>The exemptions received (§3.3): a list of <see cref="AtcExemptionDto"/>, each with its status at the send.</summary>
    public string AtcExemptionsJson { get; set; } = "[]";

    /// <summary>
    /// Whether the archive of ATC sessions answered at the send: without it nothing was proposed, and an empty list of
    /// controllers says nothing about who was online (§3.3). False on the reports sent before T12.
    /// </summary>
    public bool AtcArchiveAvailable { get; set; }

    public bool IsDiversion { get; set; }

    /// <summary>Where the first flight landed instead, when it was a diversion.</summary>
    public string? DiversionIcao { get; set; }

    public DiversionReason? DiversionReason { get; set; }

    public string? DiversionNote { get; set; }

    public string? PilotRemarks { get; set; }

    /// <summary>The rules in force at the first send, with parameters and errors (§5.4): a list of <see cref="SnapshotRuleDto"/>.</summary>
    public string RulesSnapshotJson { get; set; } = "[]";

    /// <summary>The leg as it was at the first send (§3.2 point 6): a <see cref="SnapshotLegDto"/>; the flight's route on an Open tour.</summary>
    public string LegSnapshotJson { get; set; } = "{}";

    /// <summary>Who has it in hand, and until when (§4.2). Written by T13.</summary>
    public int? AssignedToVid { get; set; }

    public DateTime? LeaseUntil { get; set; }

    /// <summary>Who decided it and when: the grace after a rejection runs from here (§2.5). Written by T13.</summary>
    public int? DecidedByVid { get; set; }

    public DateTime? DecidedAt { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>
    /// Any member, as the global filter reads it (design M2 §1.1): an anonymous reader never sees a report. Which member is
    /// the endpoints' business — a pilot reads their own, the staff by the permissions of §7.
    /// </summary>
    public Visibility Visibility
    {
        get => Visibility.Members;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public int? StakeholderVid => Vid;

    public string ResourceScope => ScopeOf(TourId);

    public List<PirepFlight> Flights { get; set; } = [];

    /// <summary>Its history, added to and never changed; written in the same save as the step it records.</summary>
    public List<PirepEvent> Events { get; set; } = [];

    /// <summary>The scope a grant on one tour carries (§7.3), the tour's and every report's on it.</summary>
    public static string ScopeOf(long tourId) => $"{FlightOpsModule.ModuleKey}:tour:{tourId}";

    /// <summary>The states in which the leg counts as pending: sent and not decided, or sent back to the pilot.</summary>
    public static bool IsPending(PirepStatus status) =>
        status is PirepStatus.Queued or PirepStatus.InReview or PirepStatus.ToModify;

    /// <summary>What a daily limit counts, and what a route already flown is (§3.6, §2.6): neither rejected nor withdrawn.</summary>
    public static bool Counts(PirepStatus status) => status is not (PirepStatus.Rejected or PirepStatus.Withdrawn);
}

/// <summary>
/// One flight of a report (design M2 §1.8), <c>fo_pirep_flights</c>: one, or two in a diversion — the flight that ended
/// elsewhere and the one that brought the aircraft to the leg's arrival. It keeps every revision of the flight plan as IVAO
/// gave them, and which one was valid at take-off.
/// <para>A session of the tracker counts for one report only (§3.4): <see cref="ClaimedSessionId"/> holds it, unique, while
/// the report claims it, and is emptied when the report is withdrawn, so the flight can be reported again.
/// <see cref="TrackerSessionId"/> keeps it either way.</para>
/// </summary>
public sealed class PirepFlight
{
    public long Id { get; set; }

    public long PirepId { get; set; }

    /// <summary>1, or 2 for the flight after a diversion.</summary>
    public int Seq { get; set; }

    public long TrackerSessionId { get; set; }

    /// <summary>The session, while the report holds it: unique, and null once the report lets it go.</summary>
    public long? ClaimedSessionId { get; set; }

    public string Callsign { get; set; } = string.Empty;

    public string? Aircraft { get; set; }

    public string DepartureIcao { get; set; } = string.Empty;

    public string ArrivalIcao { get; set; } = string.Empty;

    public DateTime TakeoffAt { get; set; }

    public DateTime? LandingAt { get; set; }

    /// <summary>Every revision of the plan, as IVAO sends them: the validator sees them all (§4.3).</summary>
    public string FlightPlansJson { get; set; } = "[]";

    /// <summary>Which revision the checks read: the last one filed before the take-off.</summary>
    public int? PlanAtTakeoffRevision { get; set; }
}

/// <summary>The history of a report (design M2 §1.8), <c>fo_pirep_events</c>: rows are added, never changed.</summary>
public sealed class PirepEvent
{
    public long Id { get; set; }

    public long PirepId { get; set; }

    /// <summary>None when the report was created.</summary>
    public PirepStatus? FromStatus { get; set; }

    public PirepStatus ToStatus { get; set; }

    /// <summary>The VID of whoever did it; 0 for the module itself (the automatic withdrawal).</summary>
    public int ByVid { get; set; }

    public DateTime At { get; set; }

    /// <summary>An i18n key or a few words; never shown to the pilot as the validator's name (§3.5).</summary>
    public string? Note { get; set; }
}

/// <summary>
/// A pilot in a tour (design M2 §1.9), <c>fo_enrolments</c>: the first report writes it. How far they got is never stored;
/// it is read off the reports (<see cref="TourRules"/>). Where a <c>SequentialChosenStart</c> began and the order of the hubs
/// of a <c>Hub</c> tour are read off them too (Carmine, 23 September 2026): a start whose only report is withdrawn is free again.
/// <para>A report on a subtour enrols the pilot in it and in its container (§2.7).</para>
/// </summary>
public sealed class Enrolment : IAuditable
{
    public long Id { get; set; }

    public long TourId { get; set; }

    public int Vid { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Written by T15, when an accepted report completes the tour; never taken back (§3.5).</summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }
}

/// <summary>
/// A ban (design M2 §3.9), <c>fo_bans</c>: the pilot sends no report on the tours it names — one, or all of them — while it
/// runs. Their reports already sent are validated as usual. T11 reads it; the screen that writes it is T15's.
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class Ban : IOwnedByDepartment, IAuditable
{
    public long Id { get; set; }

    public int Vid { get; set; }

    /// <summary>The tour; none, every tour.</summary>
    public long? TourId { get; set; }

    public DateTime StartsAt { get; set; }

    /// <summary>None, for good.</summary>
    public DateTime? EndsAt { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>Whether it holds on this tour at this moment; a subtour is covered by a ban on its container too.</summary>
    public bool Holds(long tourId, long? parentTourId, DateTime at) =>
        StartsAt <= at
        && (EndsAt is null || EndsAt > at)
        && (TourId is null || TourId == tourId || TourId == parentTourId);
}
