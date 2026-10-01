using System.Globalization;
using System.Text.Json;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// Reads the reference data from files instead of from IVAO, for when the OAuth client of a
/// division is not allowed those endpoints, or when somebody wants to run the hub with no
/// credentials at all (design M0 section 4.6).
/// <para>Switched on with <c>Ivao:UseFixtures=true</c>, and refused outside development and the
/// end to end bench: a production site must never quietly serve invented airspace.</para>
/// </summary>
public sealed class FixtureIvaoApiClient : IIvaoApiClient
{
    /// <summary>Where the files live, relative to the root of the repository.</summary>
    public const string Directory = "tests/fixtures/ivao";

    private readonly HubPaths _paths;
    private readonly ILogger<FixtureIvaoApiClient> _logger;

    public FixtureIvaoApiClient(HubPaths paths, IHostEnvironment environment, ILogger<FixtureIvaoApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(environment);

        // The guard lives here as well as at registration, because configuration can arrive late:
        // this is the object that would actually serve the invented airspace.
        //
        // The end to end bench is allowed it for the same reason development is, and needs it more:
        // it runs with no IVAO credentials at all, and the start up sync of the reference data is
        // awaited before the first request is served. Without the files it would spend that time
        // failing against an API it cannot reach.
        if (!environment.IsDevelopment() && !environment.IsEnvironment(HubEnvironments.E2E))
        {
            throw new InvalidOperationException(
                $"{IvaoServiceCollectionExtensions.UseFixturesKey} is only allowed in development "
                + $"and in the {HubEnvironments.E2E} environment.");
        }

        _paths = paths;
        _logger = logger;
    }

    public Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(
        string countryId,
        CancellationToken cancellationToken = default)
    {
        var centers = Read($"centers-{countryId}.json")
            .Select(item => new IvaoCenterDto(
                Required(item, "centerId").ToUpperInvariant(),
                Required(item, "name"),
                Required(item, "countryId"),
                item.GetRawText()))
            .ToArray();

        return Task.FromResult<IReadOnlyList<IvaoCenterDto>>(centers);
    }

