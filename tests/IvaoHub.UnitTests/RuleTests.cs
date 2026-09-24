using System.Text.Json.Nodes;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of T9 that need no database (design M2 §1.7, §5.2): the parameters of a rule as the catalogue of the checks
/// reads them — the starting values on a rule of its own, only what changes on an amendment —, the rules in force on a tour
/// and on a subtour, and the copy of a tour's rules that keeps what the tour already has.
/// </summary>
public sealed class RuleTests
{
    private static readonly DateTime Retired = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ARuleOfItsOwnStartsFromTheValuesOfItsCheck()
    {
        var (parameters, problems) = CheckCatalog.Read(CheckCatalog.Disconnections, JsonNode.Parse("""{"maxTotalDisconnectMinutes":30}"""), amending: false);

        Assert.Empty(problems);
        Assert.Equal("""{"maxSingleDisconnectMinutes":15,"maxTotalDisconnectMinutes":30}""", parameters.ToJsonString());
    }

    [Fact]
    public void AnAmendmentKeepsOnlyWhatItChanges()
    {
        var (parameters, problems) = CheckCatalog.Read(
            CheckCatalog.Disconnections,
            JsonNode.Parse("""{"maxSingleDisconnectMinutes":10,"minutes":3}"""),
            amending: true);

        Assert.Empty(problems);
        Assert.Equal("""{"maxSingleDisconnectMinutes":10}""", parameters.ToJsonString());
    }

    [Theory]
    [InlineData(CheckCatalog.Disconnections, """{"maxSingleDisconnectMinutes":-1}""", "maxSingleDisconnectMinutes", "errors.number.range")]
    [InlineData(CheckCatalog.Vmc, """{"minVisibilityMeters":"five"}""", "minVisibilityMeters", "errors.number.range")]
    [InlineData(CheckCatalog.FlightRules, """{"rules":[]}""", "rules", "errors.required")]
    [InlineData(CheckCatalog.FlightRules, """{"rules":["X"]}""", "rules", "flightops:errors.parameterChoice")]
    [InlineData(CheckCatalog.Equipment, """{"lettersI":["W","Q9"]}""", "lettersI", "flightops:errors.parameterChoice")]
    [InlineData(CheckCatalog.Equipment, """{"transponderV":["S","W"]}""", "transponderV", "flightops:errors.parameterChoice")]
    [InlineData(CheckCatalog.Equipment, """{"highLevelFl":90}""", "highLevelFl", "errors.number.range")]
    public void AWrongParameterIsRefusedOnTheField(string check, string input, string field, string key) =>
        Assert.Contains(new ShapeProblem(field, key), CheckCatalog.Read(check, JsonNode.Parse(input), amending: false).Problems);

    [Fact]
    public void AnEquipmentRuleStartsWithWAndJ1OnlyAboveFl285AndNothingElseRequired()
    {
        var (parameters, problems) = CheckCatalog.Read(CheckCatalog.Equipment, JsonNode.Parse("""{"lettersI":["s","D","W"]}"""), amending: false);

        Assert.Empty(problems);
        Assert.Equal(["S", "D", "W"], OpenCatalog.Codes(parameters, "lettersI"));
        Assert.Equal(["W", "J1"], OpenCatalog.Codes(parameters, "highLevelLetters"));
        Assert.Equal(285, OpenCatalog.Number(parameters, "highLevelFl"));
        Assert.Empty(OpenCatalog.Codes(parameters, "lettersV"));
        Assert.Equal(["I", "V", "Y", "Z"], OpenCatalog.Codes(CheckCatalog.Read(CheckCatalog.FlightRules, null, amending: false).Parameters, "rules"));
    }

    [Fact]
    public void ARuleWithoutACheckHasNoParameters()
    {
        var (parameters, problems) = CheckCatalog.Read(null, JsonNode.Parse("""{"radiusNm":5}"""), amending: false);

        Assert.Empty(problems);
        Assert.Equal("{}", parameters.ToJsonString());
    }

    [Fact]
    public void EveryCheckWithParametersHasAStartingValueOrAsksForOne()
    {
        // The tolerance of the take-off from the threshold is a setting (answer 15): no parameter here.
        Assert.Empty(CheckCatalog.Fields(CheckCatalog.TakeoffFromThreshold));

        foreach (var field in CheckCatalog.Keys.SelectMany(CheckCatalog.Fields))
        {
            Assert.True(field.Default is not null || field.Type != ParameterType.Whole, $"{field.Name} has no starting value");
            Assert.True(field.Default is null || (field.Default >= field.Min && field.Default <= field.Max), $"{field.Name} starts out of its bounds");
        }
    }

