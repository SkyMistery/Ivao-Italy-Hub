using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Rules;

namespace IvaoHub.Modules.FlightOps.Review;

/// <summary>How the queue is ordered (§4.1), kept as the validator's preference <c>flightops.reviewQueueOrder</c>.</summary>
public static class ReviewQueueOrder
{
    public const string PreferenceKey = "flightops.reviewQueueOrder";

    /// <summary>The report that has waited longest first.</summary>
    public const string ByDate = "date";

    /// <summary>Grouped by tour, and within a tour by date: the legs of one tour validated in a row.</summary>
    public const string ByTour = "tour";
}

/// <summary>A member as the staff reads them: the VID and the name the hub has (none, if they never signed in).</summary>
public sealed record MemberDto(int Vid, string? Name);

/// <summary>
/// One row of the queue (§4.1): tour, leg, pilot, date of the flight, since when it waits, status, who holds it, disputed —
/// and whether the reader may take it. The suggestion of the checks joins with T17.
/// </summary>
public sealed record ReviewQueueRowDto(
    long Id,
    long TourId,
    Localized<string> TourTitle,
    long? LegId,
    int? LegNumber,
    string DepartureIcao,
    string ArrivalIcao,
    MemberDto Pilot,
    DateTime TakeoffAt,
    DateTime QueuedAt,
    PirepStatus Status,
    MemberDto? AssignedTo,
    DateTime? LeaseUntil,
    bool IsDisputed,
    bool IsOwn,
    bool CanTake);

/// <summary>
/// An error of the rules the report froze, as the table of the validation page shows it (§4.3): how often the pilot got it in
/// the calendar year of this flight and ever, on their decided reports (note 2026-09-23-la-validazione §2.5), and whether
/// the current decision marks it.
/// </summary>
public sealed record ReviewErrorDto(
    long Id,
    Localized<string> Name,
    ErrorCategory Category,
    int? YearlyMax,
    IReadOnlyList<string> RuleCodes,
    int CountInYear,
    int CountEver,
    bool Marked,
    bool SuggestedByCheck);

/// <summary>Why the suggestion is a rejection: a dangerous error, or a warning that takes the year over its maximum.</summary>
public sealed record SuggestionReasonDto(long ErrorId, string Reason, int? CountInYear, int? YearlyMax);

/// <summary>What the system proposes (§4.3): <c>Accepted</c> or <c>Rejected</c>, with the errors that decide it.</summary>
public sealed record SuggestionDto(PirepStatus Outcome, IReadOnlyList<SuggestionReasonDto> Reasons);

/// <summary>A ban as the validator reads it on the pilot's profile.</summary>
public sealed record PilotBanDto(long? TourId, DateTime StartsAt, DateTime? EndsAt, string Reason, bool InForce);

/// <summary>
/// The pilot in this tour (§4.3): legs reported, accepted, rejected, and those rejected and disputed; every ban. The counts of
/// the disputes (opened, upheld, turned down) arrive with the threads of T14.
/// </summary>
public sealed record PilotProfileDto(
    MemberDto Pilot,
    int Reported,
    int Accepted,
    int Rejected,
    int Disputed,
    IReadOnlyList<PilotBanDto> Bans);

/// <summary>One flight of the report as the validator reads it: every revision of the plan, and the one at take-off.</summary>
public sealed record ReviewFlightDto(
    int Seq,
    long TrackerSessionId,
    string Callsign,
    string? Aircraft,
    string DepartureIcao,
    string ArrivalIcao,
    DateTime TakeoffAt,
    DateTime? LandingAt,
    IReadOnlyList<ReviewPlanDto> FlightPlans,
    int? PlanAtTakeoffRevision,
    bool HasTrack);

