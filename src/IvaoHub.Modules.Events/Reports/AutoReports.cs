using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.After;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Reports;

/// <summary>
/// What an event's <c>Auto</c> reports look at (design M4 §5.1): the airports a flight touches — the event's own, the division's for an event of
/// the whole division, or <see langword="null"/> for any flight when the event has none —, and the positions a controller's shifts are on —
/// its own and the division's at its airports, or every position of the division for an event of the whole division. Upper case.
/// </summary>
public sealed record AutoReportScope(IReadOnlySet<string>? Airports, IReadOnlySet<string> Positions);

/// <summary>
/// The reports of support the system writes for a member (design M4 §1.11, §5.1, §5.2, E14b; note 2026-10-17-le-regole-di-award): for whoever
/// switched on «report my participation in events automatically», <c>events-after</c> looks for their sessions in the event's window and, when
/// there are some, writes their report — a pilot's with the flights, a controller's with the shifts —, <see cref="ReportSource.Auto"/>, with
/// what the check found, waiting for a validator like any other. One the member sent by hand is never touched: the unique index and the writer
/// keep it (<see cref="MemberReports"/>).
/// <para>Only the members with the preference are asked about (§5.1): never a search of every airport of the division.</para>
/// </summary>
public sealed class AutoReports(
    EventsDbContext database,
    HubDbContext hub,
    INetworkSessionSource network,
    IAtcPositionDirectory positions,
    IAirportDirectory airports,
    IFirDirectory firs)
{
    /// <summary>The preference, off until the member switches it on (§1.11).</summary>
    public const string PreferenceKey = $"{EventsModule.ModuleKey}.autoReport";

    /// <summary>The value of the preference switched on, as the core keeps it.</summary>
    public const string On = "true";

    /// <summary>What a member costs a run of <c>events-after</c>: a question for their flights, one for their shifts.</summary>
    public const int Cost = 2;

    /// <summary>The members with the preference after <paramref name="afterVid"/>, in the order of their VIDs: the ones still to look at.</summary>
    public async Task<IReadOnlyList<int>> HoldersAsync(int? afterVid, CancellationToken cancellationToken)
    {
        var after = afterVid ?? 0;

        return await hub.UserPreferences.AsNoTracking()
            .Where(preference => preference.Key == PreferenceKey && preference.ValueJson == On && preference.Vid > after)
            .OrderBy(preference => preference.Vid)
            .Select(preference => preference.Vid)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The positions of <paramref name="row"/> (reading 8): read once per event, before its members are looked at.</summary>
    public async Task<AutoReportScope> ScopeAsync(Event row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var division = await positions.OfDivisionAsync(cancellationToken);
        if (row.WholeDivision)
        {
            // The division's airports, as the core counts the division's traffic: the airspace of its snapshot.
            var airspace = await firs.GetAirspaceAsync(cancellationToken);
            return new AutoReportScope(
                airspace.Airports.Select(icao => icao.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal),
                division.Select(position => position.Callsign.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal));
        }

        var eventAirports = (await database.Airports.AsNoTracking()
                .Where(airport => airport.EventId == row.Id)
                .Select(airport => airport.Icao)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var own = await database.AtcPositions.AsNoTracking()
            .Where(position => position.EventId == row.Id)
            .Select(position => position.Callsign)
            .ToListAsync(cancellationToken);

        return new AutoReportScope(
            eventAirports.Count == 0 ? null : eventAirports.Select(icao => icao.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal),
            own
            .Concat(division.Where(position => position.AirportIcao is { } icao && eventAirports.Contains(icao)).Select(position => position.Callsign))
            .Select(callsign => callsign.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Looks for one member's sessions in the event's window and writes the reports they make, those not there already. False when the network
    /// did not answer: nothing is written for them, and the next run asks again.
    /// </summary>
    public async Task<bool> WriteAsync(
        Event row,
        int vid,
        AutoReportScope scope,
        int attendanceMinimumMinutes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(scope);

        var (fromUtc, toUtc) = AfterRules.FlightWindow(row);

        // A controller's sessions start up to half a day before the window and still count inside it, as a shift's do (E13a).
        var controlled = await network.SessionsAsync(
            new NetworkSessionQuery(vid, fromUtc - AfterRules.ConnectedBefore, toUtc, Connection: NetworkConnection.Atc),
            cancellationToken);
        if (controlled is null)
        {
            return false;
        }

        // A pilot's sessions that started in the window, with the airports of their flight plans (E10v).
        var flown = await network.SessionsAsync(new NetworkSessionQuery(vid, fromUtc, toUtc, Connection: NetworkConnection.Pilot), cancellationToken);
        if (flown is null)
        {
            return false;
        }

        var codes = flown.SelectMany(session => new[] { session.DepartureIcao, session.ArrivalIcao }).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var known = codes.Count == 0 ? new Dictionary<string, AirportDto>() : await airports.FindAsync(codes, cancellationToken);
        var flights = AutoReportRules.Flights(flown, scope.Airports, fromUtc, toUtc, (departure, arrival) => ReportCheck.Distance(departure, arrival, known));
        if (flights.Count > 0)
        {
            await WriteAsync(row, vid, ReportKind.Pilot, flights, now, cancellationToken);
        }

        var shifts = AutoReportRules.Shifts(controlled, scope.Positions, fromUtc, toUtc, attendanceMinimumMinutes);
        if (shifts.Count > 0)
        {
            await WriteAsync(row, vid, ReportKind.Atc, shifts, now, cancellationToken);
        }

        return true;
    }

    private async Task WriteAsync(
        Event row,
        int vid,
        ReportKind kind,
        IReadOnlyList<(ReportItemDraft Draft, ItemFinding Finding)> items,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var findings = items.Select(item => item.Finding).ToList();
        var verification = ReportRules.Verification([.. findings.Select(finding => finding.Verified)]);
        try
        {
            await MemberReports.WriteAsync(
                database, row, vid, kind, ReportSource.Auto, [.. items.Select(item => item.Draft)], findings, verification, now, cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.Duplicated(exception))
        {
            // The member sent theirs in the same instant: the unique index answered, and theirs stays.
            database.ChangeTracker.Clear();
        }
    }
}

/// <summary>
/// The rules of the <c>Auto</c> reports that need nothing but their values (E14b; note 2026-10-17-le-regole-di-award, reading 8): which sessions
/// make which items. Pure, and tested as such.
/// </summary>
public static class AutoReportRules
{
    /// <summary>
    /// A pilot's flights (reading 8): the sessions that started in the event's window whose flight plan says two different airports (the last
    /// revision, as the core gives them, E10v) and touches <paramref name="airports"/> — any flight when it is null (an event without airports
    /// of its own, not of the whole division) —, one item each, the earliest first, at most <see cref="ReportRules.MaxItems"/>; verified, with
    /// the session, its minutes and the distance between the two airports when the hub knows them.
    /// </summary>
    public static IReadOnlyList<(ReportItemDraft Draft, ItemFinding Finding)> Flights(
        IEnumerable<NetworkSession> sessions,
        IReadOnlySet<string>? airports,
        DateTime fromUtc,
        DateTime toUtc,
        Func<string, string, decimal?> distance)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(distance);

        return [.. sessions
            .Where(session => session is { DepartureIcao: { Length: > 0 }, ArrivalIcao: { Length: > 0 } }
                && session.DepartureIcao != session.ArrivalIcao
                && session.StartedAt >= fromUtc
                && session.StartedAt <= toUtc
                && (airports is null || airports.Contains(session.DepartureIcao!) || airports.Contains(session.ArrivalIcao!)))
            .OrderBy(session => session.StartedAt)
            .ThenBy(session => session.Id)
            .Take(ReportRules.MaxItems)
            .Select(session => (
                Draft: new ReportItemDraft(
                    session.Callsign.Trim().ToUpperInvariant(),
                    session.DepartureIcao,
                    session.ArrivalIcao,
                    DateOnly.FromDateTime(session.StartedAt),
                    null,
                    null),
                Finding: new ItemFinding(
                    session.Id,
                    (int)(session.EndedAt - session.StartedAt).TotalMinutes,
                    distance(session.DepartureIcao!, session.ArrivalIcao!),
                    Verified: true)))];
    }

    /// <summary>
    /// A controller's shifts (reading 8): their connections on the positions of <paramref name="positions"/>, cut to the event's window, one
    /// item per position from the first minute to the last — at most <see cref="ReportRules.LongestShift"/>, at most
    /// <see cref="ReportRules.MaxItems"/> items, the earliest first —, with the minutes on it. Verified with at least
    /// <paramref name="attendanceMinimumMinutes"/> on it, as a shift of the roster is: a connection of a few minutes is in the report, not
    /// verified.
    /// </summary>
    public static IReadOnlyList<(ReportItemDraft Draft, ItemFinding Finding)> Shifts(
        IEnumerable<NetworkSession> sessions,
        IReadOnlySet<string> positions,
        DateTime fromUtc,
        DateTime toUtc,
        int attendanceMinimumMinutes)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(positions);

        return [.. sessions
            .Where(session => positions.Contains(session.Callsign.ToUpperInvariant())
                && session.EndedAt > fromUtc
                && session.StartedAt < toUtc)
            .GroupBy(session => session.Callsign.ToUpperInvariant())
            .Select(position =>
            {
                var from = position.Min(session => session.StartedAt < fromUtc ? fromUtc : session.StartedAt);
                var last = position.Max(session => session.EndedAt > toUtc ? toUtc : session.EndedAt);
                var to = last - from > ReportRules.LongestShift ? from + ReportRules.LongestShift : last;
                var minutes = AfterRules.MinutesWithin(position.Select(AfterRules.Connection), from, to);
                return (Draft: new ReportItemDraft(position.Key, null, null, null, from, to),
                    Finding: new ItemFinding(null, minutes, null, minutes >= attendanceMinimumMinutes));
            })
            .Where(item => item.Draft.ToUtc > item.Draft.FromUtc)
            .OrderBy(item => item.Draft.FromUtc)
            .ThenBy(item => item.Draft.Callsign, StringComparer.Ordinal)
            .Take(ReportRules.MaxItems)];
    }
}
