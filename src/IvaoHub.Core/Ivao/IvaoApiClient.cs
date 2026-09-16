using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The typed client. Retries and the circuit breaker come from the standard resilience handler, so
/// a bad afternoon at IVAO slows the hub down instead of taking it out.
/// <para>Nothing here throws on a failed call: the reference data is a snapshot, and an empty
/// answer means "keep what is already in the database" rather than "stop".</para>
/// </summary>
public sealed class IvaoApiClient(
    HttpClient http,
    IvaoApiTokenProvider tokens,
    IMemoryCache cache,
    ILogger<IvaoApiClient> logger) : IIvaoApiClient
{
    public async Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(
        string countryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryId);

        var payload = await ReadAsync(
            $"/v2/centers?countryId={Uri.EscapeDataString(countryId)}",
            cancellationToken);

        if (payload is not { } root)
        {
            return [];
        }

        var centers = new List<IvaoCenterDto>();
        foreach (var item in Items(root))
        {
            var id = Text(item, "centerId") ?? Text(item, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            centers.Add(new IvaoCenterDto(
                id.ToUpperInvariant(),
                Text(item, "name") ?? id,
                Text(item, "countryId") ?? countryId,
                item.GetRawText()));
        }

        logger.LogInformation("Read {Count} centre(s) for {Country} from IVAO.", centers.Count, countryId);
        return centers;
    }

    public async Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(
        string? countryId,
        bool includeRunways = true,
        CancellationToken cancellationToken = default)
    {
        // No country is the world: 44 689 airports and about fourteen megabytes, measured on
        // 16 September 2026. A job asks for it once a day, never a request somebody is waiting on.
        var query = string.IsNullOrWhiteSpace(countryId)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"/v2/airports/all?includeRunways={includeRunways.ToString().ToLowerInvariant()}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"/v2/airports/all?countryId={Uri.EscapeDataString(countryId)}&includeRunways={includeRunways.ToString().ToLowerInvariant()}");

        var payload = await ReadAsync(query, cancellationToken);
        if (payload is not { } root)
        {
            return [];
        }

        var airports = new List<IvaoAirportDto>();
        foreach (var item in Items(root))
        {
            var icao = Text(item, "icao") ?? Text(item, "id");
            if (string.IsNullOrWhiteSpace(icao))
            {
                continue;
            }

            airports.Add(new IvaoAirportDto(
                icao.ToUpperInvariant(),
                Text(item, "name") ?? icao,
                Text(item, "countryId") ?? countryId ?? string.Empty,
                Text(item, "centerId")?.ToUpperInvariant(),
                item.TryGetProperty("runways", out var runways) ? runways.GetRawText() : null,
                item.GetRawText())
            {
                Iata = Text(item, "iata")?.ToUpperInvariant(),
                Latitude = Number(item, "latitude"),
                Longitude = Number(item, "longitude"),
                ElevationFeet = (int?)Number(item, "elevation"),
            });
        }

        logger.LogInformation(
            "Read {Count} airport(s) for {Country} from IVAO.",
            airports.Count,
            countryId ?? "the world");
        return airports;
    }

    public async Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v2/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("IVAO answered /v2/users/me with status {Status}.", (int)response.StatusCode);
            return null;
        }

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return document.RootElement.Clone();
    }

    public async Task<IReadOnlyList<IvaoRunway>?> GetRunwaysAsync(
        string icao,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);

        var code = icao.ToUpperInvariant();
        var payload = await ReadAsync($"/v2/airports/{Uri.EscapeDataString(code)}/runways", cancellationToken);
        if (payload is not { } root)
        {
            return null;
        }

        var runways = new List<IvaoRunway>();
        foreach (var item in Items(root))
        {
            var designator = Text(item, "runway");
            if (string.IsNullOrWhiteSpace(designator))
            {
                continue;
            }

            runways.Add(new IvaoRunway
            {
                AirportIcao = Text(item, "airportIcao")?.ToUpperInvariant() ?? code,
                Designator = designator.ToUpperInvariant(),
                LengthMetres = (int?)Number(item, "length"),
                WidthMetres = (int?)Number(item, "width"),
                Bearing = (int?)Number(item, "bearing"),
                Latitude = Number(item, "latitude"),
                Longitude = Number(item, "longitude"),
                ElevationFeet = (int?)Number(item, "elevation"),
            });
        }

        return runways;
    }

    public async Task<IReadOnlyList<IvaoAircraftType>> GetAircraftTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var payload = await ReadAsync("/v2/aircrafts/all", cancellationToken);
        if (payload is not { } root)
        {
            return [];
        }

        var types = new List<IvaoAircraftType>();
        foreach (var item in Items(root))
        {
            var code = Text(item, "icaoCode");
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            types.Add(new IvaoAircraftType
            {
                IcaoCode = code.ToUpperInvariant(),
                IataCode = Text(item, "iataCode")?.ToUpperInvariant(),
                Model = Text(item, "model") ?? code,
                // IVAO nests the manufacturer as an object; its name is what an editor shows.
                Manufacturer = item.TryGetProperty("manufacture", out var maker) ? Text(maker, "name") : null,
                Description = Text(item, "description"),
                WakeTurbulence = Text(item, "wakeTurbulence")?.ToUpperInvariant(),
                NumberOfEngines = (int?)Number(item, "numberEngines"),
                Military = Text(item, "military"),
                RawJson = item.GetRawText(),
            });
        }

        logger.LogInformation("Read {Count} aircraft type(s) from IVAO.", types.Count);
        return types;
    }

    public async Task<(IReadOnlyList<IvaoAircraftEquipment> Equipments, IReadOnlyList<IvaoTransponderType> Transponders)>
        GetFlightPlanVocabulariesAsync(CancellationToken cancellationToken = default)
    {
        var equipments = new List<IvaoAircraftEquipment>();
        if (await ReadAsync("/v2/aircrafts/equipments", cancellationToken) is { } equipmentPayload)
        {
            foreach (var item in Items(equipmentPayload))
            {
                if (Text(item, "id") is not { } id)
                {
                    continue;
                }

                equipments.Add(new IvaoAircraftEquipment
                {
                    Id = id.ToUpperInvariant(),
                    Name = Text(item, "name") ?? id,
                    Order = (int)(Number(item, "order") ?? 0),
                    IsActive = !item.TryGetProperty("isActive", out var active)
                        || active.ValueKind != JsonValueKind.False,
                });
            }
        }

        var transponders = new List<IvaoTransponderType>();
        if (await ReadAsync("/v2/aircrafts/transponderTypes", cancellationToken) is { } transponderPayload)
        {
            foreach (var item in Items(transponderPayload))
            {
                if (Text(item, "id") is not { } id)
                {
                    continue;
                }

                transponders.Add(new IvaoTransponderType
                {
                    Id = id.ToUpperInvariant(),
                    Name = Text(item, "name") ?? id,
                    Kind = Text(item, "type"),
                    Order = (int)(Number(item, "order") ?? 0),
                });
            }
        }

        return (equipments, transponders);
    }

    public async Task<IReadOnlyList<IvaoTrackerSessionDto>?> SearchSessionsAsync(
        IvaoSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sessions = new List<IvaoTrackerSessionDto>();
        for (var page = 1; ; page++)
        {
            var payload = await ReadAsync(
                $"/v2/tracker/sessions?{query.ToQueryString()}&page={page}&perPage={IvaoSessionQuery.PageSize}",
                cancellationToken);

            // A page that fails after the first one has succeeded is still a failure: half a list of
            // flights would let a pilot conclude theirs is not there.
            if (payload is not { } root)
            {
                return null;
            }

            var (rows, pages) = IvaoTrackerReader.ReadSessions(root);
            sessions.AddRange(rows);

            if (page >= pages || sessions.Count >= IvaoSessionQuery.MaxSessions || rows.Count == 0)
            {
                break;
            }
        }

        logger.LogInformation("Read {Count} tracker session(s) for {Vid} from IVAO.", sessions.Count, query.Vid);
        return sessions.Count > IvaoSessionQuery.MaxSessions
            ? sessions[..IvaoSessionQuery.MaxSessions]
            : sessions;
    }

    public async Task<IReadOnlyList<IvaoFlightPlanDto>?> GetFlightPlansAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        var payload = await ReadAsync($"/v2/tracker/sessions/{sessionId}/flightPlans", cancellationToken);
        return payload is { } root ? IvaoTrackerReader.ReadFlightPlans(root) : null;
    }

    public async Task<IReadOnlyList<IvaoTrackPointDto>?> GetTracksAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        var payload = await ReadAsync($"/v2/tracker/sessions/{sessionId}/tracks", cancellationToken);
        return payload is { } root ? IvaoTrackerReader.ReadTracks(root) : null;
    }

    public async Task<IvaoMetarDto?> GetMetarAsync(string icao, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);

        var payload = await ReadAsync(
            $"/v2/airports/{Uri.EscapeDataString(icao.ToLowerInvariant())}/metar",
            cancellationToken);

        if (payload is not { } root || Text(root, "metar") is not { } raw)
        {
            return null;
        }

        var updated = root.TryGetProperty("updatedAt", out var moment)
            && moment.ValueKind == JsonValueKind.String
            && DateTime.TryParse(
                moment.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed)
                ? parsed
                : (DateTime?)null;

        return new IvaoMetarDto(
            Text(root, "airportIcao")?.ToUpperInvariant() ?? icao.ToUpperInvariant(),
            raw,
            updated);
    }

    public async Task<IvaoNetworkStatus> GetNetworkStatusAsync(
        IvaoAirspace airspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(airspace);

        var key = $"ivao-api:network-status:{airspace.CacheKey}";
        if (cache.TryGetValue(key, out IvaoNetworkStatus? cached) && cached is not null)
        {
            return cached;
        }

        // No token: the picture of who is connected is the public one, and an installation that
        // has not been given client credentials must still be able to draw it.
        var payload = await ReadWithoutTokenAsync(IvaoWhazzup.Path, cancellationToken);
        var status = payload is { } root ? IvaoWhazzup.Read(root, airspace) : IvaoNetworkStatus.Unknown;

        // A failure is cached too, and for the same minute. Without that, a network that is down
        // turns every reader of the page into another call to a network that is down.
        cache.Set(key, status, IvaoWhazzup.Freshness);
        return status;
    }

    /// <summary>A GET with the application's token, or null when anything at all goes wrong.</summary>
    private async Task<JsonElement?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var token = await tokens.GetTokenAsync(cancellationToken);
        if (token is null)
        {
            logger.LogWarning("No IVAO application token, so {Path} was not called.", path);
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("IVAO answered {Path} with status {Status}.", path, (int)response.StatusCode);
            return null;
        }

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return document.RootElement.Clone();
    }

    /// <summary>
    /// A GET of something that belongs to nobody, so it carries no token — and one that a page is
    /// waiting on, so nothing it can do gets as far as the reader. The reference calls above are
    /// made by a job that can afford to fail; this one is made while somebody is looking at a
    /// page, and a network that is unreachable must read as "no figures", never as an error page.
    /// </summary>
    private async Task<JsonElement?> ReadWithoutTokenAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("IVAO answered {Path} with status {Status}.", path, (int)response.StatusCode);
                return null;
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            return document.RootElement.Clone();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Not the cancellation of the request itself: that one is the reader going away, and
            // it is theirs to make.
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogWarning(exception, "IVAO could not be reached at {Path}.", path);
            return null;
        }
    }

    /// <summary>
    /// The rows of an answer, whether IVAO sends a bare array or wraps it in an object with an
    /// <c>items</c> property. Both shapes exist across their endpoints.
    /// </summary>
    private static IEnumerable<JsonElement> Items(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray();
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array)
        {
            return items.EnumerateArray();
        }

        return [];
    }

    private static double? Number(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;

    private static string? Text(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => Trimmed(value.GetString()),
            JsonValueKind.Number => value.ToString(),
            _ => null,
        };
    }

    private static string? Trimmed(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
