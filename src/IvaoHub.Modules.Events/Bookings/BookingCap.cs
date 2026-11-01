namespace IvaoHub.Modules.Events.Bookings;

/// <summary>
/// The most bookings a member makes in one event (issue #314; note 2026-11-01-il-tetto-delle-prenotazioni), pure: <c>maxBookingsPerEvent</c>
/// of the settings, the same for the whole division. Counted as a pilot books: <b>a rotation is one booking</b>, whatever its legs and however
/// they were booked — whole, or a leg at a time —; any other slot is one, public or private, a private arrival's linked departure too. A
/// rotation is told by its code as the database keeps it, case and all (E5: R1 and r1 are two).
/// <para>Only what grows the count is refused: a leg of a rotation the pilot holds already passes, and a pilot over a cap lowered since keeps
/// what they booked.</para>
/// </summary>
public static class BookingCap
{
    /// <summary>A booking past the most of the division in the event.</summary>
    public const string Key = "events:errors.bookingMaxPerEvent";

    /// <summary>How many bookings these slots are: a rotation once, any other slot each.</summary>
    public static int Count(IEnumerable<EventSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var rotations = new HashSet<string>(StringComparer.Ordinal);
        return slots.Count(slot => RotationOf(slot) is not { } rotation || rotations.Add(rotation));
    }

    /// <summary>
    /// Why one more booking of <paramref name="wanted"/> passes the most of the division, or none: <paramref name="held"/> are the slots of the
    /// bookings the pilot keeps in the event — saved, and booked a moment before in the same request.
    /// </summary>
    public static string? Refusal(int max, IReadOnlyCollection<EventSlot> held, EventSlot wanted)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(wanted);

        var before = Count(held);
        var after = Count([.. held, wanted]);
        return after > before && after > max ? Key : null;
    }

    private static string? RotationOf(EventSlot slot) =>
        slot.Kind == SlotKind.Public && slot.RotationCode is { Length: > 0 } rotation ? rotation : null;
}
