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
    /// arrive with their rows — the pilots who booked with E6a, whoever registered with E16, the controllers with a shift with E12.
    /// </summary>
    public const string EventCancelled = "events.eventCancelled";

    /// <summary>
    /// The start or the end of an event changed (§8.3; E3b): the same audience as a cancellation, who arrives with the same rows —
    /// the pilots who booked since E6a, whoever registered since E16, the controllers with a shift since E12.
    /// </summary>
    public const string EventChanged = "events.eventChanged";

    /// <summary>The staff took a pilot's booking away, with the reason (§3.6; E6a): to that pilot.</summary>
    public const string BookingRemoved = "events.bookingRemoved";

    /// <summary>
    /// The staff corrected the flight of a booked slot — its callsign, its times, its airports or the aircraft types it admits — and
    /// the booking stays (E6a, Carmine's answer 3 on #233): to its pilot, with the flight as it is now.
    /// </summary>
    public const string BookingChanged = "events.bookingChanged";

    /// <summary>
    /// The reminder of the day before (§3.8; E6b): to a pilot, <c>reminderLeadHours</c> before the off block of a booking, with the
    /// near bookings of the same event in the same mail.
    /// </summary>
    public const string BookingReminder = "events.bookingReminder";

    /// <summary>
    /// A member registered to an event in person, or changed their answers (§4-bis.2; E16): to them, with their answers as they are now
    /// — and the seats of the activities with E17.
    /// </summary>
    public const string RegistrationReceived = "events.registrationReceived";

    /// <summary>
    /// The staff took a member's registration away, with the reason (E16; Carmine's answer 2 on issue #253): to that member,
    /// as for a booking.
    /// </summary>
    public const string RegistrationRemoved = "events.registrationRemoved";

    /// <summary>
    /// A controller's shifts of a roster (§4.4; E12): at its publication, one mail per controller per event with their shifts, and after it
    /// each shift added to them or handed over to them, at once.
    /// </summary>
    public const string AtcShiftAssigned = "events.atcShiftAssigned";

    /// <summary>A shift of a published roster moved to other hours or another position (§4.4; E12): to its controller, as it was and as it is.</summary>
    public const string AtcShiftChanged = "events.atcShiftChanged";

    /// <summary>A shift of a published roster taken away, or given to somebody else (§4.4; E12): to whoever held it and knew it.</summary>
    public const string AtcShiftRemoved = "events.atcShiftRemoved";

    /// <summary>The staff approved or refused a request to hand a shift over (§4.4-bis; E12): to whoever asked, with the staff's note.</summary>
    public const string AtcShiftTransferDecided = "events.atcShiftTransferDecided";

    public static readonly IReadOnlyList<string> All =
    [
        EventCancelled,
        EventChanged,
        BookingRemoved,
        BookingChanged,
        BookingReminder,
        RegistrationReceived,
        RegistrationRemoved,
        AtcShiftAssigned,
        AtcShiftChanged,
        AtcShiftRemoved,
        AtcShiftTransferDecided,
    ];
}
