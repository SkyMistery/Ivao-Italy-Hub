using FluentValidation.Results;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Settings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of the training's skeleton (M3, A4) that need no database: the defaults of the settings and their rules, as
/// design M3 §1.6 writes them, and the catalogue of the permissions (§3.1).
/// <para>The ratings are a vocabulary of this test's making, not the network's (design M3 §10): the rules have to hold for
/// whatever ladders the core is given.</para>
/// </summary>
public sealed class TrainingSettingsTests
{
    private static readonly RatingVocabulary Ratings = new(
    [
        new(RatingKind.Atc, 11, "A1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Atc, 12, "A2", HasPracticalTraining: true, PositionType: "POST"),
        new(RatingKind.Atc, 13, "A3", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Pilot, 21, "P1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Pilot, 22, "P2", HasPracticalTraining: true, PositionType: null),
    ]);

    private static readonly TrainingSettingsValidator Validator = new(Ratings);

    [Fact]
    public void TheDefaultsAreTheDesignsAndKnowNoDivision()
    {
        var defaults = new TrainingSettings();

        Assert.Empty(defaults.MinimumHours);
        Assert.Equal(5, defaults.CooldownDays);
        Assert.Equal(14, defaults.NoShowCooldownDays);
        Assert.Null(defaults.MaxResponseDays);
        Assert.Equal(3, defaults.ResponseReminderDays);
        Assert.Equal(ConflictPolicy.Warn, defaults.ConflictPolicy);
        Assert.Equal(["event"], defaults.ConflictKinds);
        Assert.Equal(24, defaults.ReminderLeadHours);
        Assert.Empty(defaults.HiddenPositions);
        Assert.Null(defaults.TheoryExamUrl);

        Assert.True(Validator.Validate(defaults).IsValid);
    }

    [Fact]
    public void AThresholdIsOnARatingTheVocabularyTrainsForOnceAndInWholeHours()
    {
        Assert.True(Validator.Validate(With([new(RatingKind.Atc, 12, 50), new(RatingKind.Pilot, 22, 30)])).IsValid);

        // Untrained, unknown, or trained on the other ladder: refused on the rating of the row.
        foreach (var threshold in new HoursThreshold[] { new(RatingKind.Atc, 11, 50), new(RatingKind.Atc, 99, 50), new(RatingKind.Pilot, 12, 50) })
        {
            Assert.Equal(
                [("minimumHours[0].rating", "training:errors.ratingNotTrained")],
                Failures(Validator.Validate(With([threshold]))));
        }

        // The same rating twice: both rows say so, and the other ladder's rung with the same place is another rating.
        Assert.Equal(
            [("minimumHours[0].rating", "training:errors.ratingTwice"), ("minimumHours[1].rating", "training:errors.ratingTwice")],
            Failures(Validator.Validate(With([new(RatingKind.Atc, 12, 50), new(RatingKind.Atc, 12, 60), new(RatingKind.Pilot, 22, 60)]))));

        Assert.Equal(
            [("minimumHours[0].hours", "errors.number.range"), ("minimumHours[1].hours", "errors.number.range")],
            Failures(Validator.Validate(With([new(RatingKind.Atc, 12, 0), new(RatingKind.Pilot, 22, TrainingSettingsValidator.MaxHours + 1)]))));
    }

    [Fact]
    public void TheWaitsTheTimesAndTheLeadHaveTheirRanges()
    {
        Assert.True(Validator.Validate(new TrainingSettings { CooldownDays = 0, NoShowCooldownDays = 0, MaxResponseDays = 365 }).IsValid);

        Assert.Equal(
            [
                ("cooldownDays", "errors.number.range"),
                ("noShowCooldownDays", "errors.number.range"),
                ("maxResponseDays", "errors.number.range"),
                ("responseReminderDays", "errors.number.range"),
                ("reminderLeadHours", "errors.number.range"),
            ],
            Failures(Validator.Validate(new TrainingSettings
            {
                CooldownDays = -1,
                NoShowCooldownDays = 366,
                MaxResponseDays = 0,
                ResponseReminderDays = 0,
                ReminderLeadHours = 169,
            })));
    }

    [Fact]
    public void TheSiteOfTheExamIsAnAddressABrowserFollows()
    {
        Assert.True(Validator.Validate(new TrainingSettings { TheoryExamUrl = "https://exam.example.org/theory" }).IsValid);

        foreach (var address in new[] { "javascript:alert(1)", "exam.example.org/theory", "/theory" })
        {
            Assert.Equal(
                [("theoryExamUrl", "errors.url.absolute")],
                Failures(Validator.Validate(new TrainingSettings { TheoryExamUrl = address })));
        }

        var tooLong = "https://exam.example.org/" + new string('x', 1024);
        Assert.Contains(("theoryExamUrl", "errors.text.tooLong"), Failures(Validator.Validate(new TrainingSettings { TheoryExamUrl = tooLong })));
    }

    [Fact]
    public void FiveOfTheNinePermissionsAreDeniedToWhoeverATrainingIsAbout()
    {
        Assert.Equal(9, TrainingPermissions.All.DistinctBy(permission => permission.Name).Count());
        Assert.All(TrainingPermissions.All, permission =>
        {
            Assert.StartsWith($"{TrainingPermissions.Area}.", permission.Name, StringComparison.Ordinal);
            Assert.False(permission.IsGlobal);
        });

        Assert.Equal(
            new[] { TrainingPermissions.Approve, TrainingPermissions.Assign, TrainingPermissions.Conduct, TrainingPermissions.Edit, TrainingPermissions.Ban }
                .Order(StringComparer.Ordinal),
            TrainingPermissions.All.Where(permission => permission.DeniedToStakeholder).Select(permission => permission.Name).Order(StringComparer.Ordinal));
    }

    private static TrainingSettings With(IReadOnlyList<HoursThreshold> thresholds) => new() { MinimumHours = thresholds };

    /// <summary>The failures as the API sends them: the field as the form spells it, and the key.</summary>
    private static (string Field, string Key)[] Failures(ValidationResult result) =>
        [.. result.Errors.Select(failure => (CrudProblems.FieldName(failure.PropertyName), failure.ErrorMessage))];
}
