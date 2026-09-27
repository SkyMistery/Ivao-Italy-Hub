using FluentValidation;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Reference;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training.Settings;

/// <summary>What happens when a trainer proposes a date that meets something in the calendar (design M3 §2.5).</summary>
public enum ConflictPolicy
{
    /// <summary>The trainer is shown what it meets, and confirms.</summary>
    Warn,

    /// <summary>The date is refused.</summary>
    Block,

    /// <summary>Nobody looks.</summary>
    None,
}

/// <summary>The hours of connection a member needs on a ladder to ask for one of its ratings (design M3 §2.2).</summary>
/// <param name="Kind">The ladder: the hours counted are the member's on it.</param>
/// <param name="Rating">The rating asked for, by the number the hub keeps: one the vocabulary of the core trains for.</param>
/// <param name="Hours">The least whole hours.</param>
public sealed record HoursThreshold(RatingKind Kind, int Rating, int Hours);

/// <summary>
/// What the training department changes from the interface without a release (design M3 §1.6). The values below are the
/// design's; they are the defaults of an installation that never saved, and of a setting added after it did.
/// <para>None of them knows the division (the test of the fork XX): no threshold of hours, no time limit for choosing a date,
/// no position left out and no site of the exam until the department writes its own.</para>
/// </summary>
public sealed record TrainingSettings
{
    /// <summary>The hours a request needs, per ladder and rating; none by default.</summary>
    public IReadOnlyList<HoursThreshold> MinimumHours { get; init; } = [];

    /// <summary>Days after a training before a training on the same ladder may be asked for.</summary>
    public int CooldownDays { get; init; } = 5;

    /// <summary>The same, after a session the trainee did not come to.</summary>
    public int NoShowCooldownDays { get; init; } = 14;

    /// <summary>Days a trainee has to choose a date before the training closes by itself; null, never (§12 n.9).</summary>
    public int? MaxResponseDays { get; init; }

    /// <summary>Days without a choice before the trainer's queue shows the training as waiting.</summary>
    public int ResponseReminderDays { get; init; } = 3;

    public ConflictPolicy ConflictPolicy { get; init; } = ConflictPolicy.Warn;

    /// <summary>The kinds of the calendar whose entries a date is checked against; the online day joins when M4 makes its kind.</summary>
    public IReadOnlyList<string> ConflictKinds { get; init; } = ["event"];

    /// <summary>Hours before a session the reminder leaves.</summary>
    public int ReminderLeadHours { get; init; } = 24;

    /// <summary>
    /// The callsigns of the positions the department does not train on. The core offers every position of the division a
    /// rating is trained on, the military ones included, and the department leaves out the ones it does not use (A2).
    /// </summary>
    public IReadOnlyList<string> HiddenPositions { get; init; } = [];

    /// <summary>Where the theory exam is taken: in the question a trainee answers and in the reminder of whoever approves (§12 n.12).</summary>
    public string? TheoryExamUrl { get; init; }
}

/// <summary>
/// The rules of the values themselves, against the vocabulary of ratings the host registers (and a test builds its own).
/// Messages are i18n keys.
/// <para>A threshold is refused on the field of its row, <c>minimumHours[2].rating</c>, and not on the list: the form draws
/// each row with its own fields and has nowhere to show an error about the list as a whole.</para>
/// </summary>
public sealed class TrainingSettingsValidator : AbstractValidator<TrainingSettings>
{
    /// <summary>More hours than anybody asks for a rating: a threshold above it is a typing mistake.</summary>
    public const int MaxHours = 10_000;

    public TrainingSettingsValidator(RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        RuleFor(settings => settings.MinimumHours).Custom((thresholds, context) =>
        {
            if (thresholds is null)
            {
                context.AddFailure("minimumHours", "errors.required");
                return;
            }

            for (var index = 0; index < thresholds.Count; index++)
            {
                var threshold = thresholds[index];

                if (vocabulary.Find(threshold.Kind, threshold.Rating) is not { HasPracticalTraining: true })
                {
                    context.AddFailure($"minimumHours[{index}].rating", "training:errors.ratingNotTrained");
                }
                else if (thresholds.Count(other => other.Kind == threshold.Kind && other.Rating == threshold.Rating) > 1)
                {
                    context.AddFailure($"minimumHours[{index}].rating", "training:errors.ratingTwice");
                }

                if (threshold.Hours is < 1 or > MaxHours)
                {
                    context.AddFailure($"minimumHours[{index}].hours", "errors.number.range");
                }
            }
        });

        RuleFor(settings => settings.CooldownDays).InclusiveBetween(0, 365).WithMessage("errors.number.range");
        RuleFor(settings => settings.NoShowCooldownDays).InclusiveBetween(0, 365).WithMessage("errors.number.range");
        RuleFor(settings => settings.MaxResponseDays).InclusiveBetween(1, 365).When(settings => settings.MaxResponseDays is not null)
            .WithMessage("errors.number.range");
        RuleFor(settings => settings.ResponseReminderDays).InclusiveBetween(1, 60).WithMessage("errors.number.range");
        RuleFor(settings => settings.ReminderLeadHours).InclusiveBetween(1, 168).WithMessage("errors.number.range");
        RuleFor(settings => settings.ConflictKinds).NotNull().WithMessage("errors.required");
        RuleFor(settings => settings.HiddenPositions).NotNull().WithMessage("errors.required");

        // Shown to members as a link, so a scheme a browser follows and nothing else: the same rule as a link of the library.
        RuleFor(settings => settings.TheoryExamUrl)
            .MaximumLength(LinkWriteDtoValidator.MaxUrlLength).WithMessage("errors.text.tooLong")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            .When(settings => settings.TheoryExamUrl is not null)
            .WithMessage("errors.url.absolute");
    }
}

/// <summary>
/// The rules of a save: those of the values, and the two that read what the hub knows — the kinds of the calendar, and the
/// positions the division trains on. A position the core no longer has is refused on its row, so that the department sees it
/// and takes it out, rather than finding it gone.
/// </summary>
public sealed class TrainingSettingsSaveValidator : AbstractValidator<TrainingSettings>
{
    public TrainingSettingsSaveValidator(RatingVocabulary vocabulary, TrainingReference reference, HubDbContext hub)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(hub);

        Include(new TrainingSettingsValidator(vocabulary));

        RuleFor(settings => settings.ConflictKinds)
            .MustAsync(async (keys, cancellationToken) =>
            {
                var known = await hub.CalendarKinds.AsNoTracking()
                    .Where(kind => keys.Contains(kind.Key))
                    .Select(kind => kind.Key)
                    .ToListAsync(cancellationToken);
                return keys.All(known.Contains);
            })
            .When(settings => settings.ConflictKinds is not null)
            .WithMessage("training:errors.calendarKindUnknown");

        RuleFor(settings => settings.HiddenPositions).CustomAsync(async (callsigns, context, cancellationToken) =>
        {
            if (callsigns is null || callsigns.Count == 0)
            {
                return;
            }

            var trained = (await reference.PositionsAsync(cancellationToken)).Select(position => position.Callsign).ToHashSet(StringComparer.Ordinal);

            for (var index = 0; index < callsigns.Count; index++)
            {
                if (!trained.Contains(callsigns[index]))
                {
                    context.AddFailure($"hiddenPositions[{index}].callsign", "training:errors.positionUnknown");
                }
            }
        });
    }
}
