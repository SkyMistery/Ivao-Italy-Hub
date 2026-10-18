using FluentValidation;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Settings;

/// <summary>
/// What a kind of event switches on when the staff chooses it (design M4 §1.12, note 2026-09-29-i-tipi-di-evento): the five
/// switches of the event, preset and never imposed — the staff changes them on the event. The kind is a key of the calendar's
/// vocabulary, which the division writes; the code knows no kind.
/// </summary>
/// <param name="Kind">A key of the kinds of the calendar (<c>cms_calendar_kinds</c>).</param>
/// <param name="PublicSlots">Public slots, prepared by the staff: an RFE, an RFO.</param>
/// <param name="PrivateSlots">Private slots, generated from the capacity: an RFO, an MSE.</param>
/// <param name="HasRoster">An ATC roster.</param>
/// <param name="WholeDivision">Every airport and position of the division, as an online day.</param>
/// <param name="InPerson">An event people come to.</param>
public sealed record KindPreset(
    string Kind,
    bool PublicSlots,
    bool PrivateSlots,
    bool HasRoster,
    bool WholeDivision,
    bool InPerson);

/// <summary>
/// What the events department changes from the interface without a release (design M4 §1.12, note
/// 2026-09-29-le-impostazioni-degli-eventi). The skeleton (E2) has the settings of M4a; every other setting is born with the
/// phase that uses it, and starts at its default on an installation that saved these long ago.
/// <para>None of them knows the division (the test of the fork XX): no kind is preset until the division writes its own, and
/// the numbers are the design's.</para>
/// </summary>
public sealed record EventsSettings
{
    /// <summary>The switches each kind presets on a new event; none by default.</summary>
    public IReadOnlyList<KindPreset> KindPresets { get; init; } = [];

    /// <summary>The least minutes between two bookings of the same pilot in one event, one way or the other (§3.5).</summary>
    public int BookingGapMinutes { get; init; } = 10;

    /// <summary>Months after an event before the bookings and the reports of its pilots go (§11).</summary>
    public int PilotRetentionMonths { get; init; } = 24;

    /// <summary>Months after an event in person before its registrations and the seats of its activities go; the sums stay (§4-bis, §11).</summary>
    public int InPersonRetentionMonths { get; init; } = 3;

    /// <summary>Hours before the off block time of a booking the pilot's reminder leaves (§3.8).</summary>
    public int ReminderLeadHours { get; init; } = 24;

    /// <summary>The length of a shift of the roster when the event does not say its own (§4.3, E11a).</summary>
    public int ShiftMinutes { get; init; } = 60;

    /// <summary>
    /// The least ATC rating a member gives their availability with (§4.2, E11a): the network's number of a rating of the core's vocabulary,
    /// chosen from it, never written in the code. None by default: any member — the division writes its own.
    /// </summary>
    public int? MinimumAtcRating { get; init; }

    /// <summary>The most shifts in a row a controller is proposed for (§4.3, read by E11b).</summary>
    public int MaxConsecutiveShifts { get; init; } = 2;

    /// <summary>The shifts of break after that many in a row (§4.3, read by E11b).</summary>
    public int BreakShifts { get; init; } = 1;

    /// <summary>Days before the start of an event its availabilities close, and the roster is proposed (§4.2, §4.3).</summary>
    public int ApplicationsCloseDays { get; init; } = 3;

    /// <summary>Days before the start of an event its roster is published (§4.4, read by E12): the staff correct it in between.</summary>
    public int RosterPublishDays { get; init; } = 2;

    /// <summary>How many months back the positions a controller opened count as experience (§4.3, read by E11b).</summary>
    public int ExperienceMonths { get; init; } = 12;

    /// <summary>
    /// The minutes a controller must be on the position within a shift to have attended it (§4.5, E13a); a shift shorter than that asks its
    /// whole length.
    /// </summary>
    public int AttendanceMinimumMinutes { get; init; } = 30;

    /// <summary>The weight of the penalty of reliability (§4.5, E13b): <c>weight × no-shows ÷ (attended + no-shows + 2)</c>.</summary>
    public double NoShowWeight { get; init; } = 1;

