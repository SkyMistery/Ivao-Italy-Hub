using System.Text.Json;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// A FIR as IVAO describes it, plus the payload it came in. The raw JSON is kept so that a field
/// nobody reads today does not have to be guessed at tomorrow.
/// </summary>
public sealed record IvaoCenterDto(string Id, string Name, string CountryId, string RawJson);

/// <summary>An airport as IVAO describes it, with its runways left as they came.</summary>
public sealed record IvaoAirportDto(
    string Icao,
    string Name,
    string CountryId,
    string? CenterId,
    string? RunwaysJson,
    string RawJson);

/// <summary>
/// The one typed client that talks to IVAO. Everything else in the hub goes through it, so that
/// retries, the circuit breaker and the token cache exist once (plan section 16, IVAO API).
/// </summary>
public interface IIvaoApiClient
{
    /// <summary>The FIRs of a country. Empty when IVAO cannot be reached: never an exception.</summary>
    Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(string countryId, CancellationToken cancellationToken = default);

    /// <summary>The airports of a country, with their runways when asked for.</summary>
    Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(
        string countryId,
        bool includeRunways = true,
        CancellationToken cancellationToken = default);

    /// <summary>The profile behind a member's access token, as raw JSON.</summary>
    Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Who is connected right now, counted for the whole network and for the airspace of the
    /// division. Cached for a minute, because a page full of this block must not turn into a call
    /// per reader, and never thrown from: <see cref="IvaoNetworkStatus.Unknown"/> is what a caller
    /// gets when the network cannot be reached, and a minute old figure beats a page that stops.
    /// </summary>
    Task<IvaoNetworkStatus> GetNetworkStatusAsync(
        IvaoAirspace airspace,
        CancellationToken cancellationToken = default);
}
