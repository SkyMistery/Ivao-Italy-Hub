using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// IVAO's ratings as the core knows them (M3, A1, decision note of 25 September 2026), and the three questions the training
/// asks instead of writing numbers of its own. These tests are about the real vocabulary; the module's are about one of its
/// own (design M3 §10). Where it can, the vocabulary is held against what IVAO sent on 25 September 2026: the ratings of a
/// real profile and the positions IVAO publishes.
/// </summary>
public sealed class RatingVocabularyTests
{
    private static readonly RatingVocabulary Ivao = IvaoRatings.Vocabulary;

    [Fact]
    public void TheLaddersAreIvaosFromTheLowestUp()
    {
        Assert.Equal(
            ["AS1", "AS2", "AS3", "ADC", "APC", "ACC", "SEC", "SAI", "CAI"],
            Ivao.Ladder(RatingKind.Atc).Select(rating => rating.ShortName));
        Assert.Equal(
            ["FS1", "FS2", "FS3", "PP", "SPP", "CP", "ATP", "SFI", "CFI"],
            Ivao.Ladder(RatingKind.Pilot).Select(rating => rating.ShortName));

        // IVAO's own numbers, 2 to 10 on both ladders: a new member starts at 2.
        Assert.Equal(Enumerable.Range(2, 9), Ivao.Ladder(RatingKind.Atc).Select(rating => rating.Number));
        Assert.Equal(Enumerable.Range(2, 9), Ivao.Ladder(RatingKind.Pilot).Select(rating => rating.Number));
    }

    [Fact]
    public void OnlyTheRatingsADivisionTrainsForHaveAPracticalTraining()
    {
        var trained = Ivao.Ladder(RatingKind.Atc).Concat(Ivao.Ladder(RatingKind.Pilot))
            .Where(rating => rating.HasPracticalTraining)
            .Select(rating => rating.ShortName);

        Assert.Equal(["ADC", "APC", "ACC", "PP", "SPP", "CP"], trained);
    }

    [Theory]
    [InlineData(RatingKind.Atc, 2, null)] // AS1: AS2 comes next, and nobody trains for it
    [InlineData(RatingKind.Atc, 3, null)]
    [InlineData(RatingKind.Atc, 4, "ADC")] // an AS3 is offered ADC (design M3 §2.2)
    [InlineData(RatingKind.Atc, 5, "APC")]
    [InlineData(RatingKind.Atc, 6, "ACC")]
    [InlineData(RatingKind.Atc, 7, null)] // an ACC is offered nothing: SEC has no practical training
    [InlineData(RatingKind.Atc, 8, null)]
    [InlineData(RatingKind.Atc, 10, null)] // the top of the ladder
    [InlineData(RatingKind.Pilot, 3, null)]
    [InlineData(RatingKind.Pilot, 4, "PP")]
    [InlineData(RatingKind.Pilot, 5, "SPP")]
    [InlineData(RatingKind.Pilot, 6, "CP")]
    [InlineData(RatingKind.Pilot, 7, null)]
    [InlineData(RatingKind.Pilot, 10, null)]
    [InlineData(RatingKind.Atc, null, null)] // a member IVAO said nothing about
    [InlineData(RatingKind.Atc, 1, null)] // a number IVAO does not use: never a training on a guess
    [InlineData(RatingKind.Pilot, 11, null)]
    public void TheNextTrainingIsTheRatingJustAboveIfItIsTrained(RatingKind kind, int? current, string? expected)
    {
        Assert.Equal(expected, Ivao.NextTraining(kind, current)?.ShortName);
    }

    [Theory]
    [InlineData(RatingKind.Atc, 5, 5, true)] // an ADC trains ADC
    [InlineData(RatingKind.Atc, 4, 5, false)] // an AS3 does not
    [InlineData(RatingKind.Atc, 8, 7, true)] // SEC, SAI and CAI train everything: it follows from the order
    [InlineData(RatingKind.Atc, 9, 5, true)]
    [InlineData(RatingKind.Atc, 10, 7, true)]
    [InlineData(RatingKind.Atc, 6, 7, false)]
    [InlineData(RatingKind.Pilot, 7, 7, true)]
    [InlineData(RatingKind.Pilot, 8, 5, true)]
    [InlineData(RatingKind.Pilot, 6, 7, false)]
    [InlineData(RatingKind.Atc, null, 5, false)] // nothing known is at least nothing
    [InlineData(RatingKind.Atc, 11, 5, false)]
    [InlineData(RatingKind.Atc, 10, 11, false)] // nor is anything at least a rating that does not exist
    public void AtLeastFollowsTheLadder(RatingKind kind, int? number, int required, bool expected)
    {
        Assert.Equal(expected, Ivao.IsAtLeast(kind, number, required));
    }

