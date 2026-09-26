using FluentValidation.Results;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Training.Sheets;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of an item of the evaluation sheet (M3, A5; design M3 §1.4), which need no database: a rating the vocabulary
/// gives a practical training, on the ladder the item says; a title in every language of the division (§12 n.11); a place in
/// the sheet.
/// <para>The ratings and the languages are of this test's making, not the network's nor this division's (design M3 §10): the
/// rules have to hold for whatever ladders the core is given and whatever languages a division speaks.</para>
/// </summary>
public sealed class TrainingSheetItemTests
{
    private static readonly RatingVocabulary Ratings = new(
    [
        new(RatingKind.Atc, 11, "A1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Atc, 12, "A2", HasPracticalTraining: true, PositionType: "POST"),
        new(RatingKind.Pilot, 21, "P1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Pilot, 22, "P2", HasPracticalTraining: true, PositionType: null),
    ]);

    private static readonly SheetItemWriteDtoValidator Validator = new(
        Ratings,
        Options.Create(new DivisionOptions
        {
            Code = "XX",
            CountryId = "XX",
            Domain = "example.org",
            Locales = ["xx", "yy"],
            DefaultLocale = "xx",
            Timezone = "UTC",
        }));

    [Fact]
    public void AnItemInEveryLanguageOnATrainedRatingIsValid()
    {
        Assert.True(Validator.Validate(Item(RatingKind.Atc, 12, SheetSection.Practice)).IsValid);
        Assert.True(Validator.Validate(Item(RatingKind.Pilot, 22, SheetSection.Theory)).IsValid);
    }

    [Fact]
    public void AnItemIsOnARatingTheVocabularyTrainsOnItsOwnLadder()
    {
        // Untrained, unknown, or trained on the other ladder: refused on the rating, which is the field the form draws.
        foreach (var (kind, number) in new[] { (RatingKind.Atc, 11), (RatingKind.Atc, 99), (RatingKind.Pilot, 12), (RatingKind.Atc, 22) })
        {
            Assert.Equal(
                [("rating", "training:errors.ratingNotTrained")],
                Failures(Validator.Validate(Item(kind, number, SheetSection.Practice))));
        }
    }

    [Fact]
    public void ATitleIsWrittenInEveryLanguageOfTheDivision()
    {
        var missing = Item(RatingKind.Atc, 12, SheetSection.Practice) with
        {
            Title = new Localized<string>([new("xx", "Phraseology"), new("yy", "   ")]),
        };

        var result = Validator.Validate(missing);

        Assert.Equal([("title", LocalizedRules.MissingMessageKey)], Failures(result));

        // The languages missing travel with the failure, so the form can name them.
        Assert.Equal(["yy"], Assert.IsType<LocalizedMissing>(result.Errors[0].CustomState).Locales);
    }

    [Fact]
    public void AnItemHasAPlaceInItsSheetAndASection()
    {
        Assert.True(Validator.Validate(Item(RatingKind.Atc, 12, SheetSection.Practice) with { Sort = 0 }).IsValid);
        Assert.True(Validator.Validate(Item(RatingKind.Atc, 12, SheetSection.Practice) with { Sort = SheetItemWriteDtoValidator.MaxSort }).IsValid);

        Assert.Equal(
            [("sort", "errors.number.range")],
            Failures(Validator.Validate(Item(RatingKind.Atc, 12, SheetSection.Practice) with { Sort = -1 })));
        Assert.Equal(
            [("sort", "errors.number.range")],
            Failures(Validator.Validate(Item(RatingKind.Atc, 12, SheetSection.Practice) with { Sort = SheetItemWriteDtoValidator.MaxSort + 1 })));
        Assert.Equal(
            [("section", "errors.required")],
            Failures(Validator.Validate(Item(RatingKind.Atc, 12, (SheetSection)7))));
    }

    [Fact]
    public void AnItemStartsActive() => Assert.True(new SheetItem().IsActive);

    private static SheetItemWriteDto Item(RatingKind kind, int number, SheetSection section) => new(
        kind,
        number,
        section,
        new Localized<string>([new("xx", "Phraseology"), new("yy", "Fraseologia")]),
        Sort: 1,
        IsActive: true,
        RowVersion: default);

    /// <summary>The failures as the API sends them: the field as the form spells it, and the key.</summary>
    private static (string Field, string Key)[] Failures(ValidationResult result) =>
        [.. result.Errors.Select(failure => (CrudProblems.FieldName(failure.PropertyName), failure.ErrorMessage))];
}
