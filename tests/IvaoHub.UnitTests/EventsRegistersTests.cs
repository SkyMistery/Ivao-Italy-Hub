using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Registers;
using IvaoHub.Modules.Events.Settings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the registers (M4, E13b; design §3.7, §4.5; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 1, 3, 7, 8 and 9): the
/// penalty with its weight, the outcomes a shift may still pass to, the bands of the limits and the event's own numbers, until when a pilot has
/// the limits, and the ranges of the five settings.
/// </summary>
public sealed class EventsRegistersTests
{
    private static readonly DateTime Start = new(2001, 1, 1, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ANoShowInTenShiftsWeighsEightHundredthsAndInTwoAQuarter()
    {
        // The design's two figures (§4.5): one no-show and nine attended, one and one.
        Assert.Equal(0.08, Math.Round(RosterProposer.Penalty(noShows: 1, attended: 9), 2));
        Assert.Equal(0.25, RosterProposer.Penalty(noShows: 1, attended: 1));

        // The «+ 2»: the first no-show of a new controller is not the most there is.
        Assert.Equal(1.0 / 3, RosterProposer.Penalty(noShows: 1, attended: 0), precision: 10);

        // The weight of the settings multiplies it, and nought weighs nothing.
        Assert.Equal(0.5, RosterProposer.Penalty(noShows: 1, attended: 1, weight: 2));
        Assert.Equal(0, RosterProposer.Penalty(noShows: 3, attended: 1, weight: 0));
        Assert.Equal(1, new EventsSettings().NoShowWeight);
    }

    [Fact]
    public void AShiftGoesToAnyOfTheStaffsThreeOutcomesButTheOneItHasNoneIsFinal()
    {
        ShiftAttendance[] staffs = [ShiftAttendance.Attended, ShiftAttendance.NoShow, ShiftAttendance.Excused];

        // Carmine's answer on #283, reading 1: whatever a shift is, it becomes another of the three — a decided one too.
        foreach (var from in Enum.GetValues<ShiftAttendance>())
        {
            foreach (var to in Enum.GetValues<ShiftAttendance>())
            {
                Assert.Equal(staffs.Contains(to) && to != from, RegisterDecisions.Allowed(from, to));
            }
        }

        // The no-show taken away is one of those changes; and a mistaken «attended» is corrected.
        Assert.True(RegisterDecisions.Allowed(ShiftAttendance.NoShow, ShiftAttendance.Excused));
        Assert.True(RegisterDecisions.Allowed(ShiftAttendance.Attended, ShiftAttendance.NoShow));

        // A shift of an event over and not cancelled may be decided; one still to end, or cancelled, not.
        Assert.True(RegisterDecisions.Decidable(new Event { EndsAtUtc = Start }, Start));
        Assert.False(RegisterDecisions.Decidable(new Event { EndsAtUtc = Start.AddMinutes(1) }, Start));
        Assert.False(RegisterDecisions.Decidable(new Event { EndsAtUtc = Start, CancelledAt = Start.AddDays(-1) }, Start));
    }

    [Fact]
    public void TheBandsAreFixedFromTheStartOfTheEvent()
    {
        Assert.Equal(0, BookingLimits.Band(Start, Start, 2));
        Assert.Equal(0, BookingLimits.Band(Start, Start.AddMinutes(119), 2));
        Assert.Equal(1, BookingLimits.Band(Start, Start.AddHours(2), 2));
        // A slot in the six hours before the start (E5's margin) is a band of its own, before the first.
        Assert.Equal(-1, BookingLimits.Band(Start, Start.AddMinutes(-1), 2));
        Assert.Equal(-3, BookingLimits.Band(Start, Start.AddHours(-6), 2));
    }

    [Fact]
    public void AtMostKInABandAndMInTheEvent()
    {
        var limits = new PilotLimits(WindowHours: 2, MaxPerWindow: 1, MaxPerEvent: 2);

        Assert.Null(BookingLimits.Refusal(limits, Start, [], Start.AddMinutes(10)));
        Assert.Equal(BookingLimits.WindowKey, BookingLimits.Refusal(limits, Start, [Start.AddMinutes(10)], Start.AddMinutes(90)));
        Assert.Null(BookingLimits.Refusal(limits, Start, [Start.AddMinutes(10)], Start.AddHours(3)));
        Assert.Equal(BookingLimits.EventKey, BookingLimits.Refusal(limits, Start, [Start.AddMinutes(10), Start.AddHours(3)], Start.AddHours(5)));

        // The event's own numbers replace the settings' one by one; empty, the settings'.
        var settings = new EventsSettings();
        Assert.Equal(new PilotLimits(2, 1, 2), BookingLimits.Of(new Event(), settings));
        Assert.Equal(new PilotLimits(3, 1, 4), BookingLimits.Of(new Event { RestrictedWindowHours = 3, RestrictedMaxPerEvent = 4 }, settings));
    }

    [Fact]
    public void TheTimeOfASlotIsTheOneAtTheAirportOfTheEvent()
    {
        var departure = new EventSlot { IsArrival = false, OffBlockUtc = Start, OnBlockUtc = Start.AddHours(1) };
        var arrival = new EventSlot { IsArrival = true, OffBlockUtc = Start, OnBlockUtc = Start.AddHours(1) };
        var privateArrival = new EventSlot { IsArrival = true, OnBlockUtc = Start.AddHours(2) };

        Assert.Equal(Start, BookingLimits.AtTheEventAirport(departure));
        Assert.Equal(Start.AddHours(1), BookingLimits.AtTheEventAirport(arrival));
        Assert.Equal(Start.AddHours(2), BookingLimits.AtTheEventAirport(privateArrival));
    }

    [Fact]
    public void ThePilotHasTheLimitsUntilTheCountedFallBelowTheThreshold()
    {
        DateTime[] leaving = [Start.AddMonths(5), Start.AddMonths(1), Start.AddMonths(3), Start.AddMonths(4)];

        // Four counted, a threshold of three: once two have left, two remain — the second to leave, the third month.
        Assert.Equal(Start.AddMonths(3), BookingLimits.Until(leaving, threshold: 3));
        Assert.Equal(Start.AddMonths(1), BookingLimits.Until(leaving, threshold: 4));
        Assert.Equal(Start.AddMonths(5), BookingLimits.Until(leaving, threshold: 1));

        // Below the threshold: no limits.
        Assert.Null(BookingLimits.Until(leaving, threshold: 5));
        Assert.Null(BookingLimits.Until([], threshold: 3));
    }

    [Fact]
    public void TheFiveSettingsAreTheDesignsAndHaveTheirRanges()
    {
        var validator = new EventsSettingsValidator();
        var defaults = new EventsSettings();

        Assert.Equal((1d, 3, 2, 1, 2), (defaults.NoShowWeight, defaults.UnflownThreshold, defaults.RestrictedWindowHours, defaults.RestrictedMaxPerWindow, defaults.RestrictedMaxPerEvent));
        Assert.True(validator.Validate(defaults).IsValid);

        Assert.True(validator.Validate(new EventsSettings
        {
            NoShowWeight = EventsSettingsValidator.MaxNoShowWeight,
            UnflownThreshold = EventsSettingsValidator.MaxUnflownThreshold,
            RestrictedWindowHours = EventsSettingsValidator.MaxRestrictedWindowHours,
            RestrictedMaxPerWindow = EventsSettingsValidator.MaxRestrictedPerWindow,
            RestrictedMaxPerEvent = EventsSettingsValidator.MaxRestrictedPerEvent,
        }).IsValid);

        // A threshold of nought would limit everybody, a limit of nought close the bookings.
        var refused = validator.Validate(new EventsSettings
        {
            NoShowWeight = -0.5,
            UnflownThreshold = 0,
            RestrictedWindowHours = 0,
            RestrictedMaxPerWindow = 0,
            RestrictedMaxPerEvent = EventsSettingsValidator.MaxRestrictedPerEvent + 1,
        });
        Assert.Equal(
            ["NoShowWeight", "RestrictedMaxPerEvent", "RestrictedMaxPerWindow", "RestrictedWindowHours", "UnflownThreshold"],
            refused.Errors.Select(failure => failure.PropertyName).Order(StringComparer.Ordinal));
        Assert.All(refused.Errors, failure => Assert.Equal("errors.number.range", failure.ErrorMessage));
    }
}
