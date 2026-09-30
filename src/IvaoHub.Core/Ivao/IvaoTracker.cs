using System.Globalization;
using System.Text.Json;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// How a session was connected to the network. The tracker takes and gives four words, in capitals, and refuses any other
/// with a 400 that lists them (measured on 30 September 2026, M4 E10a): <c>PILOT</c>, <c>ATC</c>, <c>OBS</c>, and
/// <c>FOLME</c>, the follow-me car.
/// </summary>
public enum IvaoConnectionType
{
    Pilot,
    Atc,
    Observer,
    FollowMe,
}

/// <summary>
/// One connection of a member to the network, as the tracker of IVAO lists it. Measured against the
/// real API on 16 September 2026: the list answers
/// <c>{ items, totalItems, perPage, page, pages }</c>, and a row carries the flight plans it had in
/// a shortened form (<c>id</c>, <c>departureId</c>, <c>arrivalId</c>, <c>aircraftId</c>) — enough to
/// tell a pilot session from an ATC one without a second call.
/// <para>The raw payload travels with it: a field nobody reads today should not have to be guessed
/// at tomorrow, which is the same choice the centres and the airports made.</para>
/// <para>⚠️ The airports here are the first revision's. A search for an airport finds a session whose plan had it in any
/// revision (measured on 30 September 2026, M4 E10a), so a session found for its departure from one airport can say another
/// one here: the revisions are in <see cref="RawJson"/>.</para>
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

    /// <summary>
    /// A pilot, a controller, an observer or a follow-me car (M4, E10a). Null when the row does not say, as for a session
    /// a test builds by hand.
    /// </summary>
    public IvaoConnectionType? ConnectionType { get; init; }
}

