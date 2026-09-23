using IvaoHub.Core.Airspace;
using IvaoHub.Core.Atc;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>
/// Gathers what <see cref="AtcProposal"/> reads (design M2 §3.3): the archive's positions for the interval of the flights,
/// and the regions the track was in, one point a minute. Asked while the pilot fills the form in and again at the send, where
/// the answer is the one the report keeps.
/// </summary>
public sealed class AtcProposer(IAtcActivitySource archive, IFirLocator firs)
{
    /// <summary>
    /// One point of the track a minute: the network samples about every 15 seconds, and a region is not crossed in less than
    /// a minute at the speeds of the network.
    /// </summary>
    public static readonly TimeSpan SampleEvery = TimeSpan.FromMinutes(1);

    public string Attribution => firs.Attribution;

    /// <summary>
    /// The archive for the interval of the flights and the positions to propose; the activity is null, and nothing is
    /// proposed, when there is no archive or it could not be read. <paramref name="diversionIcao"/> is where the first of two
    /// flights landed, for a diversion.
    /// </summary>
    public async Task<(AtcActivity? Activity, IReadOnlyList<AtcPresence> Proposed)> ProposeAsync(
        IReadOnlyList<TrackedFlight> flights,
        string? diversionIcao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flights);

        if (flights.Count == 0)
        {
            return (null, []);
        }

        var (from, to) = Interval(flights);
        var activity = await archive.OnlineAsync(from, to, cancellationToken);
        if (activity is null)
        {
            return (null, []);
        }

        var passages = new List<FlightPassage>();
        for (var index = 0; index < flights.Count; index++)
        {
            var flight = flights[index];
            var landedAt = index == 0 && flights.Count > 1 && diversionIcao is not null ? diversionIcao : flight.ArrivalIcao;

            passages.Add(new FlightPassage(
                flight.DepartureIcao,
                landedAt,
                flight.Session.StartedAt,
                flight.Session.EndedAt,
                flight.TakeoffAt ?? flight.Session.StartedAt,
                flight.LandingAt,
                await CrossingsAsync(flight, cancellationToken)));
        }

        return (activity, AtcProposal.Propose(passages, activity));
    }

    /// <summary>From the start of the first session to the end of the last: what an exemption's position is asked about.</summary>
    public static (DateTime From, DateTime To) Interval(IReadOnlyList<TrackedFlight> flights)
    {
        ArgumentNullException.ThrowIfNull(flights);

        return (flights.Min(flight => flight.Session.StartedAt), flights.Max(flight => flight.Session.EndedAt));
    }

    private async Task<IReadOnlyList<RegionCrossing>> CrossingsAsync(TrackedFlight flight, CancellationToken cancellationToken)
    {
        var crossings = new List<RegionCrossing>();
        DateTime? last = null;

        foreach (var point in flight.Track.Where(point => !point.OnGround))
        {
            if (last is { } previous && point.At - previous < SampleEvery)
            {
                continue;
            }

            last = point.At;
            var regions = await firs.LocateAsync(point.Latitude, point.Longitude, cancellationToken);
            if (regions.Count > 0)
            {
                crossings.Add(new RegionCrossing(point.At, regions));
            }
        }

        return crossings;
    }
}
