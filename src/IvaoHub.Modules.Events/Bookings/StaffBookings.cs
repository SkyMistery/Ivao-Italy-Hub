using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Bookings;

/// <summary>
/// A booking as the staff of an event read it in the tab «Bookings» of its page (design M4 §7.2, E6b): the pilot, named the way the
/// core names a person — `{ vid, name }`, so that the list writes a person whose data was erased with the core's word —, the flight of
/// its slot with the aircraft the pilot chose, when they booked, when the reminder of the day before left, and after the event what its check found
/// (E13a): when the flight started, when it was looked for — looked for and not found is a booking not flown. The flight is read off
/// the slot, never copied onto the booking: a slot corrected after the booking is listed as it is now.
/// <para>The flight is empty only where the engine maps one row with no page around it (<c>GET /api/events/bookings/{id}</c>): the
/// list reads the slots of a page in one query (<c>StaffBookings.RowsAsync</c>).</para>
/// <para>With E13b (note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 5 and 6) its event's title — the register of a pilot lists the
/// bookings of every event —, whether the staff took a booking not flown out of the pilot's record, who and why, and whether the reader may
/// take it out now: checked, not flown, not out yet, and <c>EventBookings.Edit</c> from the one handler on the row — never on their own.</para>
/// </summary>
public sealed record EventBookingDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    long SlotId,
    EventMemberDto Pilot,
    string? Callsign,
    string? FlightNumber,
    string AircraftIcao,
    string? DepartureIcao,
    DateTime? OffBlockUtc,
    string? ArrivalIcao,
    DateTime? OnBlockUtc,
    string? Stand,
    string? Rotation,
    int? Leg,
    DateTime CreatedAt,
    DateTime? RemindedAt,
    DateTime? FlownAt,
    DateTime? FlownCheckedAt,
    Localized<string>? EventTitle,
    EventMemberDto? UnflownExcusedBy,
    string? UnflownExcusedNote,
    bool Excusable);

/// <summary>
/// The bookings of an event as its staff list them (design M4 §7.2, E6b; the list E6a left to this phase): a resource of the CRUD engine
/// in read only, <c>/api/events/bookings</c>, with <c>EventBookings.View</c> on the event's care — the department filter and the policy
/// of the engine, as for the slots —, narrowed to one event with <c>filter[eventId]</c>. «Take away» is the verb E6a wrote beside it.
/// <para>A booking is a row of a member, which the global filter shows to any member and to no visitor: the staff read it past that
/// filter, as the engine reads every row of the back office, and the department filter and the policy decide.</para>
/// </summary>
public sealed class StaffBookings(EventsDbContext database, EventsPeople people, IAuthorizationService authorization, IHttpContextAccessor http)
{
    /// <summary>
    /// The bookings of the back office by the time of their flight at the airport of the event — the off block of a departure, the on
    /// block of an arrival, as the export of the gate manager orders them (§7.4) —, then as they were made. The engine keeps the order
    /// of its source when the reader sorts by nothing (<c>CrudOptions.DefaultOrder</c> is not set): the time is a column of the slot,
    /// and a default order can only name a column of the booking.
    /// </summary>
    public static IQueryable<EventBooking> ByFlightTime(DbContext context) =>
        from booking in CrudSource.BackOffice<EventBooking>(context)
        join slot in context.Set<EventSlot>() on booking.SlotId equals slot.Id
        orderby slot.IsArrival ? slot.OnBlockUtc : slot.OffBlockUtc, booking.Id
        select booking;

    /// <summary>A page of bookings with their flights and their pilots' names: one query for the slots and one for the names, never one per row.</summary>
    public async Task<IReadOnlyList<EventBookingDto>> RowsAsync(IReadOnlyList<EventBooking> bookings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bookings);

        var slotIds = bookings.Select(booking => booking.SlotId).Distinct().ToList();
        var slots = await database.Slots.AsNoTracking()
            .Where(slot => slotIds.Contains(slot.Id))
            .ToDictionaryAsync(slot => slot.Id, cancellationToken);
        var names = await people.NamesAsync(
            bookings.Select(booking => booking.BookerVid).Concat(bookings.Select(booking => booking.UnflownExcusedBy).OfType<int>()),
            cancellationToken);
        var eventIds = bookings.Select(booking => booking.EventId).Distinct().ToList();
        var titles = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.Title, cancellationToken);

        var excusable = new HashSet<long>();
        foreach (var booking in bookings.Where(booking => booking.FlownCheckedAt is not null && booking.FlownAt is null && booking.UnflownExcusedBy is null))
        {
            if (http.HttpContext is { } context && (await authorization.AuthorizeAsync(context.User, booking, EventsPermissions.BookingsEdit)).Succeeded)
            {
                excusable.Add(booking.Id);
            }
        }

        return [.. bookings.Select(booking => Row(booking, slots.GetValueOrDefault(booking.SlotId), names, titles.GetValueOrDefault(booking.EventId), excusable.Contains(booking.Id)))];
    }

    /// <summary>
    /// One booking with its flight (<see cref="BookedFlight"/>: a public slot's own, a private one's as its pilot wrote it, E7) — none
    /// known, the slot not read, the flight empty but for a private callsign — and the name of its pilot.
    /// </summary>
    public static EventBookingDto Row(
        EventBooking booking,
        EventSlot? slot,
        IReadOnlyDictionary<int, string> names,
        Localized<string>? eventTitle = null,
        bool excusable = false)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var flight = slot is null ? new BookedFlight(booking.Callsign, null, null, null, null) : BookedFlight.Of(slot, booking);

        return new EventBookingDto(
            booking.Id,
            booking.EventId,
            booking.OwnerDepartment,
            booking.SlotId,
            EventsPeople.Member(booking.BookerVid, names),
            flight.Callsign,
            slot?.FlightNumber,
            booking.AircraftIcao,
            flight.DepartureIcao,
            flight.OffBlockUtc,
            flight.ArrivalIcao,
            flight.OnBlockUtc,
            slot?.Stand,
            slot?.RotationCode,
            slot?.RotationLeg,
            booking.CreatedAt,
            booking.RemindedAt,
            booking.FlownAt,
            booking.FlownCheckedAt,
            eventTitle,
            booking.UnflownExcusedBy is { } by ? EventsPeople.Member(by, names) : null,
            booking.UnflownExcusedNote,
            excusable);
    }
}
