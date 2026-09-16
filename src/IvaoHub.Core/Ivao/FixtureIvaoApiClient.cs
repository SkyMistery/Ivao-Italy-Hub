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
        string countryId,
        bool includeRunways = true,
        CancellationToken cancellationToken = default)
    {
        var airports = Read($"airports-{countryId}.json")
            .Select(item => new IvaoAirportDto(
                Required(item, "icao").ToUpperInvariant(),
                Required(item, "name"),
                Required(item, "countryId"),
                item.TryGetProperty("centerId", out var center) ? center.GetString()?.ToUpperInvariant() : null,
                includeRunways && item.TryGetProperty("runways", out var runways) ? runways.GetRawText() : null,
                item.GetRawText()))
            .ToArray();

        return Task.FromResult<IReadOnlyList<IvaoAirportDto>>(airports);
    }

    /// <summary>Not available from fixtures: a member's profile only exists behind a real login.</summary>
    public Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default) =>
        Task.FromResult<JsonElement?>(null);

    /// <summary>
    /// The recorded sessions of a member, filtered the way the API filters them, so that a test and
    /// production disagree about nothing except where the bytes came from. The files are written by
    /// <c>tools/record-ivao-fixtures.mjs</c> from real flights.
    /// </summary>
    public Task<IReadOnlyList<IvaoTrackerSessionDto>?> SearchSessionsAsync(
        IvaoSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sessions = Read($"tracker-sessions-{query.Vid}.json")
            .Select(IvaoTrackerReader.ReadSession)
            .OfType<IvaoTrackerSessionDto>()
            .Where(session => session.StartedAt >= query.FromUtc && session.StartedAt <= query.ToUtc)
            .Where(session => Matches(session.DepartureIcao, query.DepartureIcao))
            .Where(session => Matches(session.ArrivalIcao, query.ArrivalIcao))
            .Take(IvaoSessionQuery.MaxSessions)
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

    private static bool Matches(string? value, string? wanted) =>
        string.IsNullOrWhiteSpace(wanted)
        || string.Equals(value, wanted, StringComparison.OrdinalIgnoreCase);

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

    private static string Required(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
