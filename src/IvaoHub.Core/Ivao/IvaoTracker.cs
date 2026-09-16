using System.Globalization;
using System.Text.Json;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// One connection of a member to the network, as the tracker of IVAO lists it. Measured against the
/// real API on 16 September 2026: the list answers
/// <c>{ items, totalItems, perPage, page, pages }</c>, and a row carries the flight plans it had in
/// a shortened form (<c>id</c>, <c>departureId</c>, <c>arrivalId</c>, <c>aircraftId</c>) — enough to
/// tell a pilot session from an ATC one without a second call.
/// <para>The raw payload travels with it: a field nobody reads today should not have to be guessed
/// at tomorrow, which is the same choice the centres and the airports made.</para>
/// </summary>
public sealed record IvaoTrackerSessionDto(
    long Id,
    int Vid,
    string Callsign,
    DateTime StartedAt,
    TimeSpan Duration,
    bool HasFlightPlan,
    string? DepartureIcao,
    string? ArrivalIcao,
    string? AircraftIcao,
    string RawJson)
{
    /// <summary>When the connection ended, as far as its declared length says.</summary>
    public DateTime EndedAt => StartedAt + Duration;
}

/// <summary>
/// One revision of the flight plan of a session. IVAO keeps them all and numbers them
/// (<c>revision</c>, from 1), which is why the hub asks for the lot and decides afterwards which
/// one was valid at take off (design M2 section 3.2).
/// <para><see cref="Equipment"/> and <see cref="Transponder"/> come expanded from IVAO as lists of
/// <c>{ id, name }</c>: the letters are flattened back here, so a check can read <c>SD</c> the way
/// the pilot filed it.</para>
/// </summary>
public sealed record IvaoFlightPlanDto(
    long Id,
    int Revision,
    DateTime FiledAt,
    string DepartureIcao,
    string ArrivalIcao,
    string? AlternateIcao,
    string? SecondAlternateIcao,
    string? AircraftIcao,
    string? WakeTurbulence,
    string Equipment,
    string Transponder,
    string FlightRules,
    string? FlightType,
    string? Level,
    string? Speed,
    string? Route,
    string? Remarks,
    TimeSpan? DepartureTime,
    TimeSpan? EstimatedEnroute,
    string RawJson);

/// <summary>
/// One point of the track of a session. Measured on real flights: the network samples every 15
/// seconds or so on a long flight (minimum 4, maximum 20 on the one measured), and the points of a
/// session survive about 90 days at IVAO.
/// </summary>
public sealed record IvaoTrackPointDto(
    DateTime At,
    double Latitude,
    double Longitude,
    int AltitudeFeet,
    int GroundSpeedKnots,
    int Heading,
    bool OnGround,
    string? State,
    string? Transponder);

/// <summary>
/// What the hub asks the tracker for: the sessions of one member in a window, optionally between
/// two airports. The window is the report window of the tour (design M2 section 3.2), never a
/// fishing trip across the network.
/// </summary>
public sealed record IvaoSessionQuery(
    int Vid,
    DateTime FromUtc,
    DateTime ToUtc,
    string? DepartureIcao = null,
    string? ArrivalIcao = null)
{
    /// <summary>How many rows a page carries; 50 is what the API was measured to accept.</summary>
    public const int PageSize = 50;

    /// <summary>
    /// At most this many sessions come back. A pilot reporting one leg picks from a short list, and
    /// a window wide enough to need more than two hundred rows is a mistake upstream, not a page to
    /// keep fetching.
    /// </summary>
    public const int MaxSessions = 200;

    public string ToQueryString()
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"userId={Vid}&from={Iso(FromUtc)}&to={Iso(ToUtc)}");

        if (!string.IsNullOrWhiteSpace(DepartureIcao))
        {
            query += $"&departureId={Uri.EscapeDataString(DepartureIcao.ToUpperInvariant())}";
        }

        if (!string.IsNullOrWhiteSpace(ArrivalIcao))
        {
            query += $"&arrivalId={Uri.EscapeDataString(ArrivalIcao.ToUpperInvariant())}";
        }

        return query;
    }

    private static string Iso(DateTime moment) =>
        Uri.EscapeDataString(DateTime.SpecifyKind(moment, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture));
}

