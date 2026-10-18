using IvaoHub.Modules.Events.Settings;

namespace IvaoHub.Modules.Events.Registers;

/// <summary>The limits of whoever books and does not fly in one event (§3.7): the hours of a band, the bookings in each, the bookings in the event.</summary>
public sealed record PilotLimits(int WindowHours, int MaxPerWindow, int MaxPerEvent);

/// <summary>
/// The rules of the limits (design M4 §3.7, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 7 and 8), pure: a pilot with at
/// least <c>unflownThreshold</c> bookings not flown keeps at most <c>restrictedMaxPerWindow</c> bookings in each band of
/// <c>restrictedWindowHours</c> hours of an event — fixed bands from its start — and <c>restrictedMaxPerEvent</c> in it; the event's own
/// numbers replace the settings' one by one. A booking falls in the band of its slot's time at the airport of the event.
/// </summary>
public static class BookingLimits
{
    /// <summary>A booking past the most in its band.</summary>
    public const string WindowKey = "events:errors.bookingRestrictedWindow";

    /// <summary>A booking past the most in the event.</summary>
    public const string EventKey = "events:errors.bookingRestrictedEvent";

    /// <summary>The limits valid in an event: its own where it has them, the settings' otherwise.</summary>
    public static PilotLimits Of(Event row, EventsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(settings);

        return new PilotLimits(
            row.RestrictedWindowHours ?? settings.RestrictedWindowHours,
            row.RestrictedMaxPerWindow ?? settings.RestrictedMaxPerWindow,
            row.RestrictedMaxPerEvent ?? settings.RestrictedMaxPerEvent);
    }

    /// <summary>
    /// A slot's time at the airport of the event (reading 7): the off block of a departure, the on block of an arrival — the slot's own,
    /// never the time a pilot writes for the other airport of a private flight. A slot with only the other time says that one.
    /// </summary>
    public static DateTime AtTheEventAirport(EventSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        return (slot.IsArrival ? slot.OnBlockUtc ?? slot.OffBlockUtc : slot.OffBlockUtc ?? slot.OnBlockUtc).GetValueOrDefault();
    }

    /// <summary>The band a moment falls in, counted from the start of the event: negative before it, in the six hours a slot may lie there.</summary>
    public static long Band(DateTime startsAtUtc, DateTime time, int windowHours) =>
        (long)Math.Floor((time - startsAtUtc).TotalHours / Math.Max(windowHours, 1));

    /// <summary>
    /// Why one more booking at <paramref name="wanted"/> passes the limits, or none: <paramref name="held"/> are the times of the bookings the
    /// pilot keeps in the event — saved, and booked a moment before in the same request.
    /// </summary>
    public static string? Refusal(PilotLimits limits, DateTime startsAtUtc, IReadOnlyCollection<DateTime> held, DateTime wanted)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(held);

        if (held.Count + 1 > limits.MaxPerEvent)
        {
            return EventKey;
        }

        var band = Band(startsAtUtc, wanted, limits.WindowHours);
        return held.Count(time => Band(startsAtUtc, time, limits.WindowHours) == band) + 1 > limits.MaxPerWindow ? WindowKey : null;
    }

    /// <summary>
    /// Until when a pilot has the limits (reading 8): the moment the bookings counted fall below the threshold — with <c>n</c> counted and
    /// threshold <c>s</c>, the <c>(n − s + 1)</c>-th to leave the record. None when they are below it already. <paramref name="leaving"/> is
    /// when each counted booking leaves the record: the end of its event and the months the bookings are kept.
    /// </summary>
    public static DateTime? Until(IReadOnlyCollection<DateTime> leaving, int threshold)
    {
        ArgumentNullException.ThrowIfNull(leaving);

        if (threshold < 1 || leaving.Count < threshold)
        {
            return null;
        }

        return leaving.Order().ElementAt(leaving.Count - threshold);
    }
}
