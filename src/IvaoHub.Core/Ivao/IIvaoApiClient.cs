using System.Text.Json;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// A FIR as IVAO describes it, plus the payload it came in. The raw JSON is kept so that a field
/// nobody reads today does not have to be guessed at tomorrow.
/// </summary>
public sealed record IvaoCenterDto(string Id, string Name, string CountryId, string RawJson);

/// <summary>
/// The METAR IVAO holds for an airport, with the moment it was issued. Measured on 16 September
/// 2026: <c>/v2/airports/{icao}/metar</c> answers <c>{ airportIcao, metar, updatedAt }</c> for both
/// spellings of the code — the lower case rule of the design no longer holds, and does no harm.
/// </summary>
public sealed record IvaoMetarDto(string Icao, string Raw, DateTime? UpdatedAt);

/// <summary>
/// An airport as IVAO describes it, with its runways left as they came. The four properties below
/// the constructor arrived with the world snapshot of T1: they are what a leg of a tour needs
/// (where the airport is) and what an Open tour filters on (how high it is).
/// </summary>
public sealed record IvaoAirportDto(
    string Icao,
    string Name,
    string CountryId,
    string? CenterId,
    string? RunwaysJson,
    string RawJson)
{
    public string? Iata { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public int? ElevationFeet { get; init; }
}

/// <summary>
/// The one typed client that talks to IVAO. Everything else in the hub goes through it, so that
/// retries, the circuit breaker and the token cache exist once (plan section 16, IVAO API).
/// </summary>
public interface IIvaoApiClient
{
    /// <summary>The FIRs of a country. Empty when IVAO cannot be reached: never an exception.</summary>
    Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(string countryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The airports of a country, with their runways when asked for — or, with
    /// <paramref name="countryId"/> left null, <b>the airports of the world</b>, which is what a
    /// module whose legs go anywhere needs (design M2 section 1.12).
    /// </summary>
    Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(
        string? countryId,
        bool includeRunways = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The runways of one airport, thresholds and all. One call per airport, which is why they are
    /// fetched on demand rather than for the world (<see cref="IRunwayDirectory"/>). <c>null</c>
    /// when IVAO could not be asked.
    /// </summary>
    Task<IReadOnlyList<IvaoRunway>?> GetRunwaysAsync(string icao, CancellationToken cancellationToken = default);

    /// <summary>Every aircraft type IVAO knows, for the editor and for the "allowed aircraft" check.</summary>
    Task<IReadOnlyList<IvaoAircraftType>> GetAircraftTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>The equipment letters and the transponder letters of a flight plan, as vocabularies.</summary>
    Task<(IReadOnlyList<IvaoAircraftEquipment> Equipments, IReadOnlyList<IvaoTransponderType> Transponders)>
        GetFlightPlanVocabulariesAsync(CancellationToken cancellationToken = default);

    /// <summary>The profile behind a member's access token, as raw JSON.</summary>
    Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// The connections of one member in a window, newest first, for a pilot picking the flight they
    /// are reporting (design M2 section 3.2).
    /// <para><c>null</c> means IVAO could not be asked, and it is not the same answer as an empty
    /// list: "no flight of yours matches" and "we could not look" must never read alike to a pilot
    /// who is sure they flew it.</para>
    /// </summary>
    Task<IReadOnlyList<IvaoTrackerSessionDto>?> SearchSessionsAsync(
        IvaoSessionQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every revision of the flight plan of a session, oldest first. Which one counts at take off is
    /// decided by the module, never here.
    /// </summary>
    Task<IReadOnlyList<IvaoFlightPlanDto>?> GetFlightPlansAsync(
        long sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>The points of a session, oldest first. IVAO keeps them for about ninety days.</summary>
    Task<IReadOnlyList<IvaoTrackPointDto>?> GetTracksAsync(
        long sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The current METAR IVAO holds for an airport — one link of the weather chain the core builds
    /// (design M2 section 1.13). <c>null</c> when IVAO has none or cannot be asked.
    /// </summary>
    Task<IvaoMetarDto?> GetMetarAsync(string icao, CancellationToken cancellationToken = default);

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