/// <summary>
/// One revision of the flight plan of a session. IVAO keeps them all and numbers them
/// (<c>revision</c>, from 1), which is why the hub asks for the lot and decides afterwards which
/// one was valid at take off (design M2 section 3.2).
/// <para><see cref="Equipment"/> and <see cref="Transponder"/> come expanded from IVAO as lists of
/// <c>{ id, name }</c>: the letters are flattened back here, so a check can read <c>SD</c> the way
/// the pilot filed it.</para>
/// <para><see cref="FiledAt"/> is when the revision was filed: IVAO's <c>updatedAt</c>, not its <c>createdAt</c>.</para>
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
/// What the hub asks the tracker for: the sessions that started in a window — of one member, for a pilot reporting a leg
/// in the report window of the tour (design M2 section 3.2), or, without a VID, at an airport, for the events that count
/// who flew (design M4 section 5.1, E10a) — optionally between two airports, and of one kind of connection.
/// <para>What each part means is IVAO's, measured on 30 September 2026 (E10a): the window holds the sessions that
/// <i>started</i> in it, both ends included, so a caller who wants who was connected widens it backwards; an airport asked
/// alone is found in any revision of a flight plan, and two airports asked together in one same revision.</para>
/// </summary>
public sealed record IvaoSessionQuery(
    int? Vid,
    DateTime FromUtc,
    DateTime ToUtc,
    string? DepartureIcao = null,
    string? ArrivalIcao = null,
    IvaoConnectionType? ConnectionType = null)
{
    /// <summary>
    /// How many rows a page carries: the most the tracker gives, since a hundred and one are refused ("Should be lower than
    /// 100"). Measured on 30 September 2026 (E10a); until then it was 50, which T2 had measured the API to accept.
    /// </summary>
    public const int PageSize = 100;

    /// <summary>
    /// How many sessions a search reads when its caller does not say: two hundred, plenty for a pilot picking the one
    /// flight they are reporting.
    /// </summary>
    public const int DefaultLimit = 200;

    private readonly int _limit = DefaultLimit;

    /// <summary>
    /// The most sessions the caller will read, newest first: the pages stop there. It is the caller's to declare, because
    /// an airport on the evening of an event holds more than a pilot's list, and a job reads what one run can carry. An
    /// answer with fewer sessions than the limit is all there is; one with exactly as many may have been cut, and what is
    /// left is in the part of the window before the oldest of them.
    /// </summary>
    public int Limit
    {
        get => _limit;
        init => _limit = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Limit), value, "A search reads at least one session.");
    }

    /// <summary>The rows a page is asked for: a full page, or the limit when that is smaller.</summary>
    public int PerPage => Math.Min(PageSize, Limit);

    public string ToQueryString()
    {
        var window = $"from={Iso(FromUtc)}&to={Iso(ToUtc)}";
        var query = Vid is { } vid
            ? string.Create(CultureInfo.InvariantCulture, $"userId={vid}&{window}")
            : window;

        if (!string.IsNullOrWhiteSpace(DepartureIcao))
        {
            query += $"&departureId={Uri.EscapeDataString(DepartureIcao.ToUpperInvariant())}";
        }

        if (!string.IsNullOrWhiteSpace(ArrivalIcao))
        {
            query += $"&arrivalId={Uri.EscapeDataString(ArrivalIcao.ToUpperInvariant())}";
        }

        if (ConnectionType is { } type)
        {
            query += $"&connectionType={IvaoTrackerReader.Word(type)}";
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
    /// <summary>The words of the tracker for the kinds of connection (<see cref="IvaoConnectionType"/>).</summary>
    private static readonly (IvaoConnectionType Type, string Word)[] ConnectionWords =
    [
        (IvaoConnectionType.Pilot, "PILOT"),
        (IvaoConnectionType.Atc, "ATC"),
        (IvaoConnectionType.Observer, "OBS"),
        (IvaoConnectionType.FollowMe, "FOLME"),
    ];

    /// <summary>
    /// Every session a search asks for, page after page as the tracker hands them out: newest first, until the last page,
    /// a page with nothing on it, or the limit of the search (M4, E10a). <paramref name="page"/> gives the payload of one
    /// page, counted from one — the real client asks IVAO for it, a test hands out recorded ones — or null when it could
    /// not be had, and then the whole answer is null: half a list would read as "not there". The same session on two pages
    /// is counted once, which is what happens when a new one arrives while the pages are read and pushes the rest down.
    /// <para>Besides the sessions, how many the tracker says there are in all: more than were read when the limit cut
    /// the answer.</para>
    /// </summary>
    public static async Task<(IReadOnlyList<IvaoTrackerSessionDto> Sessions, int Total)?> ReadPagesAsync(
        IvaoSessionQuery query,
        Func<int, CancellationToken, Task<JsonElement?>> page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        var sessions = new List<IvaoTrackerSessionDto>();
        var seen = new HashSet<long>();
        int? total = null;
        for (var number = 1; ; number++)
        {
            if (await page(number, cancellationToken) is not { } root)
            {
                return null;
            }

            var (rows, pages) = ReadSessions(root);
            sessions.AddRange(rows.Where(row => seen.Add(row.Id)));
            total = Number(root, "totalItems") is { } items ? (int)items : total;

            if (number >= pages || rows.Count == 0 || sessions.Count >= query.Limit)
            {
                break;
            }
        }

        return (sessions.Count > query.Limit ? sessions[..query.Limit] : sessions, Math.Max(total ?? 0, sessions.Count));
    }

    /// <summary>
    /// Whether the tracker lists this row for this search: its rule, as measured on 30 September 2026 (M4, E10a). The
    /// member, when one is asked; a start inside the window, both ends included; the kind of connection; and the airports
    /// in a revision of the flight plan — any revision for one airport, one same revision for two. It is for the fixture
    /// client, which answers from recorded rows what IVAO would have answered.
    /// </summary>
    public static bool Answers(JsonElement row, IvaoSessionQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (ReadSession(row) is not { } session
            || (query.Vid is { } vid && session.Vid != vid)
            || session.StartedAt < query.FromUtc
            || session.StartedAt > query.ToUtc
            || (query.ConnectionType is { } type && session.ConnectionType != type))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.DepartureIcao) && string.IsNullOrWhiteSpace(query.ArrivalIcao))
        {
            return true;
        }

        return row.TryGetProperty("flightPlans", out var plans)
            && plans.ValueKind == JsonValueKind.Array
            && plans.EnumerateArray().Any(plan =>
                Same(Text(plan, "departureId"), query.DepartureIcao) && Same(Text(plan, "arrivalId"), query.ArrivalIcao));

        static bool Same(string? airport, string? asked) =>
            string.IsNullOrWhiteSpace(asked) || string.Equals(airport, asked.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The tracker's word for a kind of connection, as a search asks for it.</summary>
    internal static string Word(IvaoConnectionType type) => ConnectionWords.First(entry => entry.Type == type).Word;

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
            item.GetRawText())
        {
            ConnectionType = ConnectionTypeOf(Text(item, "connectionType")),
        };
    }

    private static IvaoConnectionType? ConnectionTypeOf(string? word)
    {
        foreach (var (type, known) in ConnectionWords)
        {
            if (string.Equals(known, word, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        return null;
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

        // When a revision was filed is its updatedAt: the revisions of a plan share one createdAt unless the route changes,
        // and on a real flight two of four revisions came after the take-off with the createdAt of the first (T17, measured
        // on the tours' own PIREPs of September 2026).
        return new IvaoFlightPlanDto(
            (long)id,
            (int)(Number(item, "revision") ?? 1),
            Moment(item, "updatedAt") ?? Moment(item, "createdAt") ?? DateTime.UnixEpoch,
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
