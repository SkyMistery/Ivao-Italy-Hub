using System.Text.Json.Nodes;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.FlightOps.Agent;

// Version 1 of the agent's contract (Carmine, 24 September 2026: the agent's DTOs are its own, frozen in the version). Filled by
// the same services as the validation page, never the page's DTOs: a change of the page must not be a change of the contract.
// Within version 1 a field may be added, never renamed or removed. Statuses and outcomes are strings, not the module's enums,
// for the same reason. No default values: a record parameter with one becomes an optional field of the generated schema.

/// <summary>What the hub speaks: the versions, the checks an agent may send, and the ones the server keeps for itself.</summary>
public sealed record AgentContractDto(
    int Current,
    IReadOnlyList<int> Accepted,
    IReadOnlyList<string> AgentChecks,
    IReadOnlyList<string> ServerChecks,
    int MaxResults,
    int MaxEvidenceCharacters);

/// <summary>
/// A report waiting for a validator the token's member may decide — never their own —, with the agent's checks its rules name
/// and when an agent last sent a result on it since it was queued (none: it waits for one).
/// </summary>
public sealed record AgentQueueItemDto(
    long Id,
    long TourId,
    Localized<string> TourTitle,
    int? LegNumber,
    string DepartureIcao,
    string ArrivalIcao,
    int PilotVid,
    DateTime TakeoffAt,
    DateTime QueuedAt,
    string Status,
    IReadOnlyList<string> AgentChecks,
    DateTime? AgentRanAt);

/// <summary>
/// A report as the agent reads it (note of 15 September §3.2): every revision of every flight's plan, the tracks, the parameters
/// of the agent's checks as the report froze them, the settings they read, the airports with their runways, the archive of the
/// controllers for the flight's interval, and what agents already sent.
/// </summary>
public sealed record AgentPirepDto(
    long Id,
    long TourId,
    Localized<string> TourTitle,
    string Status,
    int PilotVid,
    string FlightRules,
    string? Sid,
    string? Star,
    string? Approach,
    AgentLegDto Leg,
    bool IsDiversion,
    string? DiversionIcao,
    IReadOnlyList<AgentFlightDto> Flights,
    IReadOnlyList<AgentCheckWantedDto> Checks,
    AgentSettingsDto Settings,
    IReadOnlyList<AgentAirportDto> Airports,
    AgentAtcDto Atc,
    IReadOnlyList<AgentResultDto> Results);

/// <summary>The leg the report froze: on an <c>Open</c> tour, the route flown and no number.</summary>
public sealed record AgentLegDto(int? Number, string DepartureIcao, string ArrivalIcao, IReadOnlyList<string> Callsigns);

/// <summary>One flight: 1, or 2 after a diversion. <c>Track</c> is null once the track has gone (90 days after the decision).</summary>
public sealed record AgentFlightDto(
    int Seq,
    string Callsign,
    string? Aircraft,
    string DepartureIcao,
    string ArrivalIcao,
    DateTime TakeoffAt,
    DateTime? LandingAt,
    int? PlanAtTakeoffRevision,
    IReadOnlyList<AgentPlanDto> Plans,
    IReadOnlyList<AgentTrackPointDto>? Track);

/// <summary>One revision of a flight plan, as the tracker filed it; the times are minutes (HHMM in the plan).</summary>
public sealed record AgentPlanDto(
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

/// <summary>One point of a track, about every 15 seconds.</summary>
public sealed record AgentTrackPointDto(
    DateTime At,
    double Latitude,
    double Longitude,
    int AltitudeFeet,
    int GroundSpeedKnots,
    int Heading,
    bool OnGround);

/// <summary>A check the report's rules name that the server does not run, with the parameters of the rule that names it.</summary>
public sealed record AgentCheckWantedDto(string Key, JsonObject Parameters);

/// <summary>The module's settings an agent's check reads: where semicircular levels go north and south (design M2 §6.4).</summary>
public sealed record AgentSettingsDto(IReadOnlyList<string> NorthSouthLevelCountries);

/// <summary>An airport of the report, with its position when the reference data has one, and its runway ends.</summary>
public sealed record AgentAirportDto(
    string Icao,
    string Name,
    string CountryId,
    double? Latitude,
    double? Longitude,
    int? ElevationFeet,
    IReadOnlyList<AgentRunwayDto> Runways);

/// <summary>One runway end; IVAO gives the length sometimes in feet as if it were metres.</summary>
public sealed record AgentRunwayDto(string Designator, int? LengthMetres, int? Bearing, double? Latitude, double? Longitude, int? ElevationFeet);

/// <summary>
/// The controllers for the flight's interval: an hour before the first take-off to an hour after the last landing. Not
/// <c>Available</c> when the division has no archive or it could not be read — «not available», never «nobody online». A
/// position the archive does not list was offline only from <c>DivisionSince</c> (for the division's prefixes) or
/// <c>WorldSince</c> on. <c>Declared</c> and <c>Exemptions</c> are what the pilot sent.
/// </summary>
public sealed record AgentAtcDto(
    bool Available,
    DateTime From,
    DateTime To,
    IReadOnlyList<AgentAtcPresenceDto> Online,
    DateTime? DivisionSince,
    DateTime? WorldSince,
    IReadOnlyList<string> DivisionPrefixes,
    IReadOnlyList<AgentAtcContactDto> Declared,
    IReadOnlyList<AgentExemptionDto> Exemptions);

public sealed record AgentAtcPresenceDto(string Callsign, string? Frequency, DateTime StartedAt, DateTime? EndedAt);

/// <summary>A controller the pilot said they contacted: proposed by the hub and kept, added by hand, or proposed and removed.</summary>
public sealed record AgentAtcContactDto(string Callsign, string? Frequency, string Origin);

/// <summary>An exemption the pilot declared, with the status the send found and the checks it softens.</summary>
public sealed record AgentExemptionDto(string Callsign, string Kind, string? Note, string Status, IReadOnlyList<string> Softens);

/// <summary>What an agent sent on the report for one check: the last one, since sending the same check again replaces it.</summary>
public sealed record AgentResultDto(
    string CheckKey,
    string Outcome,
    IReadOnlyList<string> Evidence,
    DateTime RanAt,
    int? ByVid,
    string? AgentVersion);

/// <summary>The results of one run of the agent on a report.</summary>
public sealed record AgentChecksWriteDto(string? AgentVersion, IReadOnlyList<AgentCheckWriteDto>? Results);

/// <summary>
/// One result: the check's key, <c>Passed</c>, <c>Failed</c> or <c>Unavailable</c>, and the lines that say why — text, up to 2000
/// characters in all, in the shape the note allows (no coordinates, no points the pilot did not write, no geometry).
/// </summary>
public sealed record AgentCheckWriteDto(string? CheckKey, string? Outcome, IReadOnlyList<string>? Evidence);

/// <summary>What the report holds from agents afterwards, and the errors of the catalogue now suggested on it by any check.</summary>
public sealed record AgentChecksWrittenDto(IReadOnlyList<AgentResultDto> Results, IReadOnlyList<long> SuggestedErrorIds);
