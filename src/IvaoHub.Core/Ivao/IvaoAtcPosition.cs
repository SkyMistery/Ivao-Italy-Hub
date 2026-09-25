using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// One place on the network a controller connects to, snapshot of the two lists IVAO keeps of them: the positions of the
/// airports (<c>/v2/ATCPositions/all</c>: <c>LIRF_TWR</c>, <c>LIRF_AWL_APP</c>) and the sectors of the FIRs
/// (<c>/v2/subcenters/all</c>: <c>LIRR_NE_CTR</c>). Measured on 25 September 2026: 11 863 and 1 497 of them in the world,
/// 195 and 37 in Italy (M3, A2, decision note of that day).
/// <para>The <b>world</b>, like the airports since T1: the synchronisation copies IVAO, and what belongs to the division
/// is <see cref="IAtcPositionDirectory"/>'s to say, as the airspace is <see cref="IFirDirectory"/>'s.</para>
/// </summary>
public sealed class IvaoAtcPosition
{
    /// <summary>Widest callsign the column holds; the longest IVAO had was fourteen characters.</summary>
    public const int MaxCallsignLength = 32;

    /// <summary>Widest kind of position; IVAO's are three or four letters.</summary>
    public const int MaxPositionTypeLength = 16;

    /// <summary>As wide as <c>ref_ivao_airports.icao</c>, the column an airport position is joined on.</summary>
    public const int MaxAirportLength = 4;

    /// <summary>As wide as <c>ref_ivao_centers.id</c>: a sector's FIR is one of those.</summary>
    public const int MaxCenterLength = 8;

    /// <summary>Widest name; the longest IVAO had was fifty seven characters.</summary>
    public const int MaxNameLength = 256;

    /// <summary>
    /// What a controller connects as — IVAO's <c>composePosition</c>, upper case — and the key: it is what a training
    /// writes down (design M3 §1.2), where IVAO's own identifiers are two sequences whose numbers overlap.
    /// </summary>
    public string Callsign { get; set; } = string.Empty;

    /// <summary>
    /// IVAO's kind of position: <c>DEL</c>, <c>GND</c>, <c>TWR</c>, <c>APP</c>, <c>DEP</c>, <c>ATIS</c> at an airport,
    /// <c>CTR</c> and <c>FSS</c> in a FIR. The word <see cref="Rating.PositionType"/> names.
    /// </summary>
    public string PositionType { get; set; } = string.Empty;

    /// <summary>The airport of an airport position; null for a sector. A plain column, never a foreign key.</summary>
    public string? AirportIcao { get; set; }

    /// <summary>The FIR of a sector; null for an airport position, whose FIR is its airport's.</summary>
    public string? CenterId { get; set; }

    /// <summary>What is said on the frequency: <c>Fiume Tower</c>, <c>Roma Radar</c>. IVAO leaves a few of them empty.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The row as IVAO sent it, without the outline of the sector, which is most of its weight.</summary>
    public string RawJson { get; set; } = "{}";

    public DateTime SyncedAt { get; set; }
}

/// <summary>One position as the client reads it from IVAO: <see cref="IvaoAtcPosition"/> before it is a row.</summary>
public sealed record IvaoAtcPositionDto(
    string Callsign,
    string PositionType,
    string? AirportIcao,
    string? CenterId,
    string Name,
    string RawJson);

/// <summary>
/// Turns IVAO's two answers into <see cref="IvaoAtcPositionDto"/>s. It lives on its own, like the reader of the tracker,
/// so that the real client and the fixture client read the payload the same way.
/// </summary>
public static class IvaoAtcPositionReader
{
    /// <summary>The outlines IVAO may send with a row: the snapshot keeps neither.</summary>
    private static readonly string[] Outlines = ["regionMap", "regionMapPolygon"];

    /// <summary>The raw row is stored, not shown: it stays as readable as IVAO wrote it.</summary>
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The positions of the airports, from the rows of <c>/v2/ATCPositions/all</c>.</summary>
    public static IReadOnlyList<IvaoAtcPositionDto> ReadAirportPositions(IEnumerable<JsonElement> rows) =>
        Read(rows, sectors: false);

    /// <summary>The sectors of the FIRs, from the rows of <c>/v2/subcenters/all</c>.</summary>
    public static IReadOnlyList<IvaoAtcPositionDto> ReadSectors(IEnumerable<JsonElement> rows) => Read(rows, sectors: true);

    private static List<IvaoAtcPositionDto> Read(IEnumerable<JsonElement> rows, bool sectors)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var positions = new List<IvaoAtcPositionDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in rows)
        {
            var callsign = Text(item, "composePosition")?.ToUpperInvariant();
            var type = Text(item, "position")?.ToUpperInvariant();
            var place = Text(item, sectors ? "centerId" : "airportId")?.ToUpperInvariant();
            var name = Text(item, "atcCallsign") ?? string.Empty;

            // A row without what makes it a position is skipped, and so is one wider than the columns: one odd row of
            // IVAO must not make the night's snapshot fail. IVAO also lists a few stations twice — LIBG_APP and
            // LIRE_APP among them, the same airport, name and frequency under two identifiers — and the first is kept.
            if (callsign is not { Length: <= IvaoAtcPosition.MaxCallsignLength }
                || type is not { Length: <= IvaoAtcPosition.MaxPositionTypeLength }
                || place is null
                || place.Length > (sectors ? IvaoAtcPosition.MaxCenterLength : IvaoAtcPosition.MaxAirportLength)
                || name.Length > IvaoAtcPosition.MaxNameLength
                || !seen.Add(callsign))
            {
                continue;
            }

            positions.Add(new IvaoAtcPositionDto(
                callsign,
                type,
                sectors ? null : place,
                sectors ? place : null,
                name,
                WithoutOutline(item)));
        }

        return positions;
    }

    /// <summary>The row with its outlines left out, the rest of it untouched.</summary>
    private static string WithoutOutline(JsonElement item)
    {
        if (!Outlines.Any(outline => item.TryGetProperty(outline, out _)))
        {
            return item.GetRawText();
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var property in item.EnumerateObject().Where(property => !Outlines.Contains(property.Name)))
            {
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string? Text(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
