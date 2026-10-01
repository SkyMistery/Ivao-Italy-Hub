using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>
/// One airport of an event, with its capacity (design M4 §1.3), <c>evt_event_airports</c>: once per event, in the order
/// <see cref="Ordinal"/> gives. The capacity is either in movements an hour, or in arrivals and departures an hour; the private
/// slots are generated from it (§3.2). An event about the whole division has none.
/// <para>A row of the staff that belongs to its event: in the care of the event's departments and answering with the event's
/// scope, copied at every write as the legs of a tour copy their tour's (§1.1; the copy arrives with the form, E3a). Its ICAO is
/// a plain column, checked against the airports the core knows, with no key towards the core.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.Area)]
public sealed class EventAirport : IOwnedByDepartment, IAuditable, IHasResourceScope
{
    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>An airport the core knows, upper case.</summary>
    public string Icao { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    public int? MaxMovementsPerHour { get; set; }

    public int? MaxArrivalsPerHour { get; set; }

    public int? MaxDeparturesPerHour { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>The event's: a permission granted on one event reaches its airports.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
