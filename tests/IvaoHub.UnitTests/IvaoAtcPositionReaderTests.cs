using System.Text.Json;
using IvaoHub.Core.Ivao;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The ATC positions of IVAO (M3, A2), read against the two answers of the world as they were recorded on 25 September 2026
/// with <c>tools/record-ivao-fixtures.mjs --positions world</c>: the positions of the bench's airports and of Grottaglie,
/// which IVAO lists twice, in <c>atc-positions-world.json</c>, and the sectors of their FIRs in <c>subcenters-world.json</c>.
/// </summary>
public sealed class IvaoAtcPositionReaderTests
{
    private static IReadOnlyList<IvaoAtcPositionDto> Positions() =>
        IvaoAtcPositionReader.ReadAirportPositions(IvaoFixtures.Read("atc-positions-world.json").EnumerateArray());

    private static IReadOnlyList<IvaoAtcPositionDto> Sectors() =>
        IvaoAtcPositionReader.ReadSectors(IvaoFixtures.Read("subcenters-world.json").EnumerateArray());

    [Fact]
    public void AnAirportPositionIsItsCallsignItsKindItsAirportAndItsName()
    {
        var tower = Assert.Single(Positions(), position => position.Callsign == "LIRF_TWR");

        Assert.Equal("TWR", tower.PositionType);
        Assert.Equal("LIRF", tower.AirportIcao);
        Assert.Null(tower.CenterId);
        Assert.Equal("Fiume Tower", tower.Name);
    }

    [Fact]
    public void ASectorIsItsCallsignItsKindAndItsFir()
    {
        var sectors = Sectors();
        var sector = Assert.Single(sectors, position => position.Callsign == "LIRR_NE_CTR");

        Assert.Equal("CTR", sector.PositionType);
        Assert.Equal("LIRR", sector.CenterId);
        Assert.Null(sector.AirportIcao);
        Assert.Equal("Roma Radar", sector.Name);

        // The sectors are the only list with the centres' kinds in it: neither is ever an airport's.
        Assert.Contains(sectors, position => position.PositionType == "FSS");
        Assert.DoesNotContain(Positions(), position => position.PositionType is "CTR" or "FSS");
    }

    [Fact]
    public void AStationIvaoListsTwiceIsOnePositionTheFirstOne()
    {
        // Measured on 25 September 2026: four callsigns of the world come twice, the same airport, name and frequency under
        // two identifiers. Grottaglie's are in the file as IVAO sent them.
        var rows = IvaoFixtures.Read("atc-positions-world.json").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Count(row => row.GetProperty("composePosition").GetString() == "LIBG_APP"));

        var positions = IvaoAtcPositionReader.ReadAirportPositions(rows);

        var kept = Assert.Single(positions, position => position.Callsign == "LIBG_APP");
        using var raw = JsonDocument.Parse(kept.RawJson);
        Assert.Equal(3400, raw.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(positions.Count, positions.Select(position => position.Callsign).Distinct().Count());
    }

    [Fact]
    public void TheOutlineIsLeftOutAndTheRestOfTheRowIsKept()
    {
        // A row as IVAO sends it with mapType=regionMapPolygon. The recorded files have no outline, because the tool drops
        // it; this is the reader doing the same, so that the table keeps a row and not a map.
        using var answer = JsonDocument.Parse("""
            [
              {
                "id": 1174, "centerId": "LIRR", "atcCallsign": "Roma Radar", "military": false, "middleIdentifier": "NE",
                "position": "CTR", "composePosition": "LIRR_NE_CTR", "frequency": 124.2,
                "regionMapPolygon": [[41.9, 12.5], [42.1, 12.9], [41.7, 13.1]]
              }
            ]
            """);

        var sector = Assert.Single(IvaoAtcPositionReader.ReadSectors(answer.RootElement.EnumerateArray()));

        using var raw = JsonDocument.Parse(sector.RawJson);
        Assert.False(raw.RootElement.TryGetProperty("regionMapPolygon", out _));
        Assert.Equal(124.2, raw.RootElement.GetProperty("frequency").GetDouble());
        Assert.False(raw.RootElement.GetProperty("military").GetBoolean());
        Assert.Equal("NE", raw.RootElement.GetProperty("middleIdentifier").GetString());
    }

    [Fact]
    public void ARowThatIsNotAPositionOrDoesNotFitIsSkippedAndTheRestIsReadInUpperCase()
    {
        // One odd row of IVAO must not make the night's snapshot fail: it is left out, and so is one wider than the columns.
        using var answer = JsonDocument.Parse("""
            [
              { "airportId": "LIRF", "position": "TWR", "atcCallsign": "No callsign" },
              { "airportId": "LIRF", "composePosition": "LIRF_X_TWR", "atcCallsign": "No kind" },
              { "position": "TWR", "composePosition": "ZZZZ_TWR", "atcCallsign": "No airport" },
              { "airportId": "LIRF", "position": "TWR", "composePosition": "LIRF_A_CALLSIGN_FAR_TOO_WIDE_FOR_ITS_COLUMN_TWR" },
              { "airportId": "LIRFX", "position": "TWR", "composePosition": "LIRFX_TWR" },
              "not even an object",
              { "airportId": "lirf", "position": "twr", "composePosition": "lirf_z_twr", "atcCallsign": "  Fiume Zulu  " }
            ]
            """);

        var position = Assert.Single(IvaoAtcPositionReader.ReadAirportPositions(answer.RootElement.EnumerateArray()));

        Assert.Equal("LIRF_Z_TWR", position.Callsign);
        Assert.Equal("TWR", position.PositionType);
        Assert.Equal("LIRF", position.AirportIcao);
        Assert.Equal("Fiume Zulu", position.Name);
    }

    [Fact]
    public void ANameIvaoLeftEmptyIsAnEmptyNameAndNotAMissingPosition()
    {
        // Two positions of the world have neither a name nor a middle identifier (OIAD__APP, OIHS__APP): they are stations
        // all the same.
        using var answer = JsonDocument.Parse("""
            [{ "airportId": "OIAD", "atcCallsign": "", "middleIdentifier": "", "position": "APP", "composePosition": "OIAD__APP" }]
            """);

        var position = Assert.Single(IvaoAtcPositionReader.ReadAirportPositions(answer.RootElement.EnumerateArray()));

        Assert.Equal("OIAD__APP", position.Callsign);
        Assert.Equal(string.Empty, position.Name);
    }
}
