using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// What a write of an event may refuse only by looking at other rows (design M4 §1.2), run by the CRUD engine before every
/// save: an address no other event has, a kind the calendar has and still offers — asked when it is chosen, as a calendar
/// entry's is, so an event whose kind the division retired since stays saveable —, and an event about the whole division with
/// no airports of its own (§1.3).
/// </summary>
public sealed class EventSaving(EventsDbContext database, HubDbContext hub)
{
    public async Task<IReadOnlyDictionary<string, string[]>?> PrepareAsync(Event row, bool isNew, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var problems = new Refusals();

        if (await CrudSource.BackOffice<Event>(database).AnyAsync(other => other.Slug == row.Slug && other.Id != row.Id, cancellationToken))
        {
            problems.Add("slug", "events:errors.slugTaken");
        }

        if (isNew || !string.Equals((string)database.Entry(row).Property(nameof(Event.Kind)).OriginalValue!, row.Kind, StringComparison.Ordinal))
        {
            // Spelled as the calendar spells it: the database would find "RFO" for "rfo", the browser would not (note of E1, §3).
            var known = await hub.CalendarKinds.AsNoTracking()
                .Where(kind => kind.Key == row.Kind && kind.IsActive)
                .Select(kind => kind.Key)
                .ToListAsync(cancellationToken);

            if (!known.Contains(row.Kind, StringComparer.Ordinal))
            {
                problems.Add("kind", "errors.calendar.kindUnknown");
            }
        }

        if (row.WholeDivision && !isNew && await database.Airports.AnyAsync(airport => airport.EventId == row.Id, cancellationToken))
        {
            problems.Add("wholeDivision", "events:errors.wholeDivisionHasAirports");
        }

        return problems.IsEmpty ? null : problems.Errors;
    }

    /// <summary>
    /// Deleting an event (§2.3): only one nobody took part in — no row of a member points at it. E3a has no such rows yet; the
    /// first table of them (the bookings, E6a) refuses here, and every later one adds its own check, so whoever took part is
    /// never deleted with the event: it is cancelled instead. Its airports go with it, through the same unit of work, so each
    /// leaves its row in the audit.
    /// </summary>
    public async Task DeleteAsync(Event row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        database.Airports.RemoveRange(await database.Airports.Where(airport => airport.EventId == row.Id).ToListAsync(cancellationToken));
        database.Events.Remove(row);
    }
}

/// <summary>
/// What every row of the staff of an event shares on its way through the CRUD engine (<see cref="IEventChild"/>): its event
/// found, and its care taken from it, before the permission is asked (<c>CrudOptions.BeforeAuthorize</c>), as the rows of a
/// tour take their tour's.
/// </summary>
public sealed class EventChildren(EventsDbContext database)
{
    /// <summary>The event of the row, from the back office's rows.</summary>
    public Task<Event?> EventAsync(long eventId, CancellationToken cancellationToken) =>
        CrudSource.BackOffice<Event>(database).AsNoTracking().FirstOrDefaultAsync(row => row.Id == eventId, cancellationToken);

    /// <summary>The event's department and care on the row; a refusal when the event does not exist.</summary>
    public async Task<(Event? Event, IReadOnlyDictionary<string, string[]>? Refusal)> AdoptAsync(
        IEventChild row,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var parent = await EventAsync(row.EventId, cancellationToken);
        if (parent is null)
        {
            return (null, Refusal("eventId", "events:errors.eventUnknown"));
        }

        row.OwnerDepartment = parent.OwnerDepartment;
        row.OwnerDepartmentMask = parent.OwnerDepartmentMask;

        return (parent, null);
    }

    public static IReadOnlyDictionary<string, string[]> Refusal(string field, string key) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [key] };
}