    public Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(
        string? countryId,
        bool includeRunways = true,
        CancellationToken cancellationToken = default)
    {
        // Without a country the real client answers with the world; the file of the division is the
        // world a bench without credentials has, and reading it keeps the two callers on one path.
        var airports = Read($"airports-{countryId ?? "world"}.json")
            .Select(item => new IvaoAirportDto(
                Required(item, "icao").ToUpperInvariant(),
                Required(item, "name"),
                Required(item, "countryId"),
                item.TryGetProperty("centerId", out var center) ? center.GetString()?.ToUpperInvariant() : null,
                includeRunways && item.TryGetProperty("runways", out var runways) ? runways.GetRawText() : null,
                item.GetRawText())
            {
                Iata = item.TryGetProperty("iata", out var iata) ? iata.GetString()?.ToUpperInvariant() : null,
                Latitude = Decimal(item, "latitude"),
                Longitude = Decimal(item, "longitude"),
                ElevationFeet = (int?)Decimal(item, "elevation"),
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<IvaoAirportDto>>(airports);
    }

    /// <summary>Not available from fixtures: a member's profile only exists behind a real login.</summary>
    public Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default) =>
        Task.FromResult<JsonElement?>(null);

    /// <summary>
    /// The runways of an airport the bench knows, from <c>runways-{icao}.json</c>. A bench without
    /// the file answers with none, which is what an airport without published runways looks like.
    /// </summary>
    public Task<IReadOnlyList<IvaoRunway>?> GetRunwaysAsync(
        string icao,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);

        var code = icao.ToUpperInvariant();
        var runways = Read($"runways-{code}.json")
            .Select(item => new IvaoRunway
            {
                AirportIcao = code,
                Designator = Required(item, "runway").ToUpperInvariant(),
                LengthMetres = (int?)Decimal(item, "length"),
                WidthMetres = (int?)Decimal(item, "width"),
                Bearing = (int?)Decimal(item, "bearing"),
                Latitude = Decimal(item, "latitude"),
                Longitude = Decimal(item, "longitude"),
                ElevationFeet = (int?)Decimal(item, "elevation"),
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<IvaoRunway>?>(runways);
    }

    public Task<IReadOnlyList<IvaoAircraftType>> GetAircraftTypesAsync(CancellationToken cancellationToken = default)
    {
        var types = Read("aircraft.json")
            .Select(item => new IvaoAircraftType
            {
                IcaoCode = Required(item, "icaoCode").ToUpperInvariant(),
                Model = Required(item, "model"),
                Manufacturer = item.TryGetProperty("manufacture", out var maker)
                    && maker.TryGetProperty("name", out var name)
                        ? name.GetString()
                        : null,
                Description = item.TryGetProperty("description", out var description) ? description.GetString() : null,
                WakeTurbulence = item.TryGetProperty("wakeTurbulence", out var wake)
                    ? wake.GetString()?.ToUpperInvariant()
                    : null,
                NumberOfEngines = (int?)Decimal(item, "numberEngines"),
                RawJson = item.GetRawText(),
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<IvaoAircraftType>>(types);
    }

    public Task<(IReadOnlyList<IvaoAircraftEquipment> Equipments, IReadOnlyList<IvaoTransponderType> Transponders)>
        GetFlightPlanVocabulariesAsync(CancellationToken cancellationToken = default)
    {
        var equipments = Read("aircraft-equipments.json")
            .Select(item => new IvaoAircraftEquipment
            {
                Id = Required(item, "id").ToUpperInvariant(),
                Name = Required(item, "name"),
                Order = (int)(Decimal(item, "order") ?? 0),
            })
            .ToArray();

        var transponders = Read("transponder-types.json")
            .Select(item => new IvaoTransponderType
            {
                Id = Required(item, "id").ToUpperInvariant(),
                Name = Required(item, "name"),
                Order = (int)(Decimal(item, "order") ?? 0),
            })
            .ToArray();

        return Task.FromResult<(IReadOnlyList<IvaoAircraftEquipment>, IReadOnlyList<IvaoTransponderType>)>(
            (equipments, transponders));
    }

    /// <summary>
    /// The positions of the bench's airports and the sectors of its FIRs, a French one among them, recorded from IVAO's
    /// two answers of the world (<c>tools/record-ivao-fixtures.mjs --positions world …</c>) and read through the very
    /// reader the real client uses.
    /// </summary>
    public Task<(IReadOnlyList<IvaoAtcPositionDto> Airports, IReadOnlyList<IvaoAtcPositionDto> Sectors)>
        GetAtcPositionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<(IReadOnlyList<IvaoAtcPositionDto>, IReadOnlyList<IvaoAtcPositionDto>)>((
            IvaoAtcPositionReader.ReadAirportPositions(Read("atc-positions-world.json")),
            IvaoAtcPositionReader.ReadSectors(Read("subcenters-world.json"))));

    /// <summary>
    /// The FRAs of the bench's positions of a country, recorded from IVAO's answer for it
    /// (<c>tools/record-ivao-fixtures.mjs --fras …</c>) and read through the very reader the real client uses. A country the
    /// bench has no file for is not answered at all, which keeps whatever the snapshot holds: no file is not "no FRA".
    /// </summary>
    public Task<IReadOnlyList<IvaoFraDto>?> GetFrasAsync(string countryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryId);

        var file = $"fras-{countryId.ToUpperInvariant()}.json";
        return Task.FromResult<IReadOnlyList<IvaoFraDto>?>(
            File.Exists(Path.Combine(_paths.Root, Directory, file)) ? IvaoFraReader.Read(Read(file)) : null);
    }

    /// <summary>
    /// The recorded sessions of a member, filtered the way the API filters them, so that a test and
    /// production disagree about nothing except where the bytes came from. The files are written by
    /// <c>tools/record-ivao-fixtures.mjs</c> from real flights.
    /// <para>Without a VID, the sessions at an airport (M4, E10a): <c>tracker-airport-{icao}.json</c>, what happened at
    /// one airport in one evening (<c>--sessions-at</c>), for the departure and the arrival asked. The tracker's own rule
    /// picks the rows (<see cref="IvaoTrackerReader.Answers"/>), newest first as IVAO lists them, up to the limit.</para>
    /// </summary>
    public Task<IReadOnlyList<IvaoTrackerSessionDto>?> SearchSessionsAsync(
        IvaoSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        string[] files = query.Vid is { } vid
            ? [$"tracker-sessions-{vid}.json"]
            : [.. new[] { query.DepartureIcao, query.ArrivalIcao }
                .Where(airport => !string.IsNullOrWhiteSpace(airport))
                .Select(airport => $"tracker-airport-{airport!.Trim().ToUpperInvariant()}.json")
                .Distinct(StringComparer.Ordinal)];

        var sessions = files
            .SelectMany(Read)
            .Where(row => IvaoTrackerReader.Answers(row, query))
            .Select(IvaoTrackerReader.ReadSession)
            .OfType<IvaoTrackerSessionDto>()
            .DistinctBy(session => session.Id)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .Take(query.Limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<IvaoTrackerSessionDto>?>(sessions);
    }

    public Task<IReadOnlyList<IvaoFlightPlanDto>?> GetFlightPlansAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        var plans = Read($"tracker-flightplans-{sessionId}.json")
            .Select(IvaoTrackerReader.ReadFlightPlan)
            .OfType<IvaoFlightPlanDto>()
            .OrderBy(plan => plan.Revision)
            .ToArray();

        return Task.FromResult<IReadOnlyList<IvaoFlightPlanDto>?>(plans);
    }

    public Task<IReadOnlyList<IvaoTrackPointDto>?> GetTracksAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        var points = Read($"tracker-tracks-{sessionId}.json")
            .Select(IvaoTrackerReader.ReadTrackPoint)
            .OfType<IvaoTrackPointDto>()
            .OrderBy(point => point.At)
            .ToArray();

        return Task.FromResult<IReadOnlyList<IvaoTrackPointDto>?>(points);
    }

    public Task<IvaoMetarDto?> GetMetarAsync(string icao, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);

        var wanted = icao.ToUpperInvariant();
        foreach (var item in Read("metars.json"))
        {
            if (!string.Equals(Required(item, "airportIcao"), wanted, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var raw = Required(item, "metar");
            var updated = item.TryGetProperty("updatedAt", out var moment)
                && moment.ValueKind == JsonValueKind.String
                && DateTime.TryParse(
                    moment.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var parsed)
                    ? parsed
                    : (DateTime?)null;

            return Task.FromResult<IvaoMetarDto?>(new IvaoMetarDto(wanted, raw, updated));
        }

        return Task.FromResult<IvaoMetarDto?>(null);
    }

    /// <summary>
    /// The connections of an evening that always looks the same, read through the very same rule
    /// the real client uses: what "in the area" means is <see cref="IvaoWhazzup"/>'s to say, here
    /// as in production. No cache, because the file does not move.
    /// </summary>
    public Task<IvaoNetworkStatus> GetNetworkStatusAsync(
        IvaoAirspace airspace,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_paths.Root, Directory, "whazzup.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning("No IVAO fixture at {Path}; answering with nothing.", path);
            return Task.FromResult(IvaoNetworkStatus.Unknown);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return Task.FromResult(IvaoWhazzup.Read(document.RootElement, airspace));
    }

    /// <summary>
    /// The bookings of a day that always looks the same: <c>atc-bookings-day.json</c> holds the bookings of the bench's positions
    /// that started on one real day (<c>tools/record-ivao-fixtures.mjs --bookings day …</c>), read through the very reader the
    /// real client uses and moved onto whichever day is asked — so a bench with no credentials shows bookings beside an event
    /// of any date. The day is listed the way IVAO lists it: every booking that starts before the day ends and ends when it
    /// has begun or later, the one across midnight of the day before among them, and a position is the start of a callsign.
    /// </summary>
    public Task<IReadOnlyList<AtcBookingDto>?> GetDailyAtcBookingsAsync(
        DateOnly date,
        string? position = null,
        CancellationToken cancellationToken = default)
    {
        var recorded = Read("atc-bookings-day.json")
            .Select(IvaoAtcBookingReader.ReadBooking)
            .OfType<AtcBookingDto>()
            .ToArray();

        if (recorded.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<AtcBookingDto>?>([]);
        }

        // Every recorded booking started on the recorded day, and none lasts a day: the day asked lists its own, moved onto
        // it, and those of the day before that were still open when it began.
        var recordedDay = DateOnly.FromDateTime(recorded.Min(booking => booking.StartsAt));
        var opens = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var closes = opens.AddDays(1);
        var prefix = position?.Trim() ?? string.Empty;

        AtcBookingDto[] listed = [.. new[] { date.AddDays(-1), date }
            .SelectMany(day => recorded.Select(booking => booking with
            {
                StartsAt = booking.StartsAt.AddDays(day.DayNumber - recordedDay.DayNumber),
                EndsAt = booking.EndsAt.AddDays(day.DayNumber - recordedDay.DayNumber),
            }))
            .Where(booking => booking.StartsAt < closes && booking.EndsAt >= opens)
            .Where(booking => booking.Callsign.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];

        return Task.FromResult<IReadOnlyList<AtcBookingDto>?>(listed);
    }

    private JsonElement[] Read(string fileName)
    {
        var path = Path.Combine(_paths.Root, Directory, fileName);
        if (!File.Exists(path))
        {
            _logger.LogWarning("No IVAO fixture at {Path}; answering with nothing.", path);
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return [.. document.RootElement.EnumerateArray().Select(item => item.Clone())];
    }

    private static double? Decimal(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;

    private static string Required(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
