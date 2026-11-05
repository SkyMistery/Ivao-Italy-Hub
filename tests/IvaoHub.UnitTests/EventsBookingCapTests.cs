using FluentValidation.Results;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Settings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The most bookings a member makes in one event (issue #314; note 2026-11-01-il-tetto-delle-prenotazioni), with no database: the setting
/// of the division, five by default and from one to two hundred, and the count — a rotation is one booking, whatever its legs, any other slot
/// one each. The verbs that read it under the pilot's lock are the integration tests'.
/// </summary>
public sealed class EventsBookingCapTests
{
    private static readonly EventsSettingsValidator Validator = new();

    [Fact]
    public void TheMostOfTheDivisionIsFiveAndHasItsRange()
    {
        Assert.Equal(5, new EventsSettings().MaxBookingsPerEvent);

        Assert.True(Validator.Validate(new EventsSettings { MaxBookingsPerEvent = 1 }).IsValid);
        Assert.True(Validator.Validate(new EventsSettings { MaxBookingsPerEvent = EventsSettingsValidator.MaxBookingsCeiling }).IsValid);

        // Nought would close the bookings to everybody.
        Assert.Equal([("maxBookingsPerEvent", "errors.number.range")], Failures(Validator.Validate(new EventsSettings { MaxBookingsPerEvent = 0 })));
        Assert.Equal(
            [("maxBookingsPerEvent", "errors.number.range")],
            Failures(Validator.Validate(new EventsSettings { MaxBookingsPerEvent = EventsSettingsValidator.MaxBookingsCeiling + 1 })));
    }

    [Fact]
    public void ARotationIsOneBookingWhateverItsLegs()
    {
        // Two legs of R1, three of R2, one of r1 — another rotation, case and all —, a slot alone, a private arrival and its departure.
        Assert.Equal(6, BookingCap.Count(
        [
            Leg("R1"), Leg("R1"),
            Leg("R2"), Leg("R2"), Leg("R2"),
            Leg("r1"),
            Single(),
            Private(), Private(),
        ]));

        Assert.Equal(0, BookingCap.Count([]));
        Assert.Equal(1, BookingCap.Count([Leg("R1"), Leg("R1"), Leg("R1")]));
        Assert.Equal(3, BookingCap.Count([Single(), Single(), Private()]));
    }

    [Fact]
    public void OnlyWhatGrowsTheCountPastTheMostIsRefused()
    {
        var four = new[] { Single(), Single(), Leg("R1"), Leg("R1"), Private() };

        // Four bookings (five slots): the fifth goes, at the boundary; a leg of R1, which the pilot holds, does not count.
        Assert.Null(BookingCap.Refusal(5, four, Single()));
        Assert.Null(BookingCap.Refusal(5, four, Leg("R2")));
        Assert.Null(BookingCap.Refusal(4, four, Leg("R1")));

        // Five bookings: a sixth is refused, a slot alone or a new rotation alike, a private slot too.
        EventSlot[] five = [.. four, Leg("R2")];
        Assert.Equal(BookingCap.Key, BookingCap.Refusal(5, five, Single()));
        Assert.Equal(BookingCap.Key, BookingCap.Refusal(5, five, Leg("R3")));
        Assert.Equal(BookingCap.Key, BookingCap.Refusal(5, five, Private()));
        Assert.Null(BookingCap.Refusal(5, five, Leg("R2")));

        // A cap lowered since: the pilot keeps what they booked, and a leg of a rotation of theirs still goes.
        Assert.Null(BookingCap.Refusal(2, five, Leg("R1")));
        Assert.Equal(BookingCap.Key, BookingCap.Refusal(2, five, Single()));
    }

    /// <summary>A public slot of a rotation, by its code.</summary>
    private static EventSlot Leg(string rotation) => new() { Kind = SlotKind.Public, RotationCode = rotation, RotationLeg = 1 };

    /// <summary>A public slot of no rotation.</summary>
    private static EventSlot Single() => new() { Kind = SlotKind.Public };

    /// <summary>A private slot: one booking each, an arrival's linked departure too.</summary>
    private static EventSlot Private() => new() { Kind = SlotKind.Private };

    private static (string Field, string Key)[] Failures(ValidationResult result) =>
        [.. result.Errors.Select(failure => (CrudProblems.FieldName(failure.PropertyName), failure.ErrorMessage))];
}
