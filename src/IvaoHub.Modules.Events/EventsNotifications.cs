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
    /// arrive with their rows (E6a, E12, E16), and until then nobody is told.
    /// </summary>
    public const string EventCancelled = "events.eventCancelled";

    /// <summary>
    /// The times of an event changed (§8.3; E3b): the same audience as a cancellation, who arrives with the same rows — until then
    /// nobody is told.
    /// </summary>
    public const string EventChanged = "events.eventChanged";

    public static readonly IReadOnlyList<string> All = [EventCancelled, EventChanged];
}
