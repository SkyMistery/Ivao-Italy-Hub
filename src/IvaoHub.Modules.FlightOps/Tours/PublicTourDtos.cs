using System.Text.Json.Nodes;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Shape;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>
/// A tour as a card shows it (design M2 §8.1): what fits on a tile, and nothing a visitor may not see. The picture is
/// the identifier alone — a module reads no row of the core's library, and the address built from the identifier is
/// the one a browser is asked to check again rather than keep for a year.
/// <para>No progress and no next leg: those are the pilot's own, and the browser draws them on top of the card from
/// <c>GET /api/flightops/my-tours</c> (T15b, <see cref="People.MyTours"/>). A card is the same for whoever is looking.</para>
/// </summary>
public sealed record PublicTourCardDto(
    long Id,
    string Slug,
    TourKind Kind,
    Localized<string> Title,
    Localized<string> Summary,
    long? CoverMediaId,
    TourStateKind State,
    DateTime ReleaseAt,
    DateTime CloseAt,
    int Legs,
    decimal TotalNm);

/// <summary>
/// One leg of a tour as the public page and the map draw it (design M2 §8.1): the two airports with their coordinates —
/// the leg's own, frozen when it was written — the great circle between them, the estimated time when the tour names a
/// reference aircraft, and the callsigns and flight numbers suggested.
/// <para>A retired leg never arrives here (§1.4.1): it is in the editor's map and nowhere else. One not yet released
/// does, saying so, because a tour says in advance what it will ask.</para>
/// </summary>
public sealed record PublicLegDto(
    long Id,
    int Number,
    LegKind Kind,
    long? RotationId,
    string DepartureIcao,
    string? DepartureIata,
    double DepartureLatitude,
    double DepartureLongitude,
    string ArrivalIcao,
    string? ArrivalIata,
    double ArrivalLatitude,
    double ArrivalLongitude,
    decimal DistanceNm,
    int? EstimatedMinutes,
    IReadOnlyList<string> Callsigns,
    IReadOnlyList<string> FlightNumbers,
    PublicAircraftDto Aircraft,
    DateTime? ReleaseAt,
    bool Released);

/// <summary>
/// The aircraft something admits, as a reader needs them: the types written on it, and the names of the groups — a
/// visitor has no group list to look an identifier up in. Empty admits every aircraft.
/// </summary>
public sealed record PublicAircraftDto(IReadOnlyList<string> Types, IReadOnlyList<PublicAircraftGroupDto> Groups);

/// <summary>A group of types by name: the identifier is of no use to a reader, the name is.</summary>
public sealed record PublicAircraftGroupDto(long Id, Localized<string> Name);

/// <summary>One rotation of a hub tour, so that the page groups the legs the way the tour flies them (design M2 §1.3).</summary>
public sealed record PublicRotationDto(long Id, string Icao, int Sort, int Size);

/// <summary>A subtour of a container, as its parent's page lists it (note 2026-09-21-la-forma-dei-tour).</summary>
public sealed record PublicSubtourDto(
    long Id,
    string Slug,
    Localized<string> Title,
    Localized<string> Summary,
    TourStateKind State,
    DateTime ReleaseAt,
    DateTime CloseAt,
    int Legs,
    decimal TotalNm);

/// <summary>
/// One tour as a visitor reads it (design M2 §8.1): the briefing as it stands — a tour has no published version behind
/// it, the row is the version — the dates, the aircraft, the rules in force with their parameters, and the legs with
/// their distances and the totals. <see cref="Department"/> is where a pilot's questions about it go (T14b): the department
/// that looks after it.
/// <para>What is here depends on the kind: a <c>Distance</c> tour carries the miles it asks for, an <c>Open</c> tour its
/// goal and the filters and sequence rules every flight has to respect (§2.6.1), a <c>Container</c> its subtours and no
/// legs of its own, a <c>Hub</c> tour its rotations.</para>
/// </summary>
public sealed record PublicTourDto(
    long Id,
    string Slug,
    TourKind Kind,
    Localized<string> Title,
    Localized<string> Summary,
    JsonNode Briefing,
    long? CoverMediaId,
    long? BannerMediaId,
    TourStateKind State,
    DateTime ReleaseAt,
    DateTime CloseAt,
    int ReportWindowDays,
    TourProgression Progression,
    HubRotationOrder? HubRotationOrder,
    bool RequiresProcedures,
    int? MinPilotRating,
    string? ReferenceAircraftIcao,
    PublicAircraftDto Aircraft,
    int? RequiredNm,
    int? RequiredSubtours,
    OpenGoal? OpenGoal,
    JsonNode? OpenGoalParameters,
    IReadOnlyList<string> OpenGoalValues,
    IReadOnlyList<TourConstraintListDto> Constraints,
    IReadOnlyList<CallsignRuleListDto> CallsignRules,
    IReadOnlyList<EffectiveRuleDto> Rules,
    IReadOnlyList<PublicErrorDto> Errors,
    IReadOnlyList<PublicRotationDto> Rotations,
    IReadOnlyList<PublicLegDto> Legs,
    decimal TotalNm,
    int? TotalEstimatedMinutes,
    PublicParentDto? Parent,
    IReadOnlyList<PublicSubtourDto> Subtours,
    Department Department);

/// <summary>The container a subtour belongs to, so that its page can lead back to it.</summary>
public sealed record PublicParentDto(long Id, string Slug, Localized<string> Title);

/// <summary>
/// An error a rule in force names, when the division made it public (design M2 §5.3). The same rows the block
/// <c>flightops.errorCatalog</c> shows, narrowed to the ones this tour's rules can give.
/// </summary>
public sealed record PublicErrorDto(
    long Id,
    Localized<string> Name,
    Localized<string> Description,
    ErrorCategory Category,
    int? YearlyMax);
