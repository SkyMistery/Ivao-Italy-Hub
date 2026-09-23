using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Modules.FlightOps.Rules;
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

/// <summary>
/// Where a pilot's dispute of a rejection is (design M2 §3.8; note 2026-09-23-contestazioni-chiarimenti-segnalazioni). Stored by
/// name; none while nobody disputed.
/// </summary>
public enum DisputeStatus
{
    /// <summary>Somebody is looking at it again: the leg no longer holds the next ones.</summary>
    Open,

    /// <summary>Upheld: the report went back to the queue for a new decision.</summary>
    Upheld,

    /// <summary>Turned down: the rejection holds the next legs again, the grace counted from then.</summary>
    Dismissed,
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
/// (<see cref="IHasResourceScope"/>, the container's for a subtour) — the interceptor's guard lets that validator write it (T13).</para>
/// <para>What it is judged against is frozen at the first send: the rules in force with their parameters and errors
/// (§5.4), and the leg as it was (§3.2 point 6). A correction does not freeze them again.</para>
/// <para>A dispute opens a thread with the department in the same save (<see cref="IProjectable"/>, T14b): the thread is the
/// conversation, the outcome stays here.</para>
/// </summary>
[PermissionArea(TourPermissions.Area)]
[AlsoWrittenWith(TourPermissions.Validate)]
public sealed class Pirep : ITourChild, IAuditable, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope, IProjectable
{
    public long Id { get; set; }

    public long TourId { get; set; }

    /// <summary>
    /// The tour a validator is enabled on to take this report: its own, or its container's when it is on a subtour (Carmine,
    /// 23 September 2026, T15). A validator is enabled from the list of the tours, where a subtour does not appear, and a
    /// subtour added to the container later is covered as well. Written at the first send, never changed.
    /// </summary>
    public long ScopeTourId { get; set; }

    /// <summary>Null only on an <c>Open</c> tour, where the flight is the route (§2.6).</summary>
    public long? LegId { get; set; }

    public int Vid { get; set; }

    public PirepStatus Status { get; set; }

