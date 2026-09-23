using IvaoHub.Core.Ivao;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>
/// One session of the tracker read as a flight (design M2 §3.2 point 3): when it left the ground and when it came back, every
/// revision of its plan and the one valid at take-off — the last filed before the wheels left the ground, or the first when
/// none was. The checks read that one (§1.8); the validator sees them all.
/// </summary>
public sealed record TrackedFlight(
    IvaoTrackerSessionDto Session,
    IReadOnlyList<IvaoFlightPlanDto> Plans,
    DateTime? TakeoffAt,
    DateTime? LandingAt,
    IvaoFlightPlanDto? PlanAtTakeoff)
{
    /// <summary>The aircraft as the plan at take-off says it, else as the session's summary does.</summary>
    public string? Aircraft => (PlanAtTakeoff?.AircraftIcao ?? Session.AircraftIcao)?.Trim().ToUpperInvariant();

    public string DepartureIcao => (PlanAtTakeoff?.DepartureIcao ?? Session.DepartureIcao ?? string.Empty).Trim().ToUpperInvariant();

    public string ArrivalIcao => (PlanAtTakeoff?.ArrivalIcao ?? Session.ArrivalIcao ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary><c>I</c>, <c>V</c>, <c>Y</c> or <c>Z</c>; <c>I</c> when the plan does not say, the strictest reading.</summary>
    public string FlightRules =>
        PlanAtTakeoff?.FlightRules is { Length: > 0 } rules && "IVYZ".Contains(rules[0], StringComparison.Ordinal)
            ? rules[..1]
            : "I";

    /// <summary>The session, its plans and its track, read into a flight. A pure function: the fixtures and the API agree.</summary>
    public static TrackedFlight Read(
        IvaoTrackerSessionDto session,
        IReadOnlyList<IvaoFlightPlanDto> plans,
        IReadOnlyList<IvaoTrackPointDto> track)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(track);

        var points = track.OrderBy(point => point.At).ToList();
        var takeoff = points.FirstOrDefault(point => !point.OnGround)?.At;
        var lastAirborne = points.LastOrDefault(point => !point.OnGround)?.At;
        var landing = lastAirborne is { } airborne ? points.FirstOrDefault(point => point.OnGround && point.At > airborne)?.At : null;

        var ordered = plans.OrderBy(plan => plan.Revision).ToList();
        var atTakeoff = takeoff is { } off
            ? ordered.LastOrDefault(plan => plan.FiledAt <= off) ?? ordered.FirstOrDefault()
            : ordered.LastOrDefault();

        return new TrackedFlight(session, ordered, takeoff, landing, atTakeoff);
    }
}
