using FluentValidation;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Staff;

namespace IvaoHub.Modules.Events.Bookings;

/// <summary>
/// What a pilot sends to book a public slot (design M4 §3.3), or the whole rotation of one (§3.3, §17.1 n.16): the slot, and the
/// aircraft type they fly among those it allows — for a rotation, the one they fly every leg with.
/// </summary>
public sealed record BookingRequest(long SlotId, string? AircraftIcao);

/// <summary>What the staff send to take a booking away (§3.6): the reason, which the pilot reads in the mail.</summary>
public sealed record BookingRemovalRequest(string? Reason);

/// <summary>
/// What a pilot sends to book a private slot (design M4 §3.4, E7): the slot — an airport of the event, a direction, a time there —
/// and the flight they fly through it: the callsign, the aircraft type they declare, the other airport and the time there — where an
/// arrival leaves from and when, where a departure goes and when it lands. An arrival may bring its <b>linked departure</b>: a private
/// departure from the same airport, booked in the same request, so that both get the same gate; the same aircraft flies both.
/// </summary>
public sealed record PrivateBookingRequest(
    long SlotId,
    string? AircraftIcao,
    string? Callsign,
    string? OtherIcao,
    DateTime? OtherTimeUtc,
    PrivateDepartureRequest? Departure);

/// <summary>The linked departure of a private arrival (§3.4): its slot, and its own flight — the callsign, where it goes and when it lands there.</summary>
public sealed record PrivateDepartureRequest(long SlotId, string? Callsign, string? OtherIcao, DateTime? OtherTimeUtc);

/// <summary>What booking a private slot made (§3.4): the booking, and the linked departure booked with it, when one was asked for.</summary>
public sealed record PrivateBookingDto(MyBookingDto Booking, MyBookingDto? Departure);

/// <summary>
/// A booking as its pilot reads it on their own page (design M4 §7.1, E6b): the event — its address, its title, what its dates
/// say now —, the flight with the aircraft chosen (<see cref="BookedFlight"/>: a public slot's own, a private one's with the flight its
/// pilot wrote, E7), and whether it may still be withdrawn: until the off block (§3.6). Past ones too: the page of the member keeps
/// them. A private arrival and its linked departure each name the other (<c>PairedBookingId</c>), while both are there.
/// </summary>
public sealed record MyBookingDto(
    long Id,
    long SlotId,
    long EventId,
    string EventSlug,
    Localized<string> EventTitle,
    EventStateKind EventState,
    SlotKind Kind,
    string Callsign,
    string? FlightNumber,
    string AircraftIcao,
    string DepartureIcao,
    DateTime OffBlockUtc,
    string ArrivalIcao,
    DateTime OnBlockUtc,
    bool IsArrival,
    string? Stand,
    string? Rotation,
    int? Leg,
    bool Withdrawable,
    DateTime CreatedAt,
    long? PairedBookingId);

/// <summary>A leg of a rotation the pilot did not get, and why, as the i18n key they read: taken, closed, not compatible…</summary>
public sealed record RotationLegRefusalDto(long SlotId, int? Leg, string Callsign, string Reason);

/// <summary>
/// What «book the whole rotation» did (§3.3, c3): the legs booked, in one transaction, and the ones that were not — already taken,
/// the pilot's already, closed, not compatible with their bookings, or not allowing the aircraft — each with its reason.
/// </summary>
public sealed record RotationBookingDto(IReadOnlyList<MyBookingDto> Booked, IReadOnlyList<RotationLegRefusalDto> NotBooked);

/// <summary>What a booking request says by itself: a slot, and an aircraft type written as the network writes one. Messages are i18n keys.</summary>
public sealed class BookingRequestValidator : AbstractValidator<BookingRequest>
{
    public BookingRequestValidator()
    {
        RuleFor(request => request.SlotId).GreaterThan(0).WithMessage("errors.required");

        RuleFor(request => request.AircraftIcao).NotEmpty().WithMessage("errors.required");
        RuleFor(request => request.AircraftIcao)
            .Must(aircraft => SlotValues.IsAircraftType(aircraft!.Trim().ToUpperInvariant()))
            .When(request => !string.IsNullOrWhiteSpace(request.AircraftIcao))
            .WithMessage("events:errors.aircraftFormat");
    }
}

