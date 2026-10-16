using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.People;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// The seats of an event as its staff list them (design M4 §7.2, E17): a page with the members' names — read from the core's one reader of
/// names (<see cref="MemberNames"/>, E10m), one query, never one per row — and the activities of the page, read once.
/// </summary>
public sealed class StaffSeats(EventsDbContext database, HubDbContext hub)
{
    public async Task<IReadOnlyList<EventSeatDto>> RowsAsync(IReadOnlyList<EventActivitySeat> seats, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seats);

        var names = await MemberNames.ReadAsync(hub, seats.Select(seat => seat.Vid), cancellationToken);
        var activityIds = seats.Select(seat => seat.ActivityId).Distinct().ToList();
        var activities = await CrudSource.BackOffice<EventActivity>(database).AsNoTracking()
            .Where(activity => activityIds.Contains(activity.Id))
            .ToDictionaryAsync(activity => activity.Id, cancellationToken);

        return [.. seats.Select(seat => Row(seat, activities.GetValueOrDefault(seat.ActivityId), names))];
    }

    /// <summary>One seat with its member's name and its activity — none known, a row read alone.</summary>
    public static EventSeatDto Row(EventActivitySeat seat, EventActivity? activity, IReadOnlyDictionary<int, string> names)
    {
        ArgumentNullException.ThrowIfNull(seat);

        return new EventSeatDto(
            seat.Id,
            seat.EventId,
            seat.OwnerDepartment,
            EventsPeople.Member(seat.Vid, names),
            seat.ActivityId,
            activity?.Name ?? Localized<string>.Empty,
            seat.StartsAtUtc,
            seat.StartsAtUtc.AddMinutes(activity?.SlotMinutes ?? 0),
            seat.CreatedAt);
    }
}
