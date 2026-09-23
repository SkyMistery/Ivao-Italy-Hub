using System.Text.Json.Nodes;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>The bounds of what a pilot writes in a report.</summary>
public static class PirepValidation
{
    public const int MaxProcedureLength = 16;
    public const int MaxTextLength = 2000;

    /// <summary>One flight, or two in a diversion (design M2 §3.4).</summary>
    public const int MaxFlights = 2;
}

/// <summary>A session of the tracker the pilot may report: what the list of the form shows to choose from.</summary>
public sealed record TrackerSessionDto(
    long Id,
    string Callsign,
    DateTime StartedAt,
    DateTime EndedAt,
    string? DepartureIcao,
    string? ArrivalIcao,
    string? Aircraft);

/// <summary>
/// A report as the pilot sends it, and sends it again after a correction (design M2 §3.2): the leg — none on an
/// <c>Open</c> tour, and never another on a correction —, the session of the flight, and a second one when it ended
/// elsewhere, with the airport it ended at and why; and the controllers the pilot contacted — the proposed ones they kept
/// and the ones they added — with the exemptions they received (§3.3). Both lists are optional on the wire: none is a
/// flight on which nobody was contacted.
/// </summary>
public sealed record PirepWriteDto(
    long? LegId,
    IReadOnlyList<long> SessionIds,
    bool IsDiversion,
    string? DiversionIcao,
    DiversionReason? DiversionReason,
    string? DiversionNote,
    string? Sid,
    string? Star,
    string? Approach,
    string? PilotRemarks,
    DateTime RowVersion,
    IReadOnlyList<AtcContactWriteDto>? AtcContacts = null,
    IReadOnlyList<AtcExemptionWriteDto>? Exemptions = null);

/// <summary>One flight of a report, as its pilot reads it back.</summary>
public sealed record PirepFlightDto(
    int Seq,
    long TrackerSessionId,
    string Callsign,
    string? Aircraft,
    string DepartureIcao,
    string ArrivalIcao,
    DateTime TakeoffAt,
    DateTime? LandingAt,
    string FlightRules);

/// <summary>
/// A report as its pilot sees it: never the validator's name (design M2 §3.5). With a decision, when it was taken, the note
/// to the pilot and the rules it says were broken (T13). With a rejection, until when it may be disputed, and once disputed
/// where the dispute is and the thread it opened (T14b).
/// </summary>
public sealed record PirepDto(
    long Id,
    long TourId,
    long? LegId,
    PirepStatus Status,
    bool IsDisputed,
    DateTime SubmittedAt,
    DateTime? ResubmittedAt,
    string DepartureIcao,
    string ArrivalIcao,
    decimal DistanceNm,
    DateTime TakeoffAt,
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
    IReadOnlyList<PirepFlightDto> Flights,
    DateTime? DecidedAt,
    string? NoteToPilot,
    IReadOnlyList<Review.ViolatedRuleDto> ViolatedRules,
    DateTime RowVersion,
    DisputeStatus? DisputeStatus = null,
    DateTime? DisputableUntil = null,
    long? ThreadId = null);

/// <summary>A pilot disputing a rejection (§3.8): what they want looked at again, required.</summary>
public sealed record DisputeOpenDto(string? Text, DateTime RowVersion);

/// <summary>One leg as the pilot's map colours it (design M2 §8.1).</summary>
public sealed record MyLegDto(long Id, LegProgress Progress);

/// <summary>
/// Where the signed in pilot is in a tour: the colour of each leg, the ones that may be reported now, the next, whether the
/// tour is done, their reports, and — when they may send none at all — why (a ban, a report to correct, a rating, a tour
/// that no longer takes reports). On an <c>Open</c> tour, how far the goal is.
/// </summary>
public sealed record MyTourDto(
    long TourId,
    IReadOnlyList<MyLegDto> Legs,
    IReadOnlyList<long> Flyable,
    long? Next,
    bool Finished,
    string? Blocked,
    OpenProgress? Goal,
    IReadOnlyList<PirepDto> Reports);

/// <summary>An error as a report froze it with its rule (design M2 §5.4).</summary>
public sealed record SnapshotErrorDto(long Id, Localized<string> Name, ErrorCategory Category, int? YearlyMax);

/// <summary>A rule as a report froze it: what it said, the check and the parameters it carried, and its errors.</summary>
public sealed record SnapshotRuleDto(
    long Id,
    long? AmendsRuleId,
    string Code,
    Localized<string> Title,
    Localized<string> Text,
    string? CheckKey,
    JsonObject Parameters,
    IReadOnlyList<SnapshotErrorDto> Errors);

/// <summary>
/// The leg as a report froze it (design M2 §3.2 point 6): if the leg changes afterwards, the report is judged on this one.
/// On an <c>Open</c> tour, the route the flight flew.
/// </summary>
public sealed record SnapshotLegDto(
    long? LegId,
    int? Number,
    string DepartureIcao,
    string ArrivalIcao,
    decimal DistanceNm,
    IReadOnlyList<string> Callsigns,
    AllowedAircraft Aircraft);