    /// <summary>The bookings not flown from which a pilot has the limits (§3.7, E13b).</summary>
    public int UnflownThreshold { get; init; } = 3;

    /// <summary>For whoever has the limits: the hours of each band of an event, from its start (§3.7, E13b).</summary>
    public int RestrictedWindowHours { get; init; } = 2;

    /// <summary>For whoever has the limits: the most bookings in each band.</summary>
    public int RestrictedMaxPerWindow { get; init; } = 1;

    /// <summary>For whoever has the limits: the most bookings in the whole event.</summary>
    public int RestrictedMaxPerEvent { get; init; } = 2;
}

/// <summary>
/// The rules of the values themselves. Messages are i18n keys. A preset is refused on the field of its row,
/// <c>kindPresets[2].kind</c>, and not on the list: the form draws each row with its own fields and has nowhere to show an error
/// about the list as a whole.
/// </summary>
public sealed class EventsSettingsValidator : AbstractValidator<EventsSettings>
{
    /// <summary>A day: two bookings of one pilot further apart than that are not a question of this rule.</summary>
    public const int MaxBookingGapMinutes = 24 * 60;

    /// <summary>Ten years: a period longer than that keeps what the division no longer needs.</summary>
    public const int MaxPilotRetentionMonths = 120;

    /// <summary>A week, as the reminder of a training.</summary>
    public const int MaxReminderLeadHours = 7 * 24;

    /// <summary>A quarter of an hour: shorter, a controller would hand over the frequency more often than they would control it.</summary>
    public const int MinShiftMinutes = 15;

    /// <summary>Twelve hours: a shift longer than that is the whole event, not a shift.</summary>
    public const int MaxShiftMinutes = 12 * 60;

    /// <summary>A day of shifts in a row, at the shortest shift: more is no limit at all.</summary>
    public const int MaxShiftsInARow = 24 * 60 / MinShiftMinutes;

    /// <summary>Two months: an event whose availabilities close earlier than that has not been announced yet.</summary>
    public const int MaxDaysBefore = 60;

    /// <summary>Five years of a controller's positions: the archive of the sessions keeps no more than that worth reading.</summary>
    public const int MaxExperienceMonths = 60;

    /// <summary>Ten times the design's weight: heavier, one no-show would decide every roster by itself.</summary>
    public const double MaxNoShowWeight = 10;

    /// <summary>A hundred bookings not flown: a threshold higher than that limits nobody.</summary>
    public const int MaxUnflownThreshold = 100;

    /// <summary>Two days: a band longer than that is the whole event.</summary>
    public const int MaxRestrictedWindowHours = 48;

    /// <summary>Fifty bookings in a band: more is no limit at all.</summary>
    public const int MaxRestrictedPerWindow = 50;

    /// <summary>Two hundred bookings in an event: more is no limit at all.</summary>
    public const int MaxRestrictedPerEvent = 200;

