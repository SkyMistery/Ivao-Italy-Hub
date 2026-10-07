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

    /// <summary>
    /// The ATC positions of the world, in the two lists IVAO keeps: the positions of the airports and the sectors of the
    /// FIRs (M3, A2). Each half is empty when IVAO could not be asked, and never an exception: they are the heaviest
    /// answers of the reference data, and the rest of the night's snapshot must not go down with them.
    /// <para>⚠️ By default a client knows none. A client written before A2 — the doubles of the tests among them — keeps
    /// compiling and answers what it knows, and the synchronisation keeps the positions it already has.</para>
    /// </summary>
    Task<(IReadOnlyList<IvaoAtcPositionDto> Airports, IReadOnlyList<IvaoAtcPositionDto> Sectors)>
        GetAtcPositionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<(IReadOnlyList<IvaoAtcPositionDto>, IReadOnlyList<IvaoAtcPositionDto>)>(([], []));

    /// <summary>
    /// The FRAs of the positions of a country — the lowest rating that may connect to each, by day and hour or for a date —
    /// and only the rows of a position, never the ones IVAO keeps for a member (M4, E10c). Never an exception, like the
    /// positions: the rest of the night's snapshot must not go down with them.
    /// <para><c>null</c> means IVAO could not be asked, even half way through its pages, and it is not the same answer as an
    /// empty list: a division can lift all of its FRAs, and "it has none" must clear them where "we could not look" keeps
    /// them.</para>
    /// <para>⚠️ By default a client cannot be asked, as for the positions (A2): a client written before E10c — the doubles of
    /// the tests among them — keeps compiling, and the synchronisation keeps the FRAs it already has.</para>
    /// </summary>
    Task<IReadOnlyList<IvaoFraDto>?> GetFrasAsync(string countryId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IvaoFraDto>?>(null);

    /// <summary>The profile behind a member's access token, as raw JSON.</summary>
    Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// The connections of one member in a window, newest first, for a pilot picking the flight they
    /// are reporting (design M2 section 3.2) — or, without a VID, the connections at an airport, for
    /// the events that count who flew (M4, E10a). Read page after page, up to the query's
    /// <see cref="IvaoSessionQuery.Limit"/>.
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
    /// Who is connected right now, counted for the whole network and for an airspace: the division's,
    /// or the airports a screen asks about (<see cref="IvaoAirspace.OfAirports"/>, M4, E4b). Read once
    /// a minute for every airspace that asks, because a page full of this block must not turn into a
    /// call per reader, nor a set of airports into a call of its own (<see cref="IvaoNetworkPicture"/>),
    /// and never thrown from: <see cref="IvaoNetworkStatus.Unknown"/> is what a caller gets when the
    /// network cannot be reached, and a minute old figure beats a page that stops.
    /// </summary>
    Task<IvaoNetworkStatus> GetNetworkStatusAsync(
        IvaoAirspace airspace,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The bookings of ATC positions IVAO lists for one day of UTC, <c>/v2/atc/bookings/daily</c> (M4, E15a). As measured on
    /// 30 September 2026: every booking that touches the day — one across midnight is on both days, one that ends at midnight
    /// on the next day too, one that starts at midnight not on the day before —, and with <paramref name="position"/> only
    /// those whose callsign starts with it, in any case. A module asks <see cref="IAtcBookingSource"/>, which knows all that.
    /// <para><see langword="null"/> when IVAO could not be asked, whatever went wrong: it is asked while somebody waits on a
    /// page, and «not available» must never read as «nobody booked».</para>
    /// <para>⚠️ By default a client knows none: a client written before E15a — the doubles of the tests among them — keeps
    /// compiling and answers «not available».</para>
    /// </summary>
    Task<IReadOnlyList<AtcBookingDto>?> GetDailyAtcBookingsAsync(
        DateOnly date,
        string? position = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AtcBookingDto>?>(null);
}