/// <summary>
/// What a request for a private slot says by itself (§3.4): a slot, and a flight written as the network writes one — a callsign, an
/// aircraft type, an airport, a time —, the linked departure's under <c>departure.…</c>. Whether the airport and the type exist, whether
/// the times hold and whether the slots are free is the verb's. Messages are i18n keys.
/// <para>⚠️ Each rule of a format in a <c>RuleFor</c> of its own: a <c>When</c> at the end of a chain holds for the whole chain, and would
/// switch «required» off with it (E5).</para>
/// </summary>
public sealed class PrivateBookingRequestValidator : AbstractValidator<PrivateBookingRequest>
{
    /// <summary>Where the fields of the linked departure are refused, as the form names them: <c>departure.callsign</c>.</summary>
    public const string DeparturePrefix = "departure.";

    public PrivateBookingRequestValidator()
    {
        RuleFor(request => request.SlotId).GreaterThan(0).WithMessage("errors.required");

        RuleFor(request => request.AircraftIcao).NotEmpty().WithMessage("errors.required");
        RuleFor(request => request.AircraftIcao)
            .Must(aircraft => SlotValues.IsAircraftType(aircraft!.Trim().ToUpperInvariant()))
            .When(request => !string.IsNullOrWhiteSpace(request.AircraftIcao))
            .WithMessage("events:errors.aircraftFormat");

        Flight(request => request.Callsign, request => request.OtherIcao, request => request.OtherTimeUtc, prefix: string.Empty);

        When(request => request.Departure is not null, () =>
        {
            RuleFor(request => request.Departure!.SlotId)
                .GreaterThan(0).WithMessage("errors.required")
                .OverridePropertyName($"{DeparturePrefix}slotId");

            Flight(request => request.Departure!.Callsign, request => request.Departure!.OtherIcao, request => request.Departure!.OtherTimeUtc, DeparturePrefix);
        });
    }

    /// <summary>
    /// A flight through a private slot — its callsign, its other airport and the time there —, each refused under the field the form
    /// has: the booking's plain, the linked departure's after <see cref="DeparturePrefix"/>. Named by hand, both, so that one set of
    /// rules serves the two.
    /// </summary>
    private void Flight(
        Func<PrivateBookingRequest, string?> callsign,
        Func<PrivateBookingRequest, string?> otherIcao,
        Func<PrivateBookingRequest, DateTime?> otherTime,
        string prefix)
    {
        RuleFor(request => callsign(request))
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(EventSlot.MaxCodeLength).WithMessage("errors.text.tooLong")
            .OverridePropertyName($"{prefix}callsign");
        RuleFor(request => callsign(request))
            .Must(value => SlotValues.IsCallsign(value!.Trim().ToUpperInvariant()))
            .When(request => callsign(request) is { } value && !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= EventSlot.MaxCodeLength)
            .WithMessage("events:errors.callsignFormat")
            .OverridePropertyName($"{prefix}callsign");

        RuleFor(request => otherIcao(request)).NotEmpty().WithMessage("errors.required").OverridePropertyName($"{prefix}otherIcao");
        RuleFor(request => otherIcao(request))
            .Must(value => SlotValues.IsAirport(value!.Trim().ToUpperInvariant()))
            .When(request => !string.IsNullOrWhiteSpace(otherIcao(request)))
            .WithMessage("events:errors.airportUnknown")
            .OverridePropertyName($"{prefix}otherIcao");

        RuleFor(request => otherTime(request)).NotNull().WithMessage("errors.required").OverridePropertyName($"{prefix}otherTimeUtc");
    }
}

/// <summary>The reason of a booking taken away: required, and a few sentences at most.</summary>
public sealed class BookingRemovalRequestValidator : AbstractValidator<BookingRemovalRequest>
{
    public BookingRemovalRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().WithMessage("errors.required");
        RuleFor(request => request.Reason).MaximumLength(EventBooking.MaxNoteLength).WithMessage("errors.text.tooLong");
    }
}
