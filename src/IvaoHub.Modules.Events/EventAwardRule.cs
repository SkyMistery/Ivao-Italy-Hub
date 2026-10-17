using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The closed list a rule of award chooses its criterion from (design M4 §1.9): a new one is a line here and in
/// <c>Reports/AwardRules</c>, never an expression. The first five read a pilot's report, the last two a controller's.
/// </summary>
public enum AwardCriterion
{
    /// <summary>At least one flight verified: «pilot support».</summary>
    AnyFlight,

    /// <summary>At least <c>count</c> flights verified.</summary>
    MinFlights,

    /// <summary>A verified flight from <c>departure</c> to <c>arrival</c>.</summary>
    Route,

    /// <summary>At least <c>count</c> verified flights from <c>departure</c> to <c>arrival</c>.</summary>
    RouteTimes,

    /// <summary>A verified flight between two airports at least <c>distance</c> nautical miles apart.</summary>
    MinLegDistance,

    /// <summary>At least one shift verified: «ATC support».</summary>
    AnyShift,

    /// <summary>At least <c>minutes</c> verified on a position, of one kind when the rule names one.</summary>
    MinAtcMinutes,
}

/// <summary>
/// A rule of award of an event (design M4 §1.9, §5.4, E14b; note 2026-10-17-le-regole-di-award), <c>evt_award_rules</c>: «whoever's report of
/// this kind is accepted and meets this criterion is pointed out for this award». Accepting a report evaluates the rules of its event on its
/// verified items, and each rule met becomes a signal in the core's queue of awards; a person assigns, never the code (plan §9.7).
/// <para>A row of the staff that belongs to its event (<see cref="IEventChild"/>), in the event's own area, <c>Events</c>: whoever edits the
/// event writes its rules (§6.1). The parameters are columns of their own, each read by the criteria that take it and empty for the others; the
/// award is a plain column checked against the core's catalogue, with no key towards the core.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.Area)]
public sealed class EventAwardRule : IEventChild, IAuditable, IHasResourceScope
{
    /// <summary>The longest kind of position, as the core keeps it.</summary>
    public const int MaxPositionTypeLength = 16;

    /// <summary>The most rules an event holds: every award an evening could give, with room to spare.</summary>
    public const int MaxPerEvent = 50;

    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>The award the rule points its members out for, in the core's catalogue.</summary>
    public long AwardId { get; set; }

    /// <summary>The kind of report the rule reads: the criterion's, written by the server.</summary>
    public ReportKind Kind { get; set; }

    public AwardCriterion Criterion { get; set; }

    /// <summary><see cref="AwardCriterion.Route"/> and <see cref="AwardCriterion.RouteTimes"/>: where the flight leaves from.</summary>
    public string? DepartureIcao { get; set; }

    /// <summary><see cref="AwardCriterion.Route"/> and <see cref="AwardCriterion.RouteTimes"/>: where it lands.</summary>
    public string? ArrivalIcao { get; set; }

    /// <summary><see cref="AwardCriterion.MinFlights"/> and <see cref="AwardCriterion.RouteTimes"/>: how many flights.</summary>
    public int? Count { get; set; }

    /// <summary><see cref="AwardCriterion.MinLegDistance"/>: the nautical miles between a flight's two airports.</summary>
    public int? DistanceNm { get; set; }

    /// <summary><see cref="AwardCriterion.MinAtcMinutes"/>: the minutes on a position.</summary>
    public int? Minutes { get; set; }

    /// <summary><see cref="AwardCriterion.MinAtcMinutes"/>, optional: the kind of position, as the core spells it (<c>TWR</c>, <c>APP</c>…).</summary>
    public string? PositionType { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>The event's: a permission granted on one event reaches its rules.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
