using FluentValidation;
using IvaoHub.Core.Data;
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

    /// <summary>Hours before the off block time of a booking the pilot's reminder leaves (§3.8).</summary>
    public int ReminderLeadHours { get; init; } = 24;
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
        RuleFor(settings => settings.ReminderLeadHours).InclusiveBetween(1, MaxReminderLeadHours).WithMessage("errors.number.range");
    }
}

/// <summary>
/// The rules of a save: those of the values, and the one that reads what the hub knows — a preset is for a kind the calendar has.
/// A kind the division took off its calendar since is refused on its row, so that the department sees it and takes it out,
/// rather than finding it gone.
/// </summary>
public sealed class EventsSettingsSaveValidator : AbstractValidator<EventsSettings>
{
    public EventsSettingsSaveValidator(HubDbContext hub)
    {
        ArgumentNullException.ThrowIfNull(hub);

        Include(new EventsSettingsValidator());

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