    public EventsSettingsValidator()
    {
        RuleFor(settings => settings.KindPresets).Custom((presets, context) =>
        {
            if (presets is null)
            {
                context.AddFailure("kindPresets", "errors.required");
                return;
            }

            for (var index = 0; index < presets.Count; index++)
            {
                var kind = presets[index]?.Kind;

                if (string.IsNullOrWhiteSpace(kind))
                {
                    context.AddFailure($"kindPresets[{index}].kind", "errors.required");
                }
                else if (presets.Count(other => string.Equals(other?.Kind, kind, StringComparison.Ordinal)) > 1)
                {
                    context.AddFailure($"kindPresets[{index}].kind", "events:errors.kindTwice");
                }
            }
        });

        RuleFor(settings => settings.BookingGapMinutes).InclusiveBetween(0, MaxBookingGapMinutes).WithMessage("errors.number.range");
        RuleFor(settings => settings.PilotRetentionMonths).InclusiveBetween(1, MaxPilotRetentionMonths).WithMessage("errors.number.range");
        RuleFor(settings => settings.InPersonRetentionMonths).InclusiveBetween(1, MaxPilotRetentionMonths).WithMessage("errors.number.range");
        RuleFor(settings => settings.ReminderLeadHours).InclusiveBetween(1, MaxReminderLeadHours).WithMessage("errors.number.range");

        // The ATC (E11a): the numbers of the roster, read by E11b and E12, ranged here once.
        RuleFor(settings => settings.ShiftMinutes).InclusiveBetween(MinShiftMinutes, MaxShiftMinutes).WithMessage("errors.number.range");
        RuleFor(settings => settings.MaxConsecutiveShifts).InclusiveBetween(1, MaxShiftsInARow).WithMessage("errors.number.range");
        RuleFor(settings => settings.BreakShifts).InclusiveBetween(0, MaxShiftsInARow).WithMessage("errors.number.range");
        RuleFor(settings => settings.ApplicationsCloseDays).InclusiveBetween(0, MaxDaysBefore).WithMessage("errors.number.range");
        RuleFor(settings => settings.RosterPublishDays).InclusiveBetween(0, MaxDaysBefore).WithMessage("errors.number.range");
        RuleFor(settings => settings.ExperienceMonths).InclusiveBetween(1, MaxExperienceMonths).WithMessage("errors.number.range");

        // After the event (E13a): at least a minute on the position, and never more than the longest shift there is.
        RuleFor(settings => settings.AttendanceMinimumMinutes).InclusiveBetween(1, MaxShiftMinutes).WithMessage("errors.number.range");

        // The registers and the limits (E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, reading 9): a threshold of nought would limit
        // everybody, and a limit of nought would close the bookings to whoever has one.
        RuleFor(settings => settings.NoShowWeight).InclusiveBetween(0, MaxNoShowWeight).WithMessage("errors.number.range");
        RuleFor(settings => settings.UnflownThreshold).InclusiveBetween(1, MaxUnflownThreshold).WithMessage("errors.number.range");
        RuleFor(settings => settings.RestrictedWindowHours).InclusiveBetween(1, MaxRestrictedWindowHours).WithMessage("errors.number.range");
        RuleFor(settings => settings.RestrictedMaxPerWindow).InclusiveBetween(1, MaxRestrictedPerWindow).WithMessage("errors.number.range");
        RuleFor(settings => settings.RestrictedMaxPerEvent).InclusiveBetween(1, MaxRestrictedPerEvent).WithMessage("errors.number.range");

        // The roster is published after the availabilities close, never before: in between the staff correct it (§4.4, c2).
        RuleFor(settings => settings.RosterPublishDays)
            .LessThanOrEqualTo(settings => settings.ApplicationsCloseDays)
            .When(settings => settings.ApplicationsCloseDays is >= 0 and <= MaxDaysBefore)
            .WithMessage("events:errors.rosterBeforeClose");
    }
}

/// <summary>
/// The rules of a save: those of the values, and the two that read what the hub knows — a preset is for a kind the calendar has, and
/// the least ATC rating is one of the core's vocabulary (E11a). A kind the division took off its calendar since is refused on its row,
/// so that the department sees it and takes it out, rather than finding it gone.
/// </summary>
public sealed class EventsSettingsSaveValidator : AbstractValidator<EventsSettings>
{
    public EventsSettingsSaveValidator(HubDbContext hub, RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(vocabulary);

        Include(new EventsSettingsValidator());

        RuleFor(settings => settings.MinimumAtcRating)
            .Must(rating => vocabulary.Find(RatingKind.Atc, rating) is not null)
            .When(settings => settings.MinimumAtcRating is not null)
            .WithMessage("events:errors.ratingUnknown");

        RuleFor(settings => settings.KindPresets).CustomAsync(async (presets, context, cancellationToken) =>
        {
            var keys = presets?.Select(preset => preset?.Kind).OfType<string>().Where(key => key.Length > 0).Distinct().ToList() ?? [];
            if (keys.Count == 0)
            {
                return;
            }

            var known = await hub.CalendarKinds.AsNoTracking()
                .Where(kind => keys.Contains(kind.Key))
                .Select(kind => kind.Key)
                .ToListAsync(cancellationToken);

            for (var index = 0; index < presets!.Count; index++)
            {
                if (presets[index]?.Kind is { Length: > 0 } kind && !known.Contains(kind, StringComparer.Ordinal))
                {
                    context.AddFailure($"kindPresets[{index}].kind", "events:errors.calendarKindUnknown");
                }
            }
        });
    }
}
