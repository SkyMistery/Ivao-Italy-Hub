using IvaoHub.Core.Airspace;
using Xunit;
using ToursCircle = IvaoHub.Modules.FlightOps.Legs.GreatCircle;
using ToursPoint = IvaoHub.Modules.FlightOps.Legs.GeoPoint;

namespace IvaoHub.UnitTests;

/// <summary>
/// The distance between two airports in the core (E10e, note 2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo): the
/// questions of the tours' tests (<see cref="LegTests"/>) asked of the core, with the same answers; and the core and the
/// tours' own copy asked the same questions, answer for answer, so that moving the tours onto the core changes no
/// distance a leg or a report already has.
/// </summary>
public sealed class GreatCircleTests
{
    // The airports of the tours' tests, with their real coordinates (the integration tests of the tours give the first
    // three to their XFA airports). The expected values were computed there with an independent implementation.
    private static readonly GeoPoint Lirf = new(41.8002777778, 12.2388888889);
    private static readonly GeoPoint Liml = new(45.4451, 9.27674);
    private static readonly GeoPoint Egll = new(51.470748, -0.459909);
    private static readonly GeoPoint Lime = new(45.6739, 9.7042);
    private static readonly GeoPoint Lflj = new(45.396999, 6.63472);
    private static readonly GeoPoint Kjfk = new(40.639801, -73.7789);
    private static readonly GeoPoint Lira = new(41.9519, 12.4989);

    [Fact]
    public void TheDistanceBetweenTwoKnownAirportsIsRightOnThreeOrdersOfMagnitude()
    {
        Assert.Equal(253.9, GreatCircle.DistanceNm(Lirf, Liml), 1);
        Assert.Equal(130.1, GreatCircle.DistanceNm(Lime, Lflj), 1);
        Assert.Equal(779.6, GreatCircle.DistanceNm(Lirf, Egll), 1);
        Assert.Equal(3707.2, GreatCircle.DistanceNm(Lirf, Kjfk), 1);
        Assert.Equal(10807.3, GreatCircle.DistanceNm(new GeoPoint(90, 0), new GeoPoint(-90, 0)), 1);

        // The historical definition of the nautical mile: sixty per degree on the equator.
        Assert.Equal(60.0, GreatCircle.DistanceNm(new GeoPoint(0, 0), new GeoPoint(0, 1)), 0);
    }

    [Fact]
    public void TheDistanceHasNoDirectionNoNaNAndATenthOfAMile()
    {
        Assert.Equal(GreatCircle.DistanceNm(Lirf, Kjfk), GreatCircle.DistanceNm(Kjfk, Lirf), 6);

        // With the naive formula on the cosine this is NaN: the argument of the arccosine goes past 1.
        Assert.Equal(0, GreatCircle.DistanceNm(Lirf, Lirf));
        Assert.Equal(0m, GreatCircle.DistanceNmRounded(Egll, Egll));

        // Fiumicino and Urbe are some fifteen miles apart: where a formula that loses digits shows it.
        Assert.InRange(GreatCircle.DistanceNm(Lirf, Lira), 12, 20);

        var rounded = GreatCircle.DistanceNmRounded(Lirf, Liml);
        Assert.Equal(253.9m, rounded);
        Assert.Equal(1, rounded.Scale);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(8.0)]
    [InlineData(12.0)]
    [InlineData(34.0)]
    public void TwoAntipodesAreHalfTheCircumference(double latitude)
    {
        // At 8, 12 and 34 degrees rounding carries h one unit in the last place past 1 (measured on .NET 10, Windows):
        // still half the circumference, never NaN.
        Assert.Equal(10807.3, GreatCircle.DistanceNm(new GeoPoint(latitude, 0), new GeoPoint(-latitude, 180)), 1);
        Assert.Equal(10807.3, GreatCircle.DistanceNm(new GeoPoint(latitude, 12), new GeoPoint(-latitude, -168)), 1);
    }

    [Fact]
    public void AcrossTheAntimeridianADegreeIsADegree()
    {
        // The degree it is anywhere on the equator, not the whole way round.
        Assert.Equal(
            GreatCircle.DistanceNm(new GeoPoint(0, 0), new GeoPoint(0, 1)),
            GreatCircle.DistanceNm(new GeoPoint(0, 179.5), new GeoPoint(0, -179.5)),
            6);
    }

    /// <summary>
    /// The pairs the tours' tests measure, the edges above and a grid of the whole globe, every point against every
    /// other: the core and the tours' copy give the same double and the same tenth, to the last bit. When a session of
    /// the maintainer moves the tours onto the core, this test goes away with their copy.
    /// </summary>
    [Fact]
    public void TheCoreAndTheToursGiveTheSameAnswers()
    {
        GeoPoint[] airports = [Lirf, Liml, Egll, Lime, Lflj, Kjfk, Lira];
        GeoPoint[] edges =
        [
            new(90, 0), new(-90, 0), new(0, 0), new(0, 1), new(0, 180), new(8, 0), new(-8, 180), new(12, 12), new(-12, -168),
            new(34, 0), new(-34, 180), new(0, 179.5), new(0, -179.5),
        ];
        var grid =
            from latitude in Enumerable.Range(-6, 13)
            from longitude in Enumerable.Range(-6, 13)
            select new GeoPoint(latitude * 15, longitude * 30);
        GeoPoint[] points = [.. airports, .. edges, .. grid];

        var different = (
            from departure in points
            from arrival in points
            where ToursCircle.DistanceNm(Tours(departure), Tours(arrival)) != GreatCircle.DistanceNm(departure, arrival)
                || ToursCircle.DistanceNmRounded(Tours(departure), Tours(arrival)) != GreatCircle.DistanceNmRounded(departure, arrival)
            select $"{departure} to {arrival}").ToList();

        Assert.Equal(7 + 13 + (13 * 13), points.Length);
        Assert.Empty(different);
    }

    private static ToursPoint Tours(GeoPoint point) => new(point.Latitude, point.Longitude);
}
