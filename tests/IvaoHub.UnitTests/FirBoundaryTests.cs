using System.Globalization;
using System.Text.Json;
using IvaoHub.Core.Airspace;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The outlines of the flight information regions: what is read from the source, and how a point is
/// placed inside one. The shapes here are invented rectangles around known places — the real dataset
/// is fetched at run time and deliberately not committed (decision note of 16 September 2026), and a
/// rectangle proves the arithmetic just as well as a coastline would.
/// </summary>
public sealed class FirBoundaryTests
{
    /// <summary>
    /// A box, as GeoJSON writes it: longitude first, and the ring closed.
    /// <para>Every number goes through the invariant culture on purpose. The hub runs with the
    /// culture of its division (<c>InvariantGlobalization</c> is off), and on an Italian machine
    /// string interpolation writes 36,5 — which is not a number in JSON, and which made this test
    /// fail the first time it ran.</para>
    /// </summary>
    private static string Polygon(double minLon, double minLat, double maxLon, double maxLat)
    {
        static string N(double value) => value.ToString(CultureInfo.InvariantCulture);

        return $$"""
            {"type":"Polygon","coordinates":[[[{{N(minLon)}},{{N(minLat)}}],[{{N(maxLon)}},{{N(minLat)}}],[{{N(maxLon)}},{{N(maxLat)}}],[{{N(minLon)}},{{N(maxLat)}}],[{{N(minLon)}},{{N(minLat)}}]]]}
            """;
    }

    /// <summary>One more property of a feature, written the way the source writes it.</summary>
    private static string Extra(string name, string value) => ",\"" + name + "\":\"" + value + "\"";

    private static string Feature(string id, string geometry, string extra = "") =>
        $$"""{"type":"Feature","properties":{"id":"{{id}}"{{extra}}},"geometry":{{geometry}}}""";

    private static JsonElement Collection(params string[] features)
    {
        using var document = JsonDocument.Parse(
            $$"""{"type":"FeatureCollection","features":[{{string.Join(',', features)}}]}""");

        return document.RootElement.Clone();
    }

    [Fact]
    public void EveryRegionIsReadWithTheBoxThatContainsIt()
    {
        var boundaries = VatSpyFirBoundarySource.Read(Collection(
            Feature("LIRR", Polygon(8, 36.5, 19, 43.7), Extra("oceanic", "0") + Extra("region", "EMEA")),
            Feature("LIMM", Polygon(6.6, 43.2, 14.5, 47.1))));

        Assert.Equal(["LIRR", "LIMM"], boundaries.Select(boundary => boundary.Id));

        var rome = boundaries[0];
        Assert.False(rome.IsOceanic);
        Assert.Equal("EMEA", rome.Region);
        Assert.Equal(36.5, rome.MinLatitude);
        Assert.Equal(43.7, rome.MaxLatitude);
        Assert.Equal(8, rome.MinLongitude);
        Assert.Equal(19, rome.MaxLongitude);
        Assert.Contains("\"Polygon\"", rome.GeometryJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSectorsOfARegionAreSkipped()
    {
        // The dataset also carries how another network splits a region into sectors. They answer a
        // question nobody here asks, and they would make "which FIR is this" return five answers.
        var boundaries = VatSpyFirBoundarySource.Read(Collection(
            Feature("LIRR", Polygon(8, 36.5, 19, 43.7)),
            Feature("LIRR-NE", Polygon(12, 42, 14, 43)),
            Feature("LIMM-ES5", Polygon(9, 45, 10, 46))));

        Assert.Equal(["LIRR"], boundaries.Select(boundary => boundary.Id));
    }

    [Fact]
    public void AnOceanicRegionSaysSo()
    {
        var boundaries = VatSpyFirBoundarySource.Read(Collection(
            Feature("KZAK", Polygon(-180, 0, -120, 50), Extra("oceanic", "1"))));

        Assert.True(Assert.Single(boundaries).IsOceanic);
    }

    [Fact]
    public void AFeatureWithoutAnIdOrAShapeIsSkippedRatherThanThrownOver()
    {
        var boundaries = VatSpyFirBoundarySource.Read(Collection(
            """{"type":"Feature","properties":{},"geometry":{"type":"Polygon","coordinates":[]}}""",
            """{"type":"Feature","properties":{"id":"EMPTY"},"geometry":{"type":"Polygon","coordinates":[]}}""",
            Feature("LIRR", Polygon(8, 36.5, 19, 43.7))));

        Assert.Equal(["LIRR"], boundaries.Select(boundary => boundary.Id));
    }

    [Fact]
    public void APointIsInsideItsRegionAndOutsideTheOthers()
    {
        var rome = Shape("LIRR", Polygon(8, 36.5, 19, 43.7));
        var milan = Shape("LIMM", Polygon(6.6, 43.2, 14.5, 47.1));

        // Fiumicino.
        Assert.True(rome.Contains(41.8, 12.24));
        Assert.False(milan.Contains(41.8, 12.24));

        // Malpensa.
        Assert.True(milan.Contains(45.63, 8.73));
        Assert.False(rome.Contains(45.63, 8.73));

        // Paris is in neither.
        Assert.False(rome.Contains(49.01, 2.55));
        Assert.False(milan.Contains(49.01, 2.55));
    }

    [Fact]
    public void RegionsThatOverlapBothContainThePoint()
    {
        // An oceanic region over a continental one: a point at sea is legitimately in both, which is
        // why the question answers a list and not a single name.
        var continental = Shape("LPPC", Polygon(-10, 36, -6, 42));
        var oceanic = Shape("LPPO", Polygon(-30, 30, -5, 45));

        Assert.True(continental.Contains(38.7, -9.1));
        Assert.True(oceanic.Contains(38.7, -9.1));
    }

    [Fact]
    public void AMultiPolygonKeepsEveryOneOfItsPieces()
    {
        var geometry = $$"""
            {"type":"MultiPolygon","coordinates":[
              [[[8,36.5],[12,36.5],[12,40],[8,40],[8,36.5]]],
              [[[14,40],[19,40],[19,43.7],[14,43.7],[14,40]]]
            ]}
            """;

        var shape = Shape("LIRR", geometry);

        Assert.Equal(2, shape.RingCount);
        Assert.True(shape.Contains(38, 10));   // first piece
        Assert.True(shape.Contains(42, 16));   // second piece
        Assert.False(shape.Contains(42, 13));  // the gap between them
    }

    [Fact]
    public void TheBoxThrowsAwayWhatCannotPossiblyContainThePoint()
    {
        var shape = Shape("LIRR", Polygon(8, 36.5, 19, 43.7));

        Assert.False(shape.Contains(70, 12));    // far north
        Assert.False(shape.Contains(41.8, 120)); // far east
    }

    private static FirShape Shape(string id, string geometry)
    {
        var boundary = Assert.Single(VatSpyFirBoundarySource.Read(Collection(Feature(id, geometry))));
        return FirShape.From(boundary);
    }
}
