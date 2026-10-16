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
/// <param name="Connection">Pilots or controllers only, or both.</param>
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
/// and when it started and, as far as its declared length says, ended. Both instants are UTC.
/// </summary>
public sealed record NetworkSession(long Id, int Vid, string Callsign, DateTime StartedAt, DateTime EndedAt);

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
            .Select(session => new NetworkSession(
                session.Id,
                session.Vid,
                session.Callsign,
                session.StartedAt,
                session.EndedAt))];
    }
}