/// <summary>
/// Turns the answers of the tracker into the records above. It lives on its own so that the real
/// client and the fixture client read the payload the same way: a fixture that parsed itself
/// differently from production would be a fixture that proves nothing.
/// </summary>
public static class IvaoTrackerReader
{
    /// <summary>The rows of a page, and how many pages there are in total.</summary>
    public static (IReadOnlyList<IvaoTrackerSessionDto> Sessions, int Pages) ReadSessions(JsonElement root)
    {
        var sessions = new List<IvaoTrackerSessionDto>();
        var items = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray()
            : root.TryGetProperty("items", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray()
                : Enumerable.Empty<JsonElement>();

        foreach (var item in items)
        {
            if (ReadSession(item) is { } session)
            {
                sessions.Add(session);
            }
        }

        var pages = Number(root, "pages") is { } value ? (int)value : 1;
        return (sessions, Math.Max(pages, 1));
    }

    public static IvaoTrackerSessionDto? ReadSession(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || Number(item, "id") is not { } id
            || Number(item, "userId") is not { } vid)
        {
            return null;
        }

        var plan = item.TryGetProperty("flightPlans", out var plans) && plans.ValueKind == JsonValueKind.Array
            ? plans.EnumerateArray().FirstOrDefault()
            : default;

        var hasPlan = plan.ValueKind == JsonValueKind.Object;

        return new IvaoTrackerSessionDto(
            (long)id,
            (int)vid,
            Text(item, "callsign") ?? string.Empty,
            Moment(item, "createdAt") ?? DateTime.UnixEpoch,
            TimeSpan.FromSeconds(Number(item, "time") ?? 0),
            hasPlan,
            hasPlan ? Text(plan, "departureId")?.ToUpperInvariant() : null,
            hasPlan ? Text(plan, "arrivalId")?.ToUpperInvariant() : null,
            hasPlan ? Text(plan, "aircraftId")?.ToUpperInvariant() : null,
            item.GetRawText());
    }

    /// <summary>Every revision IVAO kept, oldest first, so "the one at take off" is a choice made later.</summary>
    public static IReadOnlyList<IvaoFlightPlanDto> ReadFlightPlans(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. root.EnumerateArray()
            .Select(ReadFlightPlan)
            .OfType<IvaoFlightPlanDto>()
            .OrderBy(plan => plan.Revision)];
    }

    public static IvaoFlightPlanDto? ReadFlightPlan(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || Number(item, "id") is not { } id)
        {
            return null;
        }

        return new IvaoFlightPlanDto(
            (long)id,
            (int)(Number(item, "revision") ?? 1),
            Moment(item, "createdAt") ?? DateTime.UnixEpoch,
            Text(item, "departureId")?.ToUpperInvariant() ?? string.Empty,
            Text(item, "arrivalId")?.ToUpperInvariant() ?? string.Empty,
            Text(item, "alternativeId")?.ToUpperInvariant(),
            Text(item, "alternative2Id")?.ToUpperInvariant(),
            Text(item, "aircraftId")?.ToUpperInvariant(),
            item.TryGetProperty("aircraft", out var aircraft) ? Text(aircraft, "wakeTurbulence") : null,
            Letters(item, "aircraftEquipments"),
            Letters(item, "aircraftTransponderTypes"),
            Text(item, "flightRules")?.ToUpperInvariant() ?? string.Empty,
            Text(item, "flightType")?.ToUpperInvariant(),
            Text(item, "level"),
            Text(item, "speed"),
            Text(item, "route"),
            Text(item, "remarks"),
            Seconds(item, "departureTime"),
            Seconds(item, "eet"),
            item.GetRawText());
    }

    /// <summary>The points of a track, oldest first: every check that reads them reads them in time.</summary>
    public static IReadOnlyList<IvaoTrackPointDto> ReadTracks(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. root.EnumerateArray()
            .Select(ReadTrackPoint)
            .OfType<IvaoTrackPointDto>()
            .OrderBy(point => point.At)];
    }

    public static IvaoTrackPointDto? ReadTrackPoint(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || (Moment(item, "timestamp") ?? Moment(item, "time")) is not { } at
            || Number(item, "latitude") is not { } latitude
            || Number(item, "longitude") is not { } longitude)
        {
            return null;
        }

        return new IvaoTrackPointDto(
            at,
            latitude,
            longitude,
            (int)(Number(item, "altitude") ?? 0),
            (int)(Number(item, "groundSpeed") ?? 0),
            (int)(Number(item, "heading") ?? 0),
            item.TryGetProperty("onGround", out var ground) && ground.ValueKind == JsonValueKind.True,
            Text(item, "state"),
            Text(item, "transponder"));
    }

    private static string Letters(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        // IVAO sends them expanded and ordered ({ id: "S", name: "Standard…", order: 1 }); the
        // letters are what a flight plan is written with, so they go back to being letters here.
        return string.Concat(list.EnumerateArray()
            .OrderBy(entry => Number(entry, "order") ?? 0)
            .Select(entry => Text(entry, "id"))
            .Where(letter => !string.IsNullOrEmpty(letter)));
    }

    private static string? Text(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static double? Number(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;

    private static TimeSpan? Seconds(JsonElement element, string property) =>
        Number(element, property) is { } seconds ? TimeSpan.FromSeconds(seconds) : null;

    private static DateTime? Moment(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String when DateTime.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed) => parsed,
            JsonValueKind.Number when value.TryGetInt64(out var unix) =>
                DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime,
            _ => null,
        };
    }
}
