using System.Globalization;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// "Who connected to the network in this window?" — the question a module asks of the network's tracker (M4, E10t): the events
/// read it after an event, to tell which bookings were flown, how many flew without booking, and who was on an ATC shift (design
/// M4 §5.1, §9.3). The search itself is the one client's (<see cref="IIvaoApiClient.SearchSessionsAsync"/>, E10a): its pages, its
/// ceiling and its rules stay there, and a module reads them through these names, which say nothing of whose network it is
/// (note 2026-10-16-le-sessioni-della-rete-nel-nucleo).
/// </summary>
public interface INetworkSessionSource
{
    /// <summary>
    /// The sessions that <i>started</i> in the window, both ends included, newest first, each once, at most
    /// <see cref="NetworkSessionQuery.Limit"/>: a caller who wants who was connected widens the window backwards. An answer with
    /// exactly as many sessions as the limit may have been cut, and what is left is before the oldest of them.
    /// <para><see langword="null"/> when the network could not be asked, which is never the same answer as an empty list:
    /// «nobody flew» and «we could not look» must not read alike.</para>
    /// </summary>
    Task<IReadOnlyList<NetworkSession>?> SessionsAsync(
        NetworkSessionQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>How a session was connected: the two kinds a module asks for.</summary>
public enum NetworkConnection
{
    /// <summary>Somebody flying.</summary>
    Pilot,

    /// <summary>Somebody controlling a position.</summary>
    Atc,
}

/// <summary>
/// What a module asks the tracker: the sessions that started in a window — of one member, or of everybody at an airport —,
/// optionally from one airport, to another, and of one kind of connection. An airport asked alone is found in any revision of
/// a session's flight plan, two asked together in one same revision; so departures and arrivals are two questions, and the
/// same session may come back from both (note 2026-09-30-il-tracker-senza-vid §3.6).
/// </summary>
/// <param name="Vid">The member, or <see langword="null"/> for everybody.</param>
/// <param name="FromUtc">The first instant a session may start at, UTC.</param>
/// <param name="ToUtc">The last instant a session may start at, UTC.</param>
/// <param name="DepartureIcao">The airport the flight plan leaves from, or none.</param>
/// <param name="ArrivalIcao">The airport the flight plan goes to, or none.</param>
/// <param name="Connection">
/// Pilots only, or controllers only; <see langword="null"/> asks for no kind, and every kind comes back, observers too.
/// </param>
public sealed record NetworkSessionQuery(
    int? Vid,
    DateTime FromUtc,
    DateTime ToUtc,
    string? DepartureIcao = null,
    string? ArrivalIcao = null,
    NetworkConnection? Connection = null)
{
    /// <summary>How many sessions a search reads when its caller does not say.</summary>
    public const int DefaultLimit = IvaoSessionQuery.DefaultLimit;

    /// <summary>The most a caller may declare: the core's ceiling, so that no caller reads the network page after page.</summary>
    public const int MaxLimit = IvaoSessionQuery.MaxLimit;

    private readonly int _limit = DefaultLimit;

    /// <summary>The most sessions the caller will read, newest first, from one to <see cref="MaxLimit"/>.</summary>
    public int Limit
    {
        get => _limit;
        init => _limit = value is >= 1 and <= MaxLimit
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(Limit),
                value,
                string.Create(CultureInfo.InvariantCulture, $"A search reads from one to {MaxLimit} sessions."));
    }
}

/// <summary>
/// One session of the network, as little of it as a module needs: the network's own number for it, the member, the callsign,
/// when it started and, as far as its declared length says, ended — both UTC — and the airports its flight plan said (E10v).
/// </summary>
/// <param name="Id">The network's own number for the session.</param>
/// <param name="Vid">The member.</param>
/// <param name="Callsign">The callsign, or the position of a controller.</param>
/// <param name="StartedAt">When the session started, UTC.</param>
/// <param name="EndedAt">When it ended as far as its declared length says, UTC.</param>
/// <param name="DepartureIcao">
/// The airport the last revision of the flight plan leaves from, in capitals; <see langword="null"/> for a controller or a session
/// without a plan.
/// </param>
/// <param name="ArrivalIcao">The airport the last revision goes to, in capitals; <see langword="null"/> when there is no plan.</param>
public sealed record NetworkSession(
    long Id,
    int Vid,
    string Callsign,
    DateTime StartedAt,
    DateTime EndedAt,
    string? DepartureIcao = null,
    string? ArrivalIcao = null)
{
    /// <summary>
    /// The two airports of every revision of the flight plan, oldest first, the last one being <see cref="DepartureIcao"/> and
    /// <see cref="ArrivalIcao"/>: a pilot who filed one leg and then the next in the same connection has both here. Empty when
    /// there is no plan. Two sessions with the same routes are equal, as for every other part of the record.
    /// </summary>
    public IReadOnlyList<NetworkRoute> Routes { get; init; } = [];

    public bool Equals(NetworkSession? other) =>
        other is not null
        && Id == other.Id
        && Vid == other.Vid
        && string.Equals(Callsign, other.Callsign, StringComparison.Ordinal)
        && StartedAt == other.StartedAt
        && EndedAt == other.EndedAt
        && string.Equals(DepartureIcao, other.DepartureIcao, StringComparison.Ordinal)
        && string.Equals(ArrivalIcao, other.ArrivalIcao, StringComparison.Ordinal)
        && Routes.SequenceEqual(other.Routes);

    public override int GetHashCode() =>
        HashCode.Combine(Id, Vid, Callsign, StartedAt, EndedAt, DepartureIcao, ArrivalIcao, Routes.Count);
}

/// <summary>One revision of a session's flight plan, by its two airports, in capitals.</summary>
public sealed record NetworkRoute(string DepartureIcao, string ArrivalIcao);

internal sealed class NetworkSessionSource(IIvaoApiClient ivao) : INetworkSessionSource
{
    public async Task<IReadOnlyList<NetworkSession>?> SessionsAsync(
        NetworkSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var search = new IvaoSessionQuery(
            query.Vid,
            query.FromUtc,
            query.ToUtc,
            query.DepartureIcao,
            query.ArrivalIcao,
            query.Connection switch
            {
                NetworkConnection.Pilot => IvaoConnectionType.Pilot,
                NetworkConnection.Atc => IvaoConnectionType.Atc,
                _ => null,
            })
        {
            Limit = query.Limit,
        };

        if (await ivao.SearchSessionsAsync(search, cancellationToken) is not { } sessions)
        {
            return null;
        }

        // The client already answers newest first and each once; the order is said again here because it is this
        // interface's promise, whatever client stands behind it.
        return [.. sessions
            .DistinctBy(session => session.Id)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .Take(query.Limit)
            .Select(Session)];
    }

    /// <summary>
    /// A session with the airports of its plan's revisions, read from the row the client already has: no second call. The last
    /// revision says the two airports, not the first one the client's record keeps (note 2026-10-17-gli-scali-di-una-sessione).
    /// </summary>
    private static NetworkSession Session(IvaoTrackerSessionDto session)
    {
        NetworkRoute[] routes = [.. IvaoTrackerReader.ReadPlannedRoutes(session)
            .Select(route => new NetworkRoute(route.DepartureIcao, route.ArrivalIcao))];

        return new NetworkSession(
            session.Id,
            session.Vid,
            session.Callsign,
            session.StartedAt,
            session.EndedAt,
            routes.LastOrDefault()?.DepartureIcao,
            routes.LastOrDefault()?.ArrivalIcao)
        {
            Routes = routes,
        };
    }
}
