using FluentValidation.Results;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Settings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of the events' skeleton (M4, E2) that need no database: the defaults of the settings and their rules, as design
/// M4 §1.12 and the note 2026-09-29-le-impostazioni-degli-eventi write them, and the catalogue of the permissions (§6.1). Whether
/// a preset's kind is one of the calendar is the save's rule, which reads the calendar: the integration tests hold it.
/// </summary>
public sealed class EventsSettingsTests
{
    private static readonly EventsSettingsValidator Validator = new();

    [Fact]
    public void TheDefaultsAreTheDesignsAndKnowNoDivision()
    {
        var defaults = new EventsSettings();

        // No kind is preset until the division writes its own: a kind is a word of its calendar.
        Assert.Empty(defaults.KindPresets);
        Assert.Equal(10, defaults.BookingGapMinutes);
        Assert.Equal(24, defaults.PilotRetentionMonths);
        Assert.Equal(24, defaults.ReminderLeadHours);

        Assert.True(Validator.Validate(defaults).IsValid);
    }

    [Fact]
    public void APresetIsForAKindAndForOneKindOnce()
    {
        Assert.True(Validator.Validate(With(Preset("first"), Preset("second"))).IsValid);

        // The same kind twice: both rows say so, on their own field.
        Assert.Equal(
            [("kindPresets[0].kind", "events:errors.kindTwice"), ("kindPresets[2].kind", "events:errors.kindTwice")],
            Failures(Validator.Validate(With(Preset("first"), Preset("second"), Preset("first")))));

        // A row with no kind is a row nobody finished.
        Assert.Equal(
            [("kindPresets[1].kind", "errors.required")],
            Failures(Validator.Validate(With(Preset("first"), Preset(" ")))));

        Assert.Equal(
            [("kindPresets", "errors.required")],
            Failures(Validator.Validate(new EventsSettings { KindPresets = null! })));
    }

    [Fact]
    public void TheGapTheRetentionAndTheLeadHaveTheirRanges()
    {
        Assert.True(Validator.Validate(new EventsSettings { BookingGapMinutes = 0, PilotRetentionMonths = 1, ReminderLeadHours = 1 }).IsValid);
        Assert.True(Validator.Validate(new EventsSettings
        {
            BookingGapMinutes = EventsSettingsValidator.MaxBookingGapMinutes,
            PilotRetentionMonths = EventsSettingsValidator.MaxPilotRetentionMonths,
            ReminderLeadHours = EventsSettingsValidator.MaxReminderLeadHours,
        }).IsValid);

        Assert.Equal(
            [
                ("bookingGapMinutes", "errors.number.range"),
                ("pilotRetentionMonths", "errors.number.range"),
                ("reminderLeadHours", "errors.number.range"),
            ],
            Failures(Validator.Validate(new EventsSettings { BookingGapMinutes = -1, PilotRetentionMonths = 0, ReminderLeadHours = 0 })));

        Assert.Equal(
            [
                ("bookingGapMinutes", "errors.number.range"),
                ("pilotRetentionMonths", "errors.number.range"),
                ("reminderLeadHours", "errors.number.range"),
            ],
            Failures(Validator.Validate(new EventsSettings
            {
                BookingGapMinutes = EventsSettingsValidator.MaxBookingGapMinutes + 1,
                PilotRetentionMonths = EventsSettingsValidator.MaxPilotRetentionMonths + 1,
                ReminderLeadHours = EventsSettingsValidator.MaxReminderLeadHours + 1,
            })));
    }