/// <summary>
/// One revision of a flight plan as the page shows it (§4.3), read from what the report stored with the reader of the core's
/// client: the browser never parses the network's own payload. The times are minutes, as a plan writes them (HHMM).
/// </summary>
public sealed record ReviewPlanDto(
    int Revision,
    DateTime FiledAt,
    string DepartureIcao,
    string ArrivalIcao,
    string? AlternateIcao,
    string? SecondAlternateIcao,
    string? AircraftIcao,
    string? WakeTurbulence,
    string Equipment,
    string Transponder,
    string FlightRules,
    string? FlightType,
    string? Level,
    string? Speed,
    string? Route,
    string? Remarks,
    int? DepartureTimeMinutes,
    int? EnrouteMinutes);

/// <summary>A step of the report's history, with the name of who took it: the staff sees it, the pilot never does (§3.5).</summary>
public sealed record ReviewEventDto(PirepStatus? FromStatus, PirepStatus ToStatus, MemberDto? By, DateTime At, string? Note);

/// <summary>What the reader may do on this report now.</summary>
public sealed record ReviewActionsDto(bool CanTake, bool CanRelease, bool CanDecide, bool CanReopen);

/// <summary>
/// The validation page (§4.3): the report and its flights, the rules it froze with the table of their errors, the pilot, the
/// suggestion, the decision as it stands and the history. The airports of the leg and of a diversion come with their positions,
/// for the map (T13b); one the reference data has no position for comes without. The weather (T16) and the automatic checks (T17) have their place
/// and say they are not available yet; the tracks are a request of their own, <c>…/tracks</c>.
/// </summary>
public sealed record ReviewDto(
    long Id,
    long TourId,
    Localized<string> TourTitle,
    string TourSlug,
    PirepStatus Status,
    bool IsDisputed,
    bool IsOwn,
    MemberDto Pilot,
    DateTime SubmittedAt,
    DateTime? ResubmittedAt,
    DateTime QueuedAt,
    SnapshotLegDto Leg,
    IReadOnlyList<AirportDto> Airports,
    string FlightRules,
    string? Sid,
    string? Star,
    string? Approach,
    bool IsDiversion,
    string? DiversionIcao,
    DiversionReason? DiversionReason,
    string? DiversionNote,
    string? PilotRemarks,
    IReadOnlyList<AtcContactDto> AtcContacts,
    IReadOnlyList<AtcExemptionDto> Exemptions,
    bool AtcArchiveAvailable,
    IReadOnlyList<ReviewFlightDto> Flights,
    IReadOnlyList<SnapshotRuleDto> Rules,
    IReadOnlyList<ReviewErrorDto> Errors,
    SuggestionDto Suggestion,
    PilotProfileDto Profile,
    MemberDto? AssignedTo,
    DateTime? LeaseUntil,
    MemberDto? DecidedBy,
    DateTime? DecidedAt,
    string? NoteToPilot,
    string? StaffNote,
    bool ThresholdOverridden,
    string? OverrideReason,
    bool WeatherAvailable,
    bool ChecksAvailable,
    IReadOnlyList<ReviewEventDto> History,
    ReviewActionsDto Actions,
    DateTime RowVersion);

/// <summary>The track of one flight of the report, or none when it was never stored or has already gone (§2.3 of the note).</summary>
public sealed record ReviewTrackDto(int Seq, IReadOnlyList<IvaoTrackPointDto>? Points);

/// <summary>The version of the report the validator saw when they pressed a button.</summary>
public sealed record ReviewStepDto(DateTime RowVersion);

/// <summary>
/// A decision (§4.3): the outcome, the errors marked among the frozen rules', the note to the pilot and the one to the staff,
/// and — when it goes against the suggestion — why.
/// </summary>
public sealed record ReviewDecisionDto(
    PirepStatus Outcome,
    IReadOnlyList<long> ErrorIds,
    string? NoteToPilot,
    string? StaffNote,
    string? OverrideReason,
    DateTime RowVersion);

/// <summary>Reopening a decision (§4.2.1): the reason is required and stays in the history.</summary>
public sealed record ReviewReopenDto(string? Reason, DateTime RowVersion);

/// <summary>The rules a decision said were broken, as the pilot reads them with it (§3.5): never who decided.</summary>
public sealed record ViolatedRuleDto(string Code, Localized<string> Title);
