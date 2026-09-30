using IvaoHub.Core.Airspace;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The distance between two airports in the core (E10e, note 2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo): the
/// questions the tours' tests asked of their own copy, asked of the core with the same answers. The tours measure here
/// since their copy went away (note 2026-09-30-i-tour-sulla-distanza-del-nucleo), and this is the one place the
/// distance is tested.
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
}
