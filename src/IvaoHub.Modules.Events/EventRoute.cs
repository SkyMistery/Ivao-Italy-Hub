using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Events;

/// <summary>
/// A route of an event (design M4 §1.4), <c>evt_routes</c>: from one airport to another, the route to file, and what a pilot
/// should know about it. The flight operations write them (c1): a row of its own in an area of its own, <c>EventRoutes</c>, so
/// that whoever writes the routes of an event does not write the event (note 2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento).
/// <para>A row of the staff that belongs to its event (<see cref="IEventChild"/>): in the care of the event's departments and
/// answering with the event's scope, copied at every write as an airport of the event copies them (§1.1). The two airports are
/// plain columns, checked against the airports the core knows, with no key towards the core. The page of the event shows the
/// routes to whoever sees the event (E4); the reminder of the day before (E6b) and «Duplicate» (E8b) read them too.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.RoutesArea)]
public sealed class EventRoute : IEventChild, IAuditable, IHasResourceScope
{
    /// <summary>The longest route: a flight plan's route across a continent, with room to spare.</summary>
    public const int MaxRouteLength = 1024;

    /// <summary>The longest remark, in each language: a sentence or two, not a briefing — the description of the event is that.</summary>
    public const int MaxRemarksLength = 500;

    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>An airport the core knows, upper case.</summary>
    public string DepartureIcao { get; set; } = string.Empty;

    /// <summary>An airport the core knows, upper case.</summary>
    public string ArrivalIcao { get; set; } = string.Empty;

    /// <summary>The route to file, as a pilot writes it in the flight plan: words of the network, never translated.</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>What a pilot should know about it, in every language of the division when written: the page shows it to everybody.</summary>
    public Localized<string>? Remarks { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>The event's: a permission granted on one event reaches its routes.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
