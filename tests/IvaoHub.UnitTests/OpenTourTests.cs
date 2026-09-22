using System.Text.Json.Nodes;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of T7c that need no database (design M2 §2.6.1, note 2026-09-22-il-tour-open): the parameters of every goal
/// and every constraint as the catalogue reads them — kept, normalized, refused on the field — and what "ready" asks of an
/// <c>Open</c> tour.
/// </summary>
public sealed class OpenTourTests
{
    [Theory]
    [InlineData(OpenGoal.Distance, """{"nm":21600}""", """{"nm":21600}""")]
    [InlineData(OpenGoal.FlightCount, """{"count":50,"nm":3}""", """{"count":50}""")]
    [InlineData(OpenGoal.DistinctAirports, """{"count":30}""", """{"count":30}""")]
    [InlineData(OpenGoal.DistinctCountries, """{"count":20}""", """{"count":20}""")]
    [InlineData(OpenGoal.CollectList, """{"airports":[" lirf","LIMC","lirf"]}""", """{"airports":["LIRF","LIMC"]}""")]
    [InlineData(OpenGoal.CollectList, """{"airports":["LIRF","LIMC"],"count":1}""", """{"airports":["LIRF","LIMC"],"count":1}""")]
    [InlineData(OpenGoal.CollectRegions, """{"firs":["lirr","limm"]}""", """{"firs":["LIRR","LIMM"]}""")]
    [InlineData(OpenGoal.CollectRegions, """{"countries":["it","fr"],"count":2}""", """{"countries":["IT","FR"],"count":2}""")]
    public void AGoalKeepsItsOwnFieldsNormalized(OpenGoal goal, string input, string kept)
    {
        var (parameters, problems) = OpenCatalog.Read(goal, JsonNode.Parse(input));

        Assert.Empty(problems);
        Assert.Equal(kept, parameters.ToJsonString());
    }

    [Theory]
    [InlineData(OpenGoal.Distance, "{}", "nm", "errors.required")]
    [InlineData(OpenGoal.Distance, """{"nm":0}""", "nm", "errors.number.range")]
    [InlineData(OpenGoal.Distance, """{"nm":50001}""", "nm", "errors.number.range")]
    [InlineData(OpenGoal.Distance, """{"nm":12.5}""", "nm", "errors.number.range")]
    [InlineData(OpenGoal.FlightCount, """{"count":"ten"}""", "count", "errors.number.range")]
    [InlineData(OpenGoal.DistinctCountries, """{"count":251}""", "count", "errors.number.range")]
    [InlineData(OpenGoal.CollectList, "{}", "airports", "errors.required")]
    [InlineData(OpenGoal.CollectList, """{"airports":["LIRF","ROMA1"]}""", "airports", "flightops:errors.icao")]
    [InlineData(OpenGoal.CollectList, """{"airports":["LIRF"],"count":2}""", "count", "flightops:errors.countAboveList")]
    [InlineData(OpenGoal.CollectRegions, "{}", "countries", "flightops:errors.regionsOneKind")]
    [InlineData(OpenGoal.CollectRegions, """{"countries":["IT"],"firs":["LIRR"]}""", "countries", "flightops:errors.regionsOneKind")]
    [InlineData(OpenGoal.CollectRegions, """{"countries":["ITA"]}""", "countries", "flightops:errors.countryCode")]
    [InlineData(OpenGoal.CollectRegions, """{"firs":["LI RR"]}""", "firs", "flightops:errors.firCode")]
    public void AGoalWithAWrongParameterIsRefusedOnTheField(OpenGoal goal, string input, string field, string key) =>
        Assert.Contains(new ShapeProblem(field, key), OpenCatalog.Read(goal, JsonNode.Parse(input)).Problems);

    [Theory]
    [InlineData(TourConstraintKind.DepartureOrArrivalIn, """{"countries":["it"]}""", """{"countries":["IT"]}""")]
    [InlineData(TourConstraintKind.DepartureIn, """{"countries":["IT","FR"]}""", """{"countries":["IT","FR"]}""")]
    [InlineData(TourConstraintKind.ArrivalIn, """{"countries":["IT"]}""", """{"countries":["IT"]}""")]
    [InlineData(TourConstraintKind.TouchesAirport, """{"airports":["LIRF","LIMC"]}""", """{"airports":["LIRF","LIMC"]}""")]
    [InlineData(TourConstraintKind.DistanceBetween, """{"minNm":200,"maxNm":1500}""", """{"minNm":200,"maxNm":1500}""")]
    [InlineData(TourConstraintKind.DistanceBetween, """{"maxNm":1500}""", """{"maxNm":1500}""")]
    [InlineData(TourConstraintKind.AircraftCategory, """{"categories":["l","M"]}""", """{"categories":["L","M"]}""")]
    [InlineData(TourConstraintKind.ArrivalRunwayMax, """{"meters":1500}""", """{"meters":1500}""")]
    [InlineData(TourConstraintKind.ArrivalElevationMin, """{"feet":5000}""", """{"feet":5000}""")]
    [InlineData(TourConstraintKind.FlightRules, """{"rules":"v"}""", """{"rules":"V"}""")]
    [InlineData(TourConstraintKind.Chained, """{"count":3}""", "{}")]
    [InlineData(TourConstraintKind.Eastbound, "{}", "{}")]
    [InlineData(TourConstraintKind.Westbound, "{}", "{}")]
    [InlineData(TourConstraintKind.IncreasingDistance, "{}", "{}")]
    [InlineData(TourConstraintKind.MinFlightsAt, """{"airport":"lirf","count":3}""", """{"airport":"LIRF","count":3}""")]
    public void AConstraintKeepsItsOwnFieldsNormalized(TourConstraintKind kind, string input, string kept)
    {
        var (parameters, problems) = OpenCatalog.Read(kind, JsonNode.Parse(input));

        Assert.Empty(problems);
        Assert.Equal(kept, parameters.ToJsonString());
    }

