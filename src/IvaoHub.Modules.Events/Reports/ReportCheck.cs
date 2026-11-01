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

        // The network asked once for the whole report (#322, item 1): the member's flights over the days of its items, then given out to each
        // flight by its day and its two airports — the tracker's own answer to the airports asked, read locally. One question per flight made a
        // report of many legs many calls to a network the whole hub shares, past the members' limit, which counts requests.
        var windows = items
            .Select(item => item is { DepartureIcao: not null, ArrivalIcao: not null, FlightDay: { } day }
                ? ReportRules.DayWindow(row, day)
                : default((DateTime FromUtc, DateTime ToUtc)?))
            .ToList();
        var asked = windows.OfType<(DateTime FromUtc, DateTime ToUtc)>().ToList();
        var sessions = asked.Count == 0
            ? []
            : Whole(await network.SessionsAsync(
                new NetworkSessionQuery(vid, asked.Min(window => window.FromUtc), asked.Max(window => window.ToUtc), Connection: NetworkConnection.Pilot)
                {
                    Limit = NetworkSessionQuery.MaxLimit,
                },
                cancellationToken));

        var flights = new List<(string Callsign, IReadOnlyList<NetworkSession>? Found)>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (windows[index] is not { } window || item is not { DepartureIcao: { } departure, ArrivalIcao: { } arrival })
            {
                flights.Add((item.Callsign, []));
                continue;
            }

            // Only a session whose plan ended on this route flew it: one found for a revision the pilot filed and then changed did not; and one
            // shorter than minimumFlightMinutes flew nothing (E14b, reading 3-bis of note 2026-10-17-le-regole-di-award; Carmine's answer 1 on #293).
            flights.Add((item.Callsign, sessions?.Where(session => session.StartedAt >= window.FromUtc
                && session.StartedAt <= window.ToUtc
                && ReportRules.FlewRoute(session, departure, arrival)
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

    /// <summary>
    /// The member's minutes on each shift's position within its hours, as the check after the event counts them (E13a): the archive where it
    /// covers the hours, the tracker by VID otherwise; none when neither answers. Each source asked once for the whole report (#322, item 1):
    /// the archive over the hours of every shift, the tracker over those of the shifts the archive did not say.
    /// </summary>
    private async Task<IReadOnlyList<ItemFinding>> ShiftsAsync(
        int vid,
        IReadOnlyList<ReportItemDraft> items,
        int attendanceMinimumMinutes,
        CancellationToken cancellationToken)
    {
        var hours = items
            .Select(item => item is { FromUtc: { } fromUtc, ToUtc: { } toUtc }
                ? (FromUtc: fromUtc, ToUtc: toUtc)
                : default((DateTime FromUtc, DateTime ToUtc)?))
            .ToList();
        var asked = hours.OfType<(DateTime FromUtc, DateTime ToUtc)>().ToList();
        var minutes = new int?[items.Count];

        // A division with no archive (atcData: none) is not asked at all, as events-after does not ask it (the review of #278, point 4).
        if (asked.Count > 0
            && archive is not UnavailableAtcActivitySource
            && await archive.SessionsOfAsync(vid, asked.Min(shift => shift.FromUtc), asked.Max(shift => shift.ToUtc), cancellationToken) is { } activity)
        {
            for (var index = 0; index < items.Count; index++)
            {
                if (hours[index] is not { } shift)
                {
                    continue;
                }

                var found = AfterRules.MinutesWithin(activity.Of(items[index].Callsign).Select(AfterRules.Connection), shift.FromUtc, shift.ToUtc);
                if (found > 0 || activity.Covers(items[index].Callsign, shift.FromUtc))
                {
                    minutes[index] = found;
                }
            }
        }

        var unsaid = Enumerable.Range(0, items.Count).Where(index => hours[index] is not null && minutes[index] is null).ToList();
        if (unsaid.Count > 0)
        {
            var fromUtc = unsaid.Min(index => hours[index]!.Value.FromUtc) - AfterRules.ConnectedBefore;
            var toUtc = unsaid.Max(index => hours[index]!.Value.ToUtc);
            var sessions = Whole(await network.SessionsAsync(
                new NetworkSessionQuery(vid, fromUtc, toUtc, Connection: NetworkConnection.Atc) { Limit = NetworkSessionQuery.MaxLimit },
                cancellationToken));

            foreach (var index in sessions is null ? [] : unsaid)
            {
                var shift = hours[index]!.Value;
                minutes[index] = AfterRules.MinutesWithin(
                    sessions!
                        .Where(session => session.StartedAt >= shift.FromUtc - AfterRules.ConnectedBefore
                            && session.StartedAt <= shift.ToUtc
                            && string.Equals(session.Callsign, items[index].Callsign, StringComparison.OrdinalIgnoreCase))
                        .Select(AfterRules.Connection),
                    shift.FromUtc,
                    shift.ToUtc);
            }
        }

        var findings = new List<ItemFinding>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            findings.Add(hours[index] is not { } shift
                ? new ItemFinding(null, null, null, Verified: false)
                : minutes[index] is { } found
                    ? new ItemFinding(null, found, null, found >= AfterRules.Threshold(attendanceMinimumMinutes, shift.FromUtc, shift.ToUtc))
                    : new ItemFinding(null, null, null, Verified: null));
        }

        return findings;
    }

    /// <summary>
    /// An answer of the tracker as the check reads it: one as long as the most a search reads may have been cut, and what is left out could be
    /// the session an item flew — not asked, rather than a guess.
    /// </summary>
    private static IReadOnlyList<NetworkSession>? Whole(IReadOnlyList<NetworkSession>? answer) =>
        answer is { Count: >= NetworkSessionQuery.MaxLimit } ? null : answer;

    /// <summary>The distance between two airports the hub knows with their coordinates, to the tenth; none otherwise.</summary>
    internal static decimal? Distance(string? departure, string? arrival, IReadOnlyDictionary<string, AirportDto> known) =>
        departure is not null
        && arrival is not null
        && known.GetValueOrDefault(departure) is { Latitude: { } fromLatitude, Longitude: { } fromLongitude }
        && known.GetValueOrDefault(arrival) is { Latitude: { } toLatitude, Longitude: { } toLongitude }
            ? GreatCircle.DistanceNmRounded(new GeoPoint(fromLatitude, fromLongitude), new GeoPoint(toLatitude, toLongitude))
            : null;
}
