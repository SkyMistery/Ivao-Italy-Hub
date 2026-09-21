using IvaoHub.Core.Division;

namespace IvaoHub.Modules.FlightOps.Shape;

/// <summary>
/// A row that belongs to a tour — a leg, a hub, a rotation, a callsign constraint — and is in the care of the tour's
/// departments, copied at every write and followed when the tour's change (Carmine, 18 September 2026, note
/// <c>2026-09-18-le-leg-dei-tour</c>): the one handler and the interceptor's guard read it as they read the tour.
/// </summary>
public interface ITourChild : IOwnedByDepartment
{
    long TourId { get; }

    new Department OwnerDepartment { get; set; }

    new int OwnerDepartmentMask { get; set; }
}
