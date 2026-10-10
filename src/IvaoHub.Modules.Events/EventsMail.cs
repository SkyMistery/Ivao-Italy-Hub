using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Modules.Events.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The mails of the events (design M4 §8.3), each member in their language: what the event is — its title —, the page it is read on,
/// and what the sentence of the type needs besides. Through the one notification service, which drops whoever switched the type off
/// or has no address; never to the pseudonym of somebody whose data was erased. One place for what every mail of the module says of
/// an event, so that a cancellation, new times and a booking taken away say it alike. A moment is written in UTC, and the sentence
/// around it says so.
/// </summary>
public sealed class EventsMail(
    HubDbContext hub,
    INotificationService notifications,
    LocaleCatalog catalog,
    IOptions<DivisionOptions> division)
{
    /// <summary>A moment as a mail writes it, the day and the time in UTC; the sentence around it says UTC.</summary>
    public static string Moment(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The page of an event, where a mail about it points.</summary>
    public static string PathOf(Event row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return $"/events/{row.Slug}";
    }

    /// <summary>An event cancelled (§2.3): to whoever booked one of its slots, with the note, in their language.</summary>
    public Task EventCancelledAsync(Event row, IEnumerable<int> vids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        return SendAsync(EventsNotifications.EventCancelled, row, vids, (data, locale) =>
            data["note"] = row.CancellationNote?.Resolve(locale, division.Value.DefaultLocale) ?? string.Empty, cancellationToken);
    }

    /// <summary>The times of an event changed (§8.3): to whoever booked one of its slots, with the new ones.</summary>
    public Task EventChangedAsync(Event row, IEnumerable<int> vids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        return SendAsync(EventsNotifications.EventChanged, row, vids, (data, _) =>
        {
            data["starts"] = Moment(row.StartsAtUtc);
            data["ends"] = Moment(row.EndsAtUtc);
        }, cancellationToken);
    }

    /// <summary>
    /// The reminder of the day before (§3.8, E6b): to one pilot, their flights of one event in one mail — each with its callsign and
    /// flight number, the aircraft they chose, where and when it leaves and lands and its stand, and under it the routes the flight
    /// operations wrote for its two airports, with their remarks in the pilot's language. The template says the rest: the times are in
    /// UTC, and a callsign taken on the network is flown with another.
    /// </summary>
    public Task BookingReminderAsync(
        Event row,
        int vid,
        IReadOnlyList<(EventBooking Booking, EventSlot Slot)> flights,
        IReadOnlyList<EventRoute> routes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(flights);
        ArgumentNullException.ThrowIfNull(routes);

        return SendAsync(EventsNotifications.BookingReminder, row, [vid], (data, locale) =>
        {
            data["count"] = flights.Count.ToString(CultureInfo.InvariantCulture);
            data["flights"] = string.Join('\n', flights.Select(flight => Flight(flight.Booking, flight.Slot, routes, locale)));
            data["mine"] = $"https://{division.Value.Domain}{MinePath}";
        }, cancellationToken);
    }

    /// <summary>The member's own page of the events (§7.1), where a booking is withdrawn.</summary>
    public const string MinePath = $"/events/{Staff.EventWriteDtoValidator.MinePage}";

    /// <summary>
    /// A booking taken away by the staff (§3.6): to its pilot, with the flight — a private one's as they wrote it (E7) — and the
    /// reason, as the staff wrote it, in their words.
    /// </summary>
    public Task BookingRemovedAsync(Event row, EventSlot slot, EventBooking booking, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(booking);

        var flight = BookedFlight.Of(slot, booking);

        return SendAsync(EventsNotifications.BookingRemoved, row, [booking.BookerVid], (data, _) =>
        {
            data["callsign"] = flight.Callsign ?? string.Empty;
            data["departure"] = flight.DepartureIcao ?? string.Empty;
            data["arrival"] = flight.ArrivalIcao ?? string.Empty;
            data["offBlock"] = flight.OffBlockUtc is { } offBlock ? Moment(offBlock) : string.Empty;
            data["reason"] = reason;
        }, cancellationToken);
    }

    /// <summary>
    /// The flight of a booked slot corrected by the staff (E6a, Carmine's answer 3 on #233): to its pilot, with the flight as it is
    /// now — callsign, airports, times, the types it admits — and the aircraft they chose, which the booking keeps.
    /// </summary>
    public Task BookingChangedAsync(Event row, EventSlot slot, EventBooking booking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(booking);

        return SendAsync(EventsNotifications.BookingChanged, row, [booking.BookerVid], (data, _) =>
        {
            data["callsign"] = slot.Callsign ?? booking.Callsign ?? string.Empty;
            data["departure"] = slot.DepartureIcao ?? string.Empty;
            data["arrival"] = slot.ArrivalIcao ?? string.Empty;
            data["offBlock"] = slot.OffBlockUtc is { } offBlock ? Moment(offBlock) : string.Empty;
            data["onBlock"] = slot.OnBlockUtc is { } onBlock ? Moment(onBlock) : string.Empty;
            data["types"] = string.Join('/', slot.AircraftTypes);
            data["aircraft"] = booking.AircraftIcao;
        }, cancellationToken);
    }

    /// <summary>
    /// One intent per language of the people told, each with the event's title in that language and its page: a cancellation of an
    /// event of four hundred slots is one or two intents, not four hundred.
    /// </summary>
    private async Task SendAsync(
        string type,
        Event row,
        IEnumerable<int> vids,
        Action<IDictionary<string, string>, string> fill,
        CancellationToken cancellationToken)
    {
        var options = division.Value;

        // Never the pseudonym of somebody whose data was erased: a negative VID, nobody's.
        var wanted = vids.Where(vid => vid > 0).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return;
        }

        var locales = await hub.Users.AsNoTracking()
            .Where(user => wanted.Contains(user.Vid))
            .Select(user => new { user.Vid, user.Locale })
            .ToDictionaryAsync(user => user.Vid, user => user.Locale, cancellationToken);

        foreach (var language in wanted.GroupBy(vid => locales.GetValueOrDefault(vid) ?? options.DefaultLocale, StringComparer.OrdinalIgnoreCase))
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = row.Title.Resolve(language.Key, options.DefaultLocale) ?? row.Slug,
                ["url"] = $"https://{options.Domain}{PathOf(row)}",
            };
            fill(data, language.Key);

            await notifications.QueueAsync(
                new NotificationIntent(type, [.. language.Select(NotificationRecipient.Member)], data),
                cancellationToken);
        }
    }

    /// <summary>The word before the stand of a flight, in a reminder.</summary>
    public const string StandKey = "events:mail.events.bookingReminder.stand";

    /// <summary>The word before a route of the flight operations, in a reminder.</summary>
    public const string RouteKey = "events:mail.events.bookingReminder.route";

    /// <summary>
    /// One flight of a reminder: callsign and flight number, the aircraft, where and when it leaves and lands — a private one's as its
    /// pilot wrote it (E7) —, the stand — the two words in the pilot's language —, and under it each route of the flight operations
    /// from its departure to its arrival, with its remarks.
    /// </summary>
    private string Flight(EventBooking booking, EventSlot slot, IReadOnlyList<EventRoute> routes, string locale)
    {
        var flight = BookedFlight.Of(slot, booking);
        var number = slot.FlightNumber is { Length: > 0 } flightNumber ? $" ({flightNumber})" : string.Empty;
        var stand = slot.Stand is { Length: > 0 } place ? $", {catalog.Resolve(locale, StandKey)} {place}" : string.Empty;
        var off = flight.OffBlockUtc is { } offBlock ? Moment(offBlock) : string.Empty;
        var on = flight.OnBlockUtc is { } onBlock ? Moment(onBlock) : string.Empty;

        List<string> lines =
        [
            $"- {flight.Callsign}{number}, {booking.AircraftIcao}: {flight.DepartureIcao} {off} → {flight.ArrivalIcao} {on}{stand}",
        ];

        foreach (var route in routes.Where(route => route.DepartureIcao == flight.DepartureIcao && route.ArrivalIcao == flight.ArrivalIcao))
        {
            var remarks = route.Remarks?.Resolve(locale, division.Value.DefaultLocale) is { Length: > 0 } text ? $" ({text})" : string.Empty;
            lines.Add($"  {catalog.Resolve(locale, RouteKey)}: {route.Route}{remarks}");
        }

        return string.Join('\n', lines);
    }
}