    [Fact]
    public void EveryTrainedAtcRatingIsTrainedOnOneKindOfPosition()
    {
        var positions = Ivao.Ladder(RatingKind.Atc)
            .Where(rating => rating.HasPracticalTraining)
            .Select(rating => (rating.ShortName, rating.PositionType));

        Assert.Equal([("ADC", "TWR"), ("APC", "APP"), ("ACC", "CTR")], positions);
        Assert.All(Ivao.Ladder(RatingKind.Pilot), rating => Assert.Null(rating.PositionType));
        Assert.All(Ivao.Ladder(RatingKind.Atc).Where(rating => !rating.HasPracticalTraining), rating => Assert.Null(rating.PositionType));
    }

    [Fact]
    public void ThePositionsARatingIsTrainedOnAreOnesIvaoPublishes()
    {
        // The positions of two airports and the sectors of a FIR, recorded from /v2/ATCPositions/all and
        // /v2/subcenters/all on 25 September 2026: a type written here that IVAO never uses would find no position at all.
        var published = IvaoFixtures.Read("atc-positions-sample.json").EnumerateArray()
            .Concat(IvaoFixtures.Read("subcenters-sample.json").EnumerateArray())
            .Select(position => position.GetProperty("position").GetString())
            .ToHashSet();

        foreach (var rating in Ivao.Ladder(RatingKind.Atc).Where(rating => rating.PositionType is not null))
        {
            Assert.Contains(rating.PositionType, published);
        }

        // And none of them says which rating it asks for, which is why the vocabulary does.
        Assert.All(
            IvaoFixtures.Read("atc-positions-sample.json").EnumerateArray()
                .Concat(IvaoFixtures.Read("subcenters-sample.json").EnumerateArray()),
            position => Assert.DoesNotContain(
                position.EnumerateObject(),
                field => field.Name.Contains("rating", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void TheRatingsOfARealProfileAreInTheVocabularyWithTheSameName()
    {
        // The only place IVAO spells a rating to the hub: the profile of the member who signs in.
        var rating = IvaoFixtures.Read("users-me-790001.json").GetProperty("rating");

        foreach (var (kind, field) in new[] { (RatingKind.Atc, "atcRating"), (RatingKind.Pilot, "pilotRating") })
        {
            var sent = rating.GetProperty(field);
            var known = Ivao.Find(kind, sent.GetProperty("id").GetInt32());

            Assert.NotNull(known);
            Assert.Equal(sent.GetProperty("shortName").GetString(), known.ShortName);
        }
    }

    [Fact]
    public void EveryRatingHasItsNameInEveryLanguage()
    {
        var catalog = new LocaleCatalog(
            HubPaths.Resolve(AppContext.BaseDirectory),
            Options.Create(new DivisionOptions
            {
                Code = "IT",
                CountryId = "IT",
                Domain = "it.ivao.aero",
                Timezone = "Europe/Rome",
                Locales = ["it", "en"],
                DefaultLocale = "it",
            }));

        foreach (var rating in Ivao.Ladder(RatingKind.Atc).Concat(Ivao.Ladder(RatingKind.Pilot)))
        {
            foreach (var locale in new[] { "it", "en" })
            {
                Assert.NotNull(catalog.Get(locale, rating.NameKey));
            }
        }

        Assert.Equal("Aerodrome Controller", catalog.Get("en", Ivao.Find(RatingKind.Atc, 5)!.NameKey));
    }

    [Fact]
    public void AVocabularyOfAnotherNetworkAnswersTheSameQuestions()
    {
        // What the training's own tests will do (design M3 §10): the same class, ratings that are not IVAO's. The order is
        // the list's, not the numbers', which here run the other way.
        var other = new RatingVocabulary(
        [
            new(RatingKind.Atc, 30, "S1", false, null),
            new(RatingKind.Atc, 20, "S2", true, "GND"),
            new(RatingKind.Atc, 10, "C1", false, null),
        ]);

        Assert.Equal("S2", other.NextTraining(RatingKind.Atc, 30)?.ShortName);
        Assert.Null(other.NextTraining(RatingKind.Atc, 20));
        Assert.True(other.IsAtLeast(RatingKind.Atc, 10, 20));
        Assert.False(other.IsAtLeast(RatingKind.Atc, 30, 20));
        Assert.Empty(other.Ladder(RatingKind.Pilot));
    }

    [Fact]
    public void ARatingIsWrittenOnceOnItsLadder()
    {
        Assert.Throws<ArgumentException>(() => new RatingVocabulary(
        [
            new(RatingKind.Atc, 5, "ADC", true, "TWR"),
            new(RatingKind.Atc, 5, "APC", true, "APP"),
        ]));
        Assert.Throws<ArgumentException>(() => new RatingVocabulary(
        [
            new(RatingKind.Atc, 5, "ADC", true, "TWR"),
            new(RatingKind.Atc, 6, "adc", true, "APP"),
        ]));

        // The same number on the two ladders is two ratings, as it is on IVAO.
        _ = new RatingVocabulary([new(RatingKind.Atc, 5, "ADC", true, "TWR"), new(RatingKind.Pilot, 5, "PP", true, null)]);
    }
}
