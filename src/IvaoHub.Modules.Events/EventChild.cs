using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>
/// A row of the staff that belongs to an event — an airport with its capacity (E3a), and after it the routes, the slots, the ATC
/// positions and the rules of award — in the care of the event's departments and answering with the event's scope (design M4
/// §1.1, note 2026-09-29-chi-lavora-sugli-eventi §2.6): its care is copied from the event at every write, before its permission
/// is asked (<c>CrudOptions.BeforeAuthorize</c>), as a leg of a tour takes its tour's. The one handler and the interceptor's
/// guard then read it as they read the event.
/// </summary>
public interface IEventChild : IOwnedByDepartment
{
    long EventId { get; }

    new Department OwnerDepartment { get; set; }

    new int OwnerDepartmentMask { get; set; }
}
