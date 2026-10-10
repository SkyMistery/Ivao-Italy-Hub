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
/// A booking as its pilot reads it on their own page (design M4 §7.1, E6b): the event — its address, its title, what its dates
/// say now —, the flight of the slot with the aircraft chosen, and whether it may still be withdrawn: until the off block (§3.6).
/// Past ones too: the page of the member keeps them.
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
    DateTime CreatedAt);

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

/// <summary>The reason of a booking taken away: required, and a few sentences at most.</summary>
public sealed class BookingRemovalRequestValidator : AbstractValidator<BookingRemovalRequest>
{
    public BookingRemovalRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().WithMessage("errors.required");
        RuleFor(request => request.Reason).MaximumLength(EventBooking.MaxNoteLength).WithMessage("errors.text.tooLong");
    }
}
