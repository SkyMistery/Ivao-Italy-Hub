namespace IvaoHub.Modules.Events;

/// <summary>
/// The kinds of notification of the events (design M4 §8.3), declared to the core through <c>IModule.NotificationTypes</c>. The
/// words are in the module's language file: the mail under <c>mail.{type}</c>, the label of the profile under
/// <c>notifications.{name}</c> of the <c>events</c> namespace. Each is declared by the phase that makes the change it tells, and
/// sent once the rows of the people it tells exist.
/// </summary>
public static class EventsNotifications
{
    /// <summary>
    /// An event was cancelled, with its note (§2.3; E3a). Its audience is whoever booked a slot, has a shift or registered: they
    /// arrive with their rows — the pilots who booked with E6a, the controllers with E12, whoever registered with E16.
    /// </summary>
    public const string EventCancelled = "events.eventCancelled";

    /// <summary>
    /// The start or the end of an event changed (§8.3; E3b): the same audience as a cancellation, who arrives with the same rows —
    /// the pilots who booked since E6a.
    /// </summary>
    public const string EventChanged = "events.eventChanged";

    /// <summary>The staff took a pilot's booking away, with the reason (§3.6; E6a): to that pilot.</summary>
    public const string BookingRemoved = "events.bookingRemoved";

    /// <summary>
    /// The staff corrected the flight of a booked slot — its callsign, its times, its airports or the aircraft types it admits — and
    /// the booking stays (E6a, Carmine's answer 3 on #233): to its pilot, with the flight as it is now.
    /// </summary>
    public const string BookingChanged = "events.bookingChanged";

    public static readonly IReadOnlyList<string> All = [EventCancelled, EventChanged, BookingRemoved, BookingChanged];
}