    [Fact]
    public void AnAmendmentTakesThePlaceOfItsRuleWithTheParametersItChanges()
    {
        var connection = General(1, "GR4", CheckCatalog.Disconnections, """{"maxSingleDisconnectMinutes":15,"maxTotalDisconnectMinutes":25}""", sort: 4);
        var procedures = General(2, "GR2", check: null, "{}", sort: 2);
        var amendment = OfTour(10, tourId: 7, "IR1", """{"maxTotalDisconnectMinutes":40}""", amends: 1);
        var own = OfTour(11, tourId: 7, "IR2", "{}");

        var effective = EffectiveRules.Compose(
            [connection, procedures],
            [[own, amendment]],
            [new() { RuleId = 1, ErrorId = 100 }, new() { RuleId = 10, ErrorId = 101 }]);

        // The general rules in their order, the amendment in its rule's place, the tour's own after them.
        Assert.Equal(["GR2", "IR1", "IR2"], effective.Select(rule => rule.Rule.Code));
        var amended = effective[1];
        Assert.Same(connection, amended.Amended);
        Assert.Equal(CheckCatalog.Disconnections, amended.CheckKey);
        Assert.Equal("""{"maxSingleDisconnectMinutes":15,"maxTotalDisconnectMinutes":40}""", amended.Parameters.ToJsonString());

        // An amendment keeps its rule's errors and adds its own.
        Assert.Equal([100L, 101L], amended.ErrorIds);
    }

    [Fact]
    public void ARetiredRuleHoldsNowhereAndTakesItsAmendmentsWithIt()
    {
        var retired = General(1, "GR1", check: null, "{}");
        retired.RetiredAt = Retired;
        var standing = General(2, "GR2", check: null, "{}");
        var amendmentOfRetired = OfTour(10, tourId: 7, "IR1", "{}", amends: 1);
        var retiredAmendment = OfTour(11, tourId: 7, "IR2", "{}", amends: 2);
        retiredAmendment.RetiredAt = Retired;
        var retiredOwn = OfTour(12, tourId: 7, "IR3", "{}");
        retiredOwn.RetiredAt = Retired;

        var effective = EffectiveRules.Compose([retired, standing], [[amendmentOfRetired, retiredAmendment, retiredOwn]], []);

        // The general rule the retired amendment amended holds again, as it is.
        Assert.Equal(["GR2"], effective.Select(rule => rule.Rule.Code));
        Assert.Null(effective[0].Amended);
    }

    [Fact]
    public void ASubtourHoldsItsParentsRulesFirstAndItsOwnAmendmentWins()
    {
        var parking = General(1, "GR7", CheckCatalog.Parking, """{"minParkingMinutesBefore":2,"minParkingMinutesAfter":2}""");
        var parentAmendment = OfTour(10, tourId: 7, "IR7", """{"minParkingMinutesBefore":5}""", amends: 1);
        var parentOwn = OfTour(11, tourId: 7, "IR8", "{}");
        var subtourAmendment = OfTour(20, tourId: 8, "SR7", """{"minParkingMinutesAfter":10}""", amends: 1);
        var subtourOwn = OfTour(21, tourId: 8, "SR8", "{}");

        var effective = EffectiveRules.Compose([parking], [[parentAmendment, parentOwn], [subtourAmendment, subtourOwn]], []);

        Assert.Equal(["SR7", "IR8", "SR8"], effective.Select(rule => rule.Rule.Code));
        Assert.Same(parking, effective[0].Amended);
        Assert.Equal("""{"minParkingMinutesBefore":5,"minParkingMinutesAfter":10}""", effective[0].Parameters.ToJsonString());
    }

    [Fact]
    public void TheCopyKeepsWhatTheTourAlreadyHas()
    {
        var target = new Tour { Id = 9 };
        var existing = new[] { OfTour(30, tourId: 9, "IR1", "{}"), OfTour(31, tourId: 9, "IR9", "{}", amends: 1) };
        var retired = OfTour(12, tourId: 7, "IR5", "{}");
        retired.RetiredAt = Retired;
        var source = new[]
        {
            OfTour(10, tourId: 7, "IR1", "{}"),
            OfTour(11, tourId: 7, "IR2", """{"toleranceKt":20}""", amends: 1),
            retired,
            OfTour(13, tourId: 7, "IR3", """{"radiusNm":3}""", check: CheckCatalog.LandingAtArrival),
        };
        source[3].ErrorLinks = [new() { RuleId = 13, ErrorId = 100 }];

        var (added, skipped) = TourCopy.Rules(source, target, existing);

        Assert.Equal(["IR1", "IR2"], skipped);
        var copy = Assert.Single(added);
        Assert.Equal((9L, "IR3", CheckCatalog.LandingAtArrival, """{"radiusNm":3}"""), (copy.TourId!.Value, copy.Code, copy.CheckKey, copy.ParametersJson));
        Assert.Equal([100L], copy.ErrorLinks.Select(link => link.ErrorId));
        Assert.Equal(0, copy.Id);
    }

    private static TourRule General(long id, string code, string? check, string parameters, int sort = 0) => new()
    {
        Id = id,
        Code = code,
        CheckKey = check,
        ParametersJson = parameters,
        Sort = sort,
    };

    private static TourRule OfTour(long id, long tourId, string code, string parameters, long? amends = null, string? check = null) => new()
    {
        Id = id,
        TourId = tourId,
        Code = code,
        AmendsRuleId = amends,
        CheckKey = check,
        ParametersJson = parameters,
    };
}
