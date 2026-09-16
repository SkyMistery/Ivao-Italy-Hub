using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Airspace;

/// <summary>
/// Where the outlines of the flight information regions come from. One implementation, one place
/// that names the provider: a module asks <see cref="IFirLocator"/> and never learns who published
/// the polygons (plan section 4.2, decision note of 16 September 2026).
/// </summary>
public interface IFirBoundarySource
{
    /// <summary>
    /// The boundaries, or an empty list when they could not be fetched — in which case the caller
    /// keeps what it already has rather than emptying the table.
    /// </summary>
    Task<IReadOnlyList<FirBoundary>> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The VATSpy data project: a GeoJSON of the boundaries the community maintains, 1121 features of
/// the whole world, measured on 16 September 2026 (2.0 MB).
/// <para><b>Licence CC BY-SA 4.0.</b> The file is fetched and read, never committed and never served
/// on: what the hub publishes is an answer derived from it ("this point is in LIRR"), with the
/// attribution the licence asks for shown where that answer is shown.</para>
/// <para><b>Only whole regions.</b> The dataset also carries the sectors a region is divided into
/// (<c>LIRR-NE</c>, <c>LIMM-ES5</c>): those belong to another network's rostering, change often, and
/// answer a question nobody here asks, so an identifier with a dash in it is skipped.</para>
/// </summary>
public sealed class VatSpyFirBoundarySource(HttpClient http, ILogger<VatSpyFirBoundarySource> logger)
    : IFirBoundarySource
{
    /// <summary>What to say next to a derived answer, as the licence requires.</summary>
    public const string Attribution = "FIR boundaries: VATSpy Data Project, CC BY-SA 4.0";

    private const string FileUrl =
        "https://raw.githubusercontent.com/vatsimnetwork/vatspy-data-project/master/Boundaries.geojson";

    public async Task<IReadOnlyList<FirBoundary>> GetAsync(CancellationToken cancellationToken = default)
    {
        JsonDocument document;
        try
        {
            using var response = await http.GetAsync(new Uri(FileUrl), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("The FIR boundaries answered {Status}.", (int)response.StatusCode);
                return [];
            }

            document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning(exception, "The FIR boundaries could not be fetched; keeping what we have.");
            return [];
        }

        using (document)
        {
            return Read(document.RootElement);
        }
    }

    /// <summary>Turns the GeoJSON into rows. Public so that a test can read a file with it.</summary>
    public static IReadOnlyList<FirBoundary> Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("features", out var features)
            || features.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var boundaries = new List<FirBoundary>();
        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var properties)
                || !feature.TryGetProperty("geometry", out var geometry)
                || properties.TryGetProperty("id", out var idValue) is false
                || idValue.GetString() is not { Length: > 0 } id)
            {
                continue;
            }

            // A sector of a region, not a region: skipped on purpose.
            if (id.Contains('-', StringComparison.Ordinal))
            {
                continue;
            }

            if (Box(geometry) is not { } box)
            {
                continue;
            }

            boundaries.Add(new FirBoundary
            {
                Id = id.ToUpperInvariant(),
                IsOceanic = Flag(properties, "oceanic"),
                Region = Text(properties, "region"),
                GeometryJson = geometry.GetRawText(),
                MinLatitude = box.MinLat,
                MaxLatitude = box.MaxLat,
                MinLongitude = box.MinLon,
                MaxLongitude = box.MaxLon,
            });
        }

        return boundaries;
    }

    /// <summary>The rectangle that contains the shape, computed once so that reading is cheap.</summary>
    private static (double MinLat, double MaxLat, double MinLon, double MaxLon)? Box(JsonElement geometry)
    {
        double minLat = 90, maxLat = -90, minLon = 180, maxLon = -180;
        var found = false;

        foreach (var (longitude, latitude) in GeoJson.Points(geometry))
        {
            found = true;
            minLat = Math.Min(minLat, latitude);
            maxLat = Math.Max(maxLat, latitude);
            minLon = Math.Min(minLon, longitude);
            maxLon = Math.Max(maxLon, longitude);
        }

        return found ? (minLat, maxLat, minLon, maxLon) : null;
    }

    private static bool Flag(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => value.GetString() is "1" or "true",
            JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
            _ => false,
        };

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() is { Length: > 0 } text ? text : null
            : null;
}

/// <summary>Reading positions out of a GeoJSON geometry, whatever depth it nests them at.</summary>
internal static class GeoJson
{
    /// <summary>Every (longitude, latitude) pair of a Polygon or a MultiPolygon.</summary>
    public static IEnumerable<(double Longitude, double Latitude)> Points(JsonElement geometry)
    {
        if (geometry.ValueKind != JsonValueKind.Object
            || !geometry.TryGetProperty("coordinates", out var coordinates))
        {
            yield break;
        }

        foreach (var point in Walk(coordinates))
        {
            yield return point;
        }
    }

    /// <summary>Every ring of the geometry, each as its own list of positions.</summary>
    public static IEnumerable<IReadOnlyList<(double Longitude, double Latitude)>> Rings(JsonElement geometry)
    {
        if (geometry.ValueKind != JsonValueKind.Object
            || !geometry.TryGetProperty("coordinates", out var coordinates))
        {
            yield break;
        }

        foreach (var ring in WalkRings(coordinates))
        {
            yield return ring;
        }
    }

    private static IEnumerable<(double Longitude, double Latitude)> Walk(JsonElement element)
    {
        if (IsPosition(element, out var position))
        {
            yield return position;
            yield break;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var inner in element.EnumerateArray())
        {
            foreach (var point in Walk(inner))
            {
                yield return point;
            }
        }
    }

    private static IEnumerable<IReadOnlyList<(double Longitude, double Latitude)>> WalkRings(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        // A ring is an array whose first element is a position.
        var first = element.EnumerateArray().FirstOrDefault();
        if (IsPosition(first, out _))
        {
            yield return [.. Walk(element)];
            yield break;
        }

        foreach (var inner in element.EnumerateArray())
        {
            foreach (var ring in WalkRings(inner))
            {
                yield return ring;
            }
        }
    }

    private static bool IsPosition(JsonElement element, out (double Longitude, double Latitude) position)
    {
        position = default;
        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var values = new List<double>(2);
        foreach (var value in element.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                values.Add(number);
                continue;
            }

            // Some feeds write the numbers as strings; the shape is what matters.
            if (value.ValueKind == JsonValueKind.String
                && double.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var parsed))
            {
                values.Add(parsed);
                continue;
            }

            return false;
        }

        if (values.Count < 2)
        {
            return false;
        }

        position = (values[0], values[1]);
        return true;
    }
}
