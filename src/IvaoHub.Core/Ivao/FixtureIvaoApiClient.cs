using System.Text.Json;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// Reads the reference data from files instead of from IVAO, for when the OAuth client of a
/// division is not allowed those endpoints, or when somebody wants to run the hub with no
/// credentials at all (design M0 section 4.6).
/// <para>Switched on with <c>Ivao:UseFixtures=true</c>, and refused outside development: a
/// production site must never quietly serve invented airspace.</para>
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
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{IvaoServiceCollectionExtensions.UseFixturesKey} is only allowed in development.");
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
