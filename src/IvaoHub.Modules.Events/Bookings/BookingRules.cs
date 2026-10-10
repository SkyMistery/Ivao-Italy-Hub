namespace IvaoHub.Modules.Events.Bookings;

/// <summary>
/// The time a booking holds its pilot (design M4 §3.5): from the off block to the on block of the flight of its slot. A public
/// slot is its own flight; a private one is its time at the airport of the event and the other time the pilot writes (E7).
/// </summary>
/// <param name="FromUtc">The off block: when the pilot is held from.</param>
/// <param name="ToUtc">The on block: when they are free again.</param>
public readonly record struct BookingInterval(DateTime FromUtc, DateTime ToUtc)
{
    /// <summary>
    /// A slot's flight alone, its off block to its on block: a public slot's; none for a private one, whose flight is its pilot's
    /// to write.
    /// </summary>
    public static BookingInterval? Of(EventSlot slot) => Of(slot, booking: null);

    /// <summary>
    /// The flight of a booking, its off block to its on block (<see cref="BookedFlight"/>): a public slot's own, a private one's
    /// with the other time its pilot wrote (E7); none while either is not known — a private slot nobody booked.
    /// </summary>
    public static BookingInterval? Of(EventSlot slot, EventBooking? booking) =>
        BookedFlight.Of(slot, booking) is { OffBlockUtc: { } offBlock, OnBlockUtc: { } onBlock }
            ? new BookingInterval(offBlock, onBlock)
            : null;
}

/// <summary>
/// The flight a booking is for (design M4 §1.5, §1.6, §3.4, E7) — what the pilot's list, the staff's, the mails, the reminder and the
/// export say of it, read in this one place. A <b>public</b> slot is its own flight, whoever books it. A <b>private</b> slot is its
/// airport of the event and its time there; the pilot who books it writes the rest — the callsign, the other airport and the time
/// there —, which stays on the booking: an arrival leaves the other airport at that time and lands at the slot's, a departure leaves
/// at the slot's and lands at the other airport at that time. A private slot nobody booked is half a flight: its airport and its
/// time, nothing else.
/// </summary>
public sealed record BookedFlight(
    string? Callsign,
    string? DepartureIcao,
    DateTime? OffBlockUtc,
    string? ArrivalIcao,
    DateTime? OnBlockUtc)
{
    public static BookedFlight Of(EventSlot slot, EventBooking? booking)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (slot.Kind == SlotKind.Public)
        {
            return new BookedFlight(slot.Callsign, slot.DepartureIcao, slot.OffBlockUtc, slot.ArrivalIcao, slot.OnBlockUtc);
        }

        return slot.IsArrival
            ? new BookedFlight(booking?.Callsign, booking?.OtherIcao, booking?.OtherTimeUtc, slot.EventAirportIcao, slot.OnBlockUtc)
            : new BookedFlight(booking?.Callsign, slot.EventAirportIcao, slot.OffBlockUtc, booking?.OtherIcao, booking?.OtherTimeUtc);
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
    /// during the event too, slot by slot. None for a slot without one: a private slot closes with the flight its pilot writes.
    /// </summary>
    public static DateTime? ClosesAt(EventSlot slot) => ClosesAt(slot, booking: null);

    /// <summary>
    /// When a booking closes: the off block of its flight (§3.3, §3.6; E7) — a public slot's EOBT; a private departure's time at the
    /// airport of the event; a private arrival's off block, the time its pilot leaves the other airport. None while the flight is not
    /// known: a private slot is booked with its flight, and checked then.
    /// </summary>
    public static DateTime? ClosesAt(EventSlot slot, EventBooking? booking) => BookingInterval.Of(slot, booking)?.FromUtc;

    /// <summary>Whether a slot is still open at <paramref name="now"/>: its off block is to come. The off block itself is closed.</summary>
    public static bool IsOpen(EventSlot slot, DateTime now) => IsOpen(slot, booking: null, now);

    /// <summary>Whether a booking — or one about to be made — is still open at <paramref name="now"/>: the off block of its flight is to come.</summary>
    public static bool IsOpen(EventSlot slot, EventBooking? booking, DateTime now) => ClosesAt(slot, booking) is { } closes && now < closes;

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

    /// <summary>A public slot sent to the verb of the private ones (E7): it is booked as it is, with «Book».</summary>
    public const string SlotNotPrivateKey = "events:errors.slotNotPrivate";

    /// <summary>The off block a pilot wrote for a private arrival — when they leave the other airport — has passed (E7).</summary>
    public const string OffBlockPassedKey = "events:errors.offBlockPassed";

    /// <summary>A linked departure asked for with a departure: only an arrival brings one (§3.4, E7).</summary>
    public const string PairedOnlyForArrivalKey = "events:errors.pairedOnlyForArrival";

    /// <summary>The linked departure is not a private departure of the event from the airport the arrival lands at (E7).</summary>
    public const string PairedNotADepartureKey = "events:errors.pairedNotADeparture";

    /// <summary>The linked departure leaves before the arrival has landed, or less than the gap after it (E7).</summary>
    public const string PairedTooSoonKey = "events:errors.pairedTooSoon";
}