    /// <summary>
    /// A flag on a <see cref="PirepStatus.Rejected"/>, not a state: the leg no longer blocks the next ones (§3.8). Since T14b it
    /// is <see cref="DisputeStatus"/> read as a column, written from the getter so the queue filters it in SQL.
    /// </summary>
    public bool IsDisputed
    {
        get => DisputeStatus == Pireps.DisputeStatus.Open;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    /// <summary>Where the pilot's dispute is; none while nobody disputed. One per report: the thread it opens is opened once.</summary>
    public DisputeStatus? DisputeStatus { get; set; }

    /// <summary>What the pilot wrote when disputing: the first message of the thread, kept here with the outcome.</summary>
    public string? DisputeText { get; set; }

    public DateTime? DisputedAt { get; set; }

    /// <summary>When the dispute was upheld or turned down: a rejection that holds again counts its grace from here (§2.5).</summary>
    public DateTime? DisputeDecidedAt { get; set; }

    public int? DisputeDecidedByVid { get; set; }

    /// <summary>
    /// The thread a dispute opens, worded by <c>PirepDisputes</c> with the tour's title the report does not carry, and handed to
    /// the row for the one save that opens it. Not a column: every later save projects nothing, and the thread is never
    /// rewritten anyway (note 2026-09-23-contestazioni-chiarimenti-segnalazioni §3.1).
    /// </summary>
    public ThreadOpeningProjection? DisputeThread { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ResubmittedAt { get; set; }

    /// <summary>When it last entered the queue — sent, or sent again corrected: what the queue is ordered by (§4.1).</summary>
    public DateTime QueuedAt { get; set; }

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

    /// <summary>What the pilot reads with the decision (§4.3); never who wrote it (§3.5).</summary>
    public string? NoteToPilot { get; set; }

    /// <summary>What the staff reads, and the pilot never does.</summary>
    public string? StaffNote { get; set; }

    /// <summary>Whether the decision went against the suggestion (§4.3), worked out by the server when it is taken.</summary>
    public bool ThresholdOverridden { get; set; }

    /// <summary>Why, when it did: asked for then and only then.</summary>
    public string? OverrideReason { get; set; }

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

    public string ResourceScope => ScopeOf(ScopeTourId);

    public string SourceModule => FlightOpsModule.ModuleKey;

    public string SourceId => ReferenceOf(Id);

    /// <summary>The thread of a dispute, in the save that opens it; nothing otherwise — no search line, no calendar entry.</summary>
    public ProjectionSnapshot? Project(ProjectionContext context) =>
        DisputeStatus is not null && DisputeThread is { } thread ? new ProjectionSnapshot(null, [], [], [], [thread]) : null;

    public List<PirepFlight> Flights { get; set; } = [];

    /// <summary>Its history, added to and never changed; written in the same save as the step it records.</summary>
    public List<PirepEvent> Events { get; set; } = [];

    /// <summary>The errors the decision marked (T13), replaced by the next decision.</summary>
    public List<PirepError> Errors { get; set; } = [];

    /// <summary>The scope a grant on one tour carries (§7.3), the tour's and every report's on it.</summary>
    public static string ScopeOf(long tourId) => $"{FlightOpsModule.ModuleKey}:tour:{tourId}";

    /// <summary>How a thread cites a report (<c>FlightOpsReferences</c>), and the source of the thread its dispute opens.</summary>
    public static string ReferenceOf(long id) => $"pirep:{id}";

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

    /// <summary>Its track, written at the send (T13) and gone <c>trackRetentionDays</c> after the decision; read only when asked for.</summary>
    public PirepTrack? Track { get; set; }
}

/// <summary>
/// An error a decision marked on a report (design M2 §1.8), <c>fo_pirep_errors</c>: one of the errors of the rules the
/// report froze, with its category as it was then. The yearly count of an error is these rows on the pilot's decided
/// reports (§4.3; note 2026-09-23-la-validazione §2.5). A new decision replaces them.
/// </summary>
public sealed class PirepError
{
    public long Id { get; set; }

    public long PirepId { get; set; }

    public long ErrorId { get; set; }

    public ErrorCategory Category { get; set; }

    /// <summary>Proposed by an automatic check (T17); false for one the validator marked alone.</summary>
    public bool SuggestedByCheck { get; set; }

    /// <summary>Marked by the validator: only these count. A suggestion left unconfirmed stays for the record.</summary>
    public bool Confirmed { get; set; }
}

/// <summary>
/// The track of one flight of a report (note 2026-09-23-la-validazione §2.2–3), <c>fo_pirep_tracks</c>: the points the
/// tracker gave at the send, as JSON compressed with gzip — about 22 KB a flight —, apart from the flight so that no list
/// ever reads it. It goes <c>trackRetentionDays</c> after the decision (<see cref="TrackRetentionJob"/>).
/// </summary>
public sealed class PirepTrack
{
    public long PirepFlightId { get; set; }

    public byte[] PointsGzip { get; set; } = [];

    public int PointCount { get; set; }

    public DateTime StoredAt { get; set; }
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
/// <para>A completed tour points its pilot out for the tour's award (§3.11, T15): the enrolment projects an
/// <see cref="AwardSignalProjection"/> in the save that completes it, and a human decides from the core's queue. A subtour
/// signals nothing of its own — it counts for its container.</para>
/// </summary>
public sealed class Enrolment : IAuditable, IProjectable
{
    public long Id { get; set; }

    public long TourId { get; set; }

    public int Vid { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Written when an accepted report completes the tour (<c>TourCompletion</c>, T15); never taken back (§3.11).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// The signal of the save that completes the tour, with the reason and the award the tour proposes, which the enrolment
    /// does not hold — set by <c>TourCompletion</c>, not mapped. A completed enrolment is saved that once and never again: a
    /// later save without it would project nothing, and the writer drops a signal still waiting when its row stops projecting
    /// it (one already handled stays whatever happens).
    /// </summary>
    public AwardSignalProjection? AwardSignal { get; set; }

    public string SourceModule => FlightOpsModule.ModuleKey;

    public string SourceId => ReferenceOf(Id);

    public static string ReferenceOf(long id) => $"enrolment:{id}";

    public ProjectionSnapshot? Project(ProjectionContext context) =>
        AwardSignal is { } signal ? new ProjectionSnapshot(null, [], [signal], []) : null;

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }
}

/// <summary>
/// A ban (design M2 §3.9), <c>fo_bans</c>: the pilot sends no report on the tours it names — one, or all of them — while it
/// runs. Their reports already sent are validated as usual. T11 reads it; the bans' list and form write it (T15a, <c>BanEndpoints</c>).
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