    [Fact]
    public void TheSettingsOfTheAtcAreTheDesignsAndHaveTheirRanges()
    {
        // Design §1.12: an hour a shift, nobody refused for a rating until the division chooses one, two shifts in a row and one of
        // break, availabilities closing three days before an event and the roster published two days before, a year of experience.
        var defaults = new EventsSettings();
        Assert.Equal(
            (60, (int?)null, 2, 1, 3, 2, 12),
            (defaults.ShiftMinutes, defaults.MinimumAtcRating, defaults.MaxConsecutiveShifts, defaults.BreakShifts, defaults.ApplicationsCloseDays,
                defaults.RosterPublishDays, defaults.ExperienceMonths));

        Assert.True(Validator.Validate(new EventsSettings
        {
            ShiftMinutes = EventsSettingsValidator.MinShiftMinutes,
            MaxConsecutiveShifts = 1,
            BreakShifts = 0,
            ApplicationsCloseDays = 0,
            RosterPublishDays = 0,
            ExperienceMonths = 1,
        }).IsValid);

        Assert.Equal(
            [
                ("shiftMinutes", "errors.number.range"),
                ("maxConsecutiveShifts", "errors.number.range"),
                ("breakShifts", "errors.number.range"),
                ("applicationsCloseDays", "errors.number.range"),
                ("rosterPublishDays", "errors.number.range"),
                ("experienceMonths", "errors.number.range"),
            ],
            Failures(Validator.Validate(new EventsSettings
            {
                ShiftMinutes = EventsSettingsValidator.MaxShiftMinutes + 1,
                MaxConsecutiveShifts = 0,
                BreakShifts = -1,
                ApplicationsCloseDays = EventsSettingsValidator.MaxDaysBefore + 1,
                RosterPublishDays = -1,
                ExperienceMonths = EventsSettingsValidator.MaxExperienceMonths + 1,
            })));

        // The roster is published after the availabilities close, so the staff can correct it in between (§4.4): never earlier.
        Assert.True(Validator.Validate(new EventsSettings { ApplicationsCloseDays = 2, RosterPublishDays = 2 }).IsValid);
        Assert.Equal(
            [("rosterPublishDays", "events:errors.rosterBeforeClose")],
            Failures(Validator.Validate(new EventsSettings { ApplicationsCloseDays = 2, RosterPublishDays = 3 })));
    }

    [Fact]
    public void FiveAreasAndTwoPermissionsDeniedToWhoeverARowIsAbout()
    {
        Assert.Equal(12, EventsPermissions.All.DistinctBy(permission => permission.Name).Count());

        string[] areas = [EventsPermissions.Area, EventsPermissions.RoutesArea, EventsPermissions.BookingsArea, EventsPermissions.AtcArea, EventsPermissions.ReportsArea];
        Assert.All(EventsPermissions.All, permission =>
        {
            Assert.Contains(permission.Name[..permission.Name.IndexOf('.', StringComparison.Ordinal)], areas);
            Assert.False(permission.IsGlobal);
            Assert.False(permission.OnlyForAssignee);
        });

        // Every area has what reads it and what writes it: the guard asks {Area}.Edit of every row of the staff (design §6.1).
        Assert.All(areas, area =>
        {
            Assert.Contains(EventsPermissions.All, permission => permission.Name == $"{area}.View");
            Assert.Contains(EventsPermissions.All, permission => permission.Name == $"{area}.Edit");
        });

        Assert.Equal(
            new[] { EventsPermissions.AtcEdit, EventsPermissions.ReportsEdit }.Order(StringComparer.Ordinal),
            EventsPermissions.All.Where(permission => permission.DeniedToStakeholder).Select(permission => permission.Name).Order(StringComparer.Ordinal));
    }

    private static EventsSettings With(params KindPreset[] presets) => new() { KindPresets = presets };

    private static KindPreset Preset(string kind) => new(kind, PublicSlots: true, PrivateSlots: false, HasRoster: false, WholeDivision: false, InPerson: false);

    /// <summary>The failures as the API sends them: the field as the form spells it, and the key.</summary>
    private static (string Field, string Key)[] Failures(ValidationResult result) =>
        [.. result.Errors.Select(failure => (CrudProblems.FieldName(failure.PropertyName), failure.ErrorMessage))];
}
