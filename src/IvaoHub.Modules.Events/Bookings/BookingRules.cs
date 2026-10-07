namespace IvaoHub.Modules.Events.Bookings;

/// <summary>
/// The time a booking holds its pilot (design M4 §3.5): from the off block to the on block of the flight of its slot. A public
/// slot is its own flight; a private one is its time at the airport of the event and the other time the pilot writes (E7).
/// </summary>
/// <param name="FromUtc">The off block: when the pilot is held from.</param>
/// <param name="ToUtc">The on block: when they are free again.</param>
public readonly record struct BookingInterval(DateTime FromUtc, DateTime ToUtc)
{
    /// <summary>A public slot's flight, its off block to its on block; none for a slot without both, which no public slot is.</summary>
    public static BookingInterval? Of(EventSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        return slot is { Kind: SlotKind.Public, OffBlockUtc: { } offBlock, OnBlockUtc: { } onBlock }
            ? new BookingInterval(offBlock, onBlock)
            : null;
    }
}

/// <summary>
/// The rules of a booking that need nothing but their values (design M4 §3.3, §3.5, §3.6; note 2026-09-29-gli-slot-e-le-prenotazioni
/// §2.5–§2.6): when a slot can be booked, and whether two bookings of one pilot go together. Pure, and tested as such; the verbs
/// read the rows and the clock, and ask here.
/// </summary>
public static class BookingRules
{
    /// <summary>
    /// Whether two bookings of the same pilot in the same event go together: at least <paramref name="gapMinutes"/> between the
    /// end of one and the start of the other, one way or the other (§3.5, c1, c2). Exactly the gap is enough — the minutes the
    /// chains of a rotation keep between two legs (§3.1), so that a whole rotation is bookable by one pilot. No limit of number.
    /// </summary>
    public static bool Compatible(BookingInterval first, BookingInterval second, int gapMinutes)
    {
        var gap = TimeSpan.FromMinutes(gapMinutes);
        return second.FromUtc >= first.ToUtc + gap || first.FromUtc >= second.ToUtc + gap;
    }

    /// <summary>
    /// When a public slot closes: its off block (EOBT, §3.3, c2). It is booked, and withdrawn, while its off block is still to come —
    /// during the event too, slot by slot. None for a slot without one.
    /// </summary>
    public static DateTime? ClosesAt(EventSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return slot.Kind == SlotKind.Public ? slot.OffBlockUtc : null;
    }

    /// <summary>Whether a slot is still open at <paramref name="now"/>: its off block is to come. The off block itself is closed.</summary>
    public static bool IsOpen(EventSlot slot, DateTime now) => ClosesAt(slot) is { } closes && now < closes;

    /// <summary>
    /// Why the bookings of an event are closed at <paramref name="now"/>, as the i18n key a pilot reads, or null when they are open:
    /// an event cancelled takes no booking, and one whose bookings do not open yet takes none before that moment (§3.3). Whether
    /// the event is seen at all is asked before, by whoever reads it: an event not seen is not found.
    /// </summary>
    public static string? EventClosed(Event row, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.CancelledAt is not null ? CancelledKey
            : row.BookingOpensAtUtc is not { } opens || now < opens ? NotOpenKey
            : null;
    }

    /// <summary>The event is cancelled: its slots are booked no more.</summary>
    public const string CancelledKey = "events:errors.bookingCancelledEvent";

    /// <summary>The bookings of the event are not open yet.</summary>
    public const string NotOpenKey = "events:errors.bookingNotOpen";

    /// <summary>The off block of the slot has passed: booked and withdrawn no more.</summary>
    public const string ClosedKey = "events:errors.slotClosed";

    /// <summary>The slot is the pilot's already.</summary>
    public const string YoursKey = "events:errors.slotAlreadyYours";

    /// <summary>Another pilot has the slot — a moment ago, or as the database answered (§10.1).</summary>
    public const string TakenKey = "events:errors.slotJustTaken";

    /// <summary>Too close to a booking the pilot already has in the event (§3.5).</summary>
    public const string IncompatibleKey = "events:errors.bookingIncompatible";

    /// <summary>An aircraft type the slot does not allow.</summary>
    public const string AircraftKey = "events:errors.aircraftNotAllowed";

    /// <summary>
    /// A deadlock: the database rolled the booking back, nothing was booked, and the same request may go through a moment later —
    /// the title of a 409, never «taken» (the review of #233, point 3).
    /// </summary>
    public const string TryAgainKey = "events:errors.bookingTryAgain";
}