    [Theory]
    [InlineData(TourConstraintKind.DepartureIn, """{"countries":[]}""", "countries", "errors.required")]
    [InlineData(TourConstraintKind.TouchesAirport, """{"airports":["LIRF1"]}""", "airports", "flightops:errors.icao")]
    [InlineData(TourConstraintKind.DistanceBetween, "{}", "minNm", "flightops:errors.distanceBoundRequired")]
    [InlineData(TourConstraintKind.DistanceBetween, """{"minNm":1500,"maxNm":200}""", "maxNm", "flightops:errors.distanceBounds")]
    [InlineData(TourConstraintKind.AircraftCategory, """{"categories":["X"]}""", "categories", "flightops:errors.parameterChoice")]
    [InlineData(TourConstraintKind.ArrivalRunwayMax, """{"meters":50}""", "meters", "errors.number.range")]
    [InlineData(TourConstraintKind.ArrivalElevationMin, "{}", "feet", "errors.required")]
    [InlineData(TourConstraintKind.FlightRules, """{"rules":"Y"}""", "rules", "flightops:errors.parameterChoice")]
    [InlineData(TourConstraintKind.FlightRules, """{"rules":["I","V"]}""", "rules", "errors.text.tooLong")]
    [InlineData(TourConstraintKind.MinFlightsAt, """{"count":3}""", "airport", "errors.required")]
    [InlineData(TourConstraintKind.MinFlightsAt, """{"airport":"LIRF","count":0}""", "count", "errors.number.range")]
    public void AConstraintWithAWrongParameterIsRefusedOnTheField(TourConstraintKind kind, string input, string field, string key) =>
        Assert.Contains(new ShapeProblem(field, key), OpenCatalog.Read(kind, JsonNode.Parse(input)).Problems);

    [Fact]
    public void AListOfConstraintsShowsTheValuesWithTheirUnits()
    {
        Assert.Equal(
            ["≥ 200 NM", "≤ 1500 NM"],
            OpenCatalog.Describe(OpenCatalog.Fields(TourConstraintKind.DistanceBetween), JsonNode.Parse("""{"minNm":200,"maxNm":1500}""")!.AsObject()));
        Assert.Equal(
            ["LIRF", "× 3"],
            OpenCatalog.Describe(OpenCatalog.Fields(TourConstraintKind.MinFlightsAt), JsonNode.Parse("""{"airport":"LIRF","count":3}""")!.AsObject()));
        Assert.Empty(OpenCatalog.Describe(OpenCatalog.Fields(TourConstraintKind.Chained), []));
    }

    [Fact]
    public void OnlyMinFlightsAtRepeats() =>
        Assert.Equal(
            [TourConstraintKind.MinFlightsAt],
            Enum.GetValues<TourConstraintKind>().Where(OpenCatalog.Repeats));

    [Fact]
    public void AnOpenTourNeedsAGoalWithItsParameters()
    {
        var tour = Tour();
        Assert.Contains(new ShapeProblem("openGoal", "errors.required"), Problems(tour));

        tour.OpenGoal = OpenGoal.CollectList;
        tour.OpenGoalJson = """{"airports":["LIRF"],"count":3}""";
        Assert.Contains(new ShapeProblem("openGoalParameters.count", "flightops:errors.countAboveList"), Problems(tour));

        tour.OpenGoalJson = """{"airports":["LIRF","LIMC","LIPZ"],"count":3}""";
        Assert.Empty(Problems(tour));
    }

    [Fact]
    public void EastboundAndWestboundContradictEachOther()
    {
        var tour = Tour();
        tour.OpenGoal = OpenGoal.Distance;
        tour.OpenGoalJson = """{"nm":21600}""";

        Assert.Empty(Problems(tour, Constraint(TourConstraintKind.Chained), Constraint(TourConstraintKind.Eastbound)));
        Assert.Contains(
            new ShapeProblem("constraints", "flightops:errors.constraintsContradict"),
            Problems(tour, Constraint(TourConstraintKind.Eastbound), Constraint(TourConstraintKind.Westbound)));
    }

    [Fact]
    public void ConstraintsBelongToAnOpenTourOnly()
    {
        var tour = new Tour { Kind = TourKind.Free };
        var parts = new TourParts([], [], [], [], null) { Constraints = [Constraint(TourConstraintKind.Chained)] };

        Assert.Contains(
            new ShapeProblem("constraints", "flightops:errors.kindHasNoConstraints"),
            TourShape.Problems(tour, parts, new HashSet<string>()));
    }

    private static IReadOnlyList<ShapeProblem> Problems(Tour tour, params TourConstraint[] constraints) =>
        TourShape.Problems(tour, new TourParts([], [], [], [], null) { Constraints = constraints }, new HashSet<string>());

    private static Tour Tour() => new()
    {
        Kind = TourKind.Open,
        ReleaseAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        CloseAt = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
    };

    private static TourConstraint Constraint(TourConstraintKind kind) => new() { Kind = kind };
}
