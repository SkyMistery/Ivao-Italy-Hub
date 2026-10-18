using IvaoHub.Core.Airspace;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.After;

namespace IvaoHub.Modules.Events.Reports;

/// <summary>
/// The check of a report of support (design M4 §5.3, E14a; note 2026-10-17-il-pirep-di-supporto, reading 5), run at every send and before
/// the report's lock is taken — the network may take seconds —: for each item, the session, the minutes and the distance, through the core's
/// sources, never the network's client.
/// <list type="bullet">
/// <item><b>A flight</b>: the member's sessions as a pilot (<see cref="INetworkSessionSource"/>) that started on its day within the event's
/// window of flights, between its two airports — <b>the last revision of their plan</b>, never one only filed (E14b, <see cref="ReportRules.FlewRoute"/>);
/// the one <see cref="ReportRules.Flown"/> gives it over the whole report, with its declared minutes. The distance between
/// the two airports (<see cref="GreatCircle"/> on <see cref="IAirportDirectory"/>) is written found or not.</item>
/// <item><b>A shift</b>: as <c>events-after</c> checks a shift of the roster (E13a) — the archive of the sessions first, the tracker by VID where
/// the archive does not answer or does not cover the hours —, verified when the minutes on the position reach <c>attendanceMinimumMinutes</c>
/// (at most the whole shift).</item>
/// </list>
/// A source that does not answer leaves the item not asked (<c>Verified</c> empty), and the report is <see cref="ReportVerification.Unavailable"/>.
/// </summary>
public sealed class ReportCheck(INetworkSessionSource network, IAtcActivitySource archive, IAirportDirectory airports)
{
    /// <summary>What the check found of each item, in their order.</summary>
    public async Task<IReadOnlyList<ItemFinding>> CheckAsync(
        Event row,
        int vid,
        ReportKind kind,
        IReadOnlyList<ReportItemDraft> items,
        int attendanceMinimumMinutes,
        int minimumFlightMinutes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(items);

        return kind == ReportKind.Pilot
            ? await FlightsAsync(row, vid, items, minimumFlightMinutes, cancellationToken)
            : await ShiftsAsync(vid, items, attendanceMinimumMinutes, cancellationToken);
    }

    private async Task<IReadOnlyList<ItemFinding>> FlightsAsync(
        Event row,
        int vid,
        IReadOnlyList<ReportItemDraft> items,
        int minimumFlightMinutes,
        CancellationToken cancellationToken)
    {
        var codes = items.SelectMany(item => new[] { item.DepartureIcao, item.ArrivalIcao }).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var known = await airports.FindAsync(codes, cancellationToken);

        // What the network answers for each flight, once per pair of airports and day; then the sessions given out over the whole report.
        var asked = new Dictionary<(string, string, DateOnly), IReadOnlyList<NetworkSession>?>();
        var flights = new List<(string Callsign, IReadOnlyList<NetworkSession>? Found)>(items.Count);
        foreach (var item in items)
        {
            if (item is not { DepartureIcao: { } departure, ArrivalIcao: { } arrival, FlightDay: { } day })
            {
                flights.Add((item.Callsign, []));
                continue;
            }

            if (!asked.TryGetValue((departure, arrival, day), out var found))
            {
                var (fromUtc, toUtc) = ReportRules.DayWindow(row, day);
                found = await network.SessionsAsync(
                    new NetworkSessionQuery(vid, fromUtc, toUtc, departure, arrival, NetworkConnection.Pilot),
                    cancellationToken);
                asked[(departure, arrival, day)] = found;
            }

            // Only a session whose plan ended on this route flew it: one found for a revision the pilot filed and then changed did not; and one
            // shorter than minimumFlightMinutes flew nothing (E14b, reading 3-bis of note 2026-10-17-le-regole-di-award; Carmine's answer 1 on #293).
            flights.Add((item.Callsign, found?.Where(session => ReportRules.FlewRoute(session, departure, arrival)
                && ReportRules.LongEnough(session, minimumFlightMinutes)).ToList()));
        }

        var flown = ReportRules.Flown(flights);
        var findings = new List<ItemFinding>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var distance = Distance(items[index].DepartureIcao, items[index].ArrivalIcao, known);
            findings.Add(flights[index].Found is null
                ? new ItemFinding(null, null, distance, Verified: null)
                : flown[index] is { } session
                    ? new ItemFinding(session.Id, (int)(session.EndedAt - session.StartedAt).TotalMinutes, distance, Verified: true)
                    : new ItemFinding(null, null, distance, Verified: false));
        }

        return findings;
    }

    private async Task<IReadOnlyList<ItemFinding>> ShiftsAsync(
        int vid,
        IReadOnlyList<ReportItemDraft> items,
        int attendanceMinimumMinutes,
        CancellationToken cancellationToken)
    {
        var findings = new List<ItemFinding>(items.Count);
        foreach (var item in items)
        {
            if (item is not { FromUtc: { } fromUtc, ToUtc: { } toUtc })
            {
                findings.Add(new ItemFinding(null, null, null, Verified: false));
                continue;
            }

            var minutes = await MinutesAsync(vid, item.Callsign, fromUtc, toUtc, cancellationToken);
            findings.Add(minutes is { } found
                ? new ItemFinding(null, found, null, found >= AfterRules.Threshold(attendanceMinimumMinutes, fromUtc, toUtc))
                : new ItemFinding(null, null, null, Verified: null));
        }

        return findings;
    }

    /// <summary>
    /// The member's minutes on a position within the hours, as the check after the event counts them (E13a): the archive where it covers the
    /// hours, the tracker by VID otherwise; null when neither answers.
    /// </summary>
    private async Task<int?> MinutesAsync(int vid, string callsign, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        // A division with no archive (atcData: none) is not asked at all, as events-after does not ask it (the review of #278, point 4).
        if (archive is not UnavailableAtcActivitySource
            && await archive.SessionsOfAsync(vid, fromUtc, toUtc, cancellationToken) is { } activity)
        {
            var minutes = AfterRules.MinutesWithin(activity.Of(callsign).Select(AfterRules.Connection), fromUtc, toUtc);
            if (minutes > 0 || activity.Covers(callsign, fromUtc))
            {
                return minutes;
            }
        }

        var sessions = await network.SessionsAsync(
            new NetworkSessionQuery(vid, fromUtc - AfterRules.ConnectedBefore, toUtc, Connection: NetworkConnection.Atc),
            cancellationToken);
        if (sessions is null)
        {
            return null;
        }

        return AfterRules.MinutesWithin(
            sessions
                .Where(session => string.Equals(session.Callsign, callsign, StringComparison.OrdinalIgnoreCase))
                .Select(AfterRules.Connection),
            fromUtc,
            toUtc);
    }

    /// <summary>The distance between two airports the hub knows with their coordinates, to the tenth; none otherwise.</summary>
    internal static decimal? Distance(string? departure, string? arrival, IReadOnlyDictionary<string, AirportDto> known) =>
        departure is not null
        && arrival is not null
        && known.GetValueOrDefault(departure) is { Latitude: { } fromLatitude, Longitude: { } fromLongitude }
        && known.GetValueOrDefault(arrival) is { Latitude: { } toLatitude, Longitude: { } toLongitude }
            ? GreatCircle.DistanceNmRounded(new GeoPoint(fromLatitude, fromLongitude), new GeoPoint(toLatitude, toLongitude))
            : null;
}
