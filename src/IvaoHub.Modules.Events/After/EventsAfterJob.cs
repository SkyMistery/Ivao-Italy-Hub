using System.Data;
using System.Globalization;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Reports;
using IvaoHub.Modules.Events.Settings;
using IvaoHub.Modules.Events.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.Events.After;

/// <summary>
/// <c>events-after</c> (design M4 §4.5, §5.1, §8.4, §10.2, E13a; note 2026-10-16-le-verifiche-dopo-l-evento): every hour, the checks after an
/// event, in batches that end inside one request.
/// <list type="bullet">
/// <item><b>The bookings</b>: for each, the pilot's sessions on the tracker between its two airports in the window of the flights; the one
/// nearest to its departure sets <c>flown_at</c>, the callsign does not count (reading 2). <c>flown_checked_at</c> says it was looked at,
/// found or not.</item>
/// <item><b>The shifts</b> still <c>Unknown</c>: the archive of the sessions, or the tracker by VID where the archive cannot say; at least
/// <c>attendanceMinimumMinutes</c> on the position within the shift is <c>Attended</c>, less a <c>NoShowProposed</c> with who covered the
/// position when the archive says so (readings 4 and 5).</item>
/// <item><b>Closing the event</b>, when no row is left to check: who flew without booking — a number of people, whose VIDs live only in
/// memory while they are counted (reading 3) —, as many airports per run as its budget holds, each piece saved with the statistics; once every
/// airport is counted, the statistics (<see cref="EventStatistics"/>) and <c>after_done_at</c> in one transaction.</item>
/// </list>
/// <para><b>A batch</b> (reading 6): a run spends at most <see cref="Budget"/> units, about the seconds of the network's time — a question by
/// VID costs <see cref="ByVidCost"/>, one about an airport <see cref="ByAirportCost"/> —, and what is left waits for the next run, which
/// finds it from the rows. <b>A source that does not answer</b> (reading 7) is asked no more in the run, and its rows stay to check; after
/// <see cref="AfterRules.GiveUpAfter"/> an event is closed with them left as they are, never marked.</para>
/// <para><b>It decides from its data, never from the hour it runs</b> (note 2026-09-28-i-job-quando-passenger-spegne-l-hub §8): every event
/// due with <c>after_done_at</c> empty, every row with its check empty, whenever it runs, so a run lost while the hub slept is made up by the
/// next, which the core's catch up runs after a start (E10j), and a run cut half way kept every row it saved. Its row in
/// <c>hub_jobs_log</c> says when it finished on every way out. It never throws; an event that fails is logged, named in the row's message and
/// left for the next run.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class EventsAfterJob(
    EventsDbContext database,
    HubDbContext hub,
    ModuleSettingsStore settingsStore,
    INetworkSessionSource network,
    IAtcActivitySource archive,
    EventStatistics statistics,
    AutoReports autoReports,
    IClock clock,
    ILogger<EventsAfterJob> logger) : IJob
{
    public const string JobName = "events-after";

    /// <summary>Its trigger, by name: a test reads its zone and its cron.</summary>
    public const string TriggerName = $"{JobName}-hourly";

    /// <summary>Every hour at seven minutes past, off the minutes the other jobs of the events run on. In UTC, which the trigger says.</summary>
    public const string Cron = "0 7 * * * ?";

    /// <summary>What a run spends at most (reading 6): about the seconds of the network's time a run can take inside the 80 the scheduled request waits.</summary>
    public const int Budget = 60;

    /// <summary>A question about one person — the tracker by VID, the archive of the sessions.</summary>
    public const int ByVidCost = 1;

    /// <summary>A question about an airport without a VID: the page with its last row takes the network about ten seconds and a half (E10a).</summary>
    public const int ByAirportCost = 11;

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>What a run did: the events it looked at, the bookings and the shifts it checked, the events it closed.</summary>
    public sealed record Outcome(int Events, int Bookings, int Shifts, int Closed);

    public async Task<Outcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var now = entry.StartedAt;
            var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);

            // The database narrows to the events whose moment passed; the rule itself is read in one place (AfterRules.IsDue).
            var endedBy = now - SlotWindow.Margin;
            var due = (await CrudSource.BackOffice<Event>(database).AsNoTracking()
                    .Where(row => row.Status == PublishStatus.Published
                        && row.CancelledAt == null
                        && row.AfterDoneAt == null
                        && row.EndsAtUtc <= endedBy)
                    .OrderBy(row => row.EndsAtUtc)
                    .ThenBy(row => row.Id)
                    .ToListAsync(cancellationToken))
                .Where(row => AfterRules.IsDue(row, now))
                .ToList();

            var run = new Run();
            var outcome = new Outcome(0, 0, 0, 0);
            var failed = new List<long>();
            foreach (var row in due)
            {
                try
                {
                    var (bookings, shifts, closed) = await CheckAsync(row, settings, run, now, cancellationToken);
                    outcome = new Outcome(
                        outcome.Events + 1,
                        outcome.Bookings + bookings,
                        outcome.Shifts + shifts,
                        outcome.Closed + (closed ? 1 : 0));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "The checks after the event {EventId} could not be done.", row.Id);
                    database.ChangeTracker.Clear();
                    failed.Add(row.Id);
                }
            }

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            var done = string.Create(
                CultureInfo.InvariantCulture,
                $"{outcome.Events} event(s) looked at: {outcome.Bookings} booking(s) and {outcome.Shifts} shift(s) checked, {outcome.Closed} closed; {run.Spent} of {Budget} unit(s) spent");
            if (run.TrackerDown)
            {
                done += "; the tracker did not answer";
            }

            if (run.ArchiveDown)
            {
                done += "; the archive of the sessions did not answer";
            }

            entry.Message = failed.Count == 0
                ? done
                : string.Create(CultureInfo.InvariantCulture, $"{done}; not done, left for the next run: {string.Join(", ", failed)}");
            if (entry.Message.Length > MaxMessageLength)
            {
                entry.Message = entry.Message[..MaxMessageLength];
            }

            await hub.SaveChangesAsync(cancellationToken);
            return outcome;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The events after job failed.");

            database.ChangeTracker.Clear();
            hub.ChangeTracker.Clear();
            hub.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength ? exception.Message : exception.Message[..MaxMessageLength];
            await hub.SaveChangesAsync(cancellationToken);

            return new Outcome(0, 0, 0, 0);
        }
    }

    /// <summary>
    /// One event: its bookings, its shifts, and — no row left to check, or the sources given up — its closing. Returns what it checked and
    /// whether it closed.
    /// </summary>
    private async Task<(int Bookings, int Shifts, bool Closed)> CheckAsync(
        Event row,
        EventsSettings settings,
        Run run,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var givenUp = AfterRules.IsGivenUp(row, now);
        var bookings = givenUp ? 0 : await BookingsAsync(row, run, now, cancellationToken);
        var shifts = givenUp ? 0 : await ShiftsAsync(row, settings, run, cancellationToken);

        // A row of a person whose data was erased after the end is never asked, and does not keep the event open (#322, item 5).
        var left = await CrudSource.BackOffice<EventBooking>(database)
                .AnyAsync(booking => booking.EventId == row.Id && booking.FlownCheckedAt == null && booking.BookerVid > 0, cancellationToken)
            || await CrudSource.BackOffice<EventAtcShift>(database)
                .AnyAsync(shift => shift.EventId == row.Id && shift.Attendance == ShiftAttendance.Unknown && shift.ControllerVid > 0, cancellationToken);
        if (left && !givenUp)
        {
            return (bookings, shifts, false);
        }

        // The reports of the members who asked for them (§5.1, E14b), as many members as the run has left, in the order of their VIDs; the event
        // closes once every one of them is looked at. Never once the sources are given up: whoever asked sends theirs by hand.
        if (!givenUp && !await AutoReportsAsync(row, settings, run, now, cancellationToken))
        {
            return (bookings, shifts, false);
        }

        // Who flew without booking (readings 3 and 6, with Carmine's conditions on #272): as many airports as the run has left, in their
        // order, each run's piece saved with the statistics; the event closes once every airport is counted. Never for an event of the whole
        // division, nor one without airports, nor once the sources are given up: then it stays «not counted», or counted at the airports done.
        if (!givenUp && !row.WholeDivision && !await CountUnbookedAsync(row, run, now, cancellationToken))
        {
            return (bookings, shifts, false);
        }

        // The statistics and the mark in one transaction: a failure takes both back, and the next run closes the event again.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await statistics.WriteAsync(row, now, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        // The job's bookkeeping, as the roster's marks: the row version of the event does not move, and its audit has no row for it.
        await database.Database.ExecuteSqlAsync(
            $"UPDATE evt_events SET after_done_at = {now}, row_version = row_version WHERE id = {row.Id} AND after_done_at IS NULL",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return (bookings, shifts, true);
    }

    /// <summary>
    /// The bookings of an event not checked yet, by the time of their departure (reading 2): each saved as soon as it is checked, so a run cut
    /// half way keeps what it did. Two bookings of one pilot on one route share the question, and never the session.
    /// <para>Never a booking of a pilot whose data was erased between the end and the check (#322, item 5): the network knows them by the VID
    /// the hub no longer has, so the pseudonym would find nothing and read «booked, not flown». It stays not checked — «we could not look» —,
    /// which the statistics count among the booked and never among the not flown.</para>
    /// </summary>
    private async Task<int> BookingsAsync(Event row, Run run, DateTime now, CancellationToken cancellationToken)
    {
        var bookings = await CrudSource.BackOffice<EventBooking>(database)
            .Where(booking => booking.EventId == row.Id && booking.FlownCheckedAt == null && booking.BookerVid > 0)
            .ToListAsync(cancellationToken);
        if (bookings.Count == 0)
        {
            return 0;
        }

        var slotIds = bookings.Select(booking => booking.SlotId).Distinct().ToList();
        var slots = await database.Slots.AsNoTracking()
            .Where(slot => slotIds.Contains(slot.Id))
            .ToDictionaryAsync(slot => slot.Id, cancellationToken);
        var taken = (await CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
                .Where(booking => booking.EventId == row.Id && booking.FlownSessionId != null)
                .Select(booking => booking.FlownSessionId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var (fromUtc, toUtc) = AfterRules.FlightWindow(row);
        var asked = new Dictionary<(int Vid, string Departure, string Arrival), IReadOnlyList<NetworkSession>>();
        var checkedCount = 0;

        var ordered = bookings
            .Select(booking => (Booking: booking, Flight: slots.TryGetValue(booking.SlotId, out var slot) ? BookedFlight.Of(slot, booking) : null))
            .OrderBy(pair => pair.Flight?.OffBlockUtc ?? pair.Flight?.OnBlockUtc ?? DateTime.MaxValue)
            .ThenBy(pair => pair.Booking.Id)
            .ToList();

        foreach (var (booking, flight) in ordered)
        {
            if (run.TrackerDown)
            {
                break;
            }

            NetworkSession? session = null;

            // A flight with both its airports is looked for; one that has not — a private slot whose pilot's half is gone — was never a
            // flight the network could show, and is checked as not found.
            if (flight is { DepartureIcao: { Length: > 0 } departure, ArrivalIcao: { Length: > 0 } arrival })
            {
                var key = (booking.BookerVid, departure, arrival);
                if (!asked.TryGetValue(key, out var found))
                {
                    if (!run.Take(ByVidCost))
                    {
                        break;
                    }

                    var answer = await network.SessionsAsync(
                        new NetworkSessionQuery(booking.BookerVid, fromUtc, toUtc, departure, arrival, NetworkConnection.Pilot),
                        cancellationToken);
                    if (answer is null)
                    {
                        run.TrackerDown = true;
                        break;
                    }

                    found = answer;
                    asked[key] = found;
                }

                session = AfterRules.Flown(flight, found, taken);
            }

            booking.FlownCheckedAt = now;
            if (session is not null)
            {
                booking.FlownAt = session.StartedAt;
                booking.FlownSessionId = session.Id;
                taken.Add(session.Id);
            }

            await database.SaveChangesAsync(cancellationToken);
            checkedCount++;
        }

        return checkedCount;
    }

    /// <summary>
    /// The shifts of an event still <c>Unknown</c> (readings 4 and 5), each saved as soon as it is decided: the archive first, the tracker by VID
    /// where the archive does not answer or does not cover the shift. A shift neither source can say stays <c>Unknown</c>, and so does one of a
    /// controller whose data was erased between the end and the check (#322, item 5): never asked by the pseudonym, nor given as covered to
    /// whoever the archive finds on the position — the person themselves, by the VID the hub no longer has.
    /// </summary>
    private async Task<int> ShiftsAsync(Event row, EventsSettings settings, Run run, CancellationToken cancellationToken)
    {
        var shifts = await CrudSource.BackOffice<EventAtcShift>(database)
            .Where(shift => shift.EventId == row.Id && shift.Attendance == ShiftAttendance.Unknown && shift.ControllerVid > 0)
            .OrderBy(shift => shift.FromUtc)
            .ThenBy(shift => shift.Id)
            .ToListAsync(cancellationToken);
        if (shifts.Count == 0)
        {
            return 0;
        }

        var positionIds = shifts.Select(shift => shift.PositionId).Distinct().ToList();
        var callsigns = await database.AtcPositions.AsNoTracking()
            .Where(position => positionIds.Contains(position.Id))
            .ToDictionaryAsync(position => position.Id, position => position.Callsign, cancellationToken);

        var decided = 0;
        foreach (var shift in shifts)
        {
            if (!callsigns.TryGetValue(shift.PositionId, out var callsign))
            {
                continue;
            }

            if (await AttendanceAsync(shift, callsign, settings, run, cancellationToken) is not { } outcome)
            {
                if (run.TrackerDown || run.Spent >= Budget)
                {
                    break;
                }

                continue;
            }

            shift.Attendance = outcome.Attendance;
            shift.CoveredByVid = outcome.CoveredBy;
            await database.SaveChangesAsync(cancellationToken);
            decided++;
        }

        return decided;
    }

    /// <summary>How one shift went, or null when neither source could say (or the run has nothing left to ask with).</summary>
    private async Task<(ShiftAttendance Attendance, int? CoveredBy)?> AttendanceAsync(
        EventAtcShift shift,
        string callsign,
        EventsSettings settings,
        Run run,
        CancellationToken cancellationToken)
    {
        var threshold = AfterRules.Threshold(settings.AttendanceMinimumMinutes, shift.FromUtc, shift.ToUtc);

        // A division with no archive (atcData: none) is not asked at all: «not available» by contract, no unit spent, nothing to report in the
        // run's row (the review of #278, point 4).
        if (!run.ArchiveDown && archive is not UnavailableAtcActivitySource)
        {
            if (!run.Take(ByVidCost))
            {
                return null;
            }

            var activity = await archive.SessionsOfAsync(shift.ControllerVid, shift.FromUtc, shift.ToUtc, cancellationToken);
            if (activity is null)
            {
                run.ArchiveDown = true;
            }
            else
            {
                var minutes = AfterRules.MinutesWithin(activity.Of(callsign).Select(AfterRules.Connection), shift.FromUtc, shift.ToUtc);
                if (minutes >= threshold)
                {
                    return (ShiftAttendance.Attended, null);
                }

                // The archive says «not there» only where it covers the shift (E10b): then whoever covered is asked of it too.
                if (activity.Covers(callsign, shift.FromUtc))
                {
                    if (!run.Take(ByVidCost))
                    {
                        return null;
                    }

                    var online = await archive.OnlineAsync(shift.FromUtc, shift.ToUtc, cancellationToken);
                    return (ShiftAttendance.NoShowProposed,
                        online is null ? null : AfterRules.CoveredBy(online.Of(callsign), shift.ControllerVid, shift.FromUtc, shift.ToUtc));
                }
            }
        }

        if (run.TrackerDown || !run.Take(ByVidCost))
        {
            return null;
        }

        var sessions = await network.SessionsAsync(
            new NetworkSessionQuery(shift.ControllerVid, shift.FromUtc - AfterRules.ConnectedBefore, shift.ToUtc, Connection: NetworkConnection.Atc),
            cancellationToken);
        if (sessions is null)
        {
            run.TrackerDown = true;
            return null;
        }

        var onThePosition = sessions
            .Where(session => string.Equals(session.Callsign, callsign, StringComparison.OrdinalIgnoreCase))
            .Select(AfterRules.Connection);

        // The tracker is not asked by callsign without a VID: who covered stays unknown (reading 5).
        return AfterRules.MinutesWithin(onThePosition, shift.FromUtc, shift.ToUtc) >= threshold
            ? (ShiftAttendance.Attended, null)
            : (ShiftAttendance.NoShowProposed, null);
    }

    /// <summary>
    /// Who flew to or from some of the event's airports in its window without booking it (reading 3): the pilots of the sessions found without
    /// a VID at <paramref name="airports"/>, counted once each, less whoever booked a slot of the event. <b>No VID and no session is kept</b>:
    /// they live in this method alone. Null when the tracker did not answer.
    /// </summary>
    private async Task<int?> FlownUnbookedAsync(Event row, IReadOnlyList<string> airports, Run run, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = AfterRules.FlightWindow(row);
        var pilots = new HashSet<int>();
        var cut = false;
        foreach (var icao in airports)
        {
            foreach (var query in new[]
            {
                new NetworkSessionQuery(null, fromUtc, toUtc, DepartureIcao: icao, Connection: NetworkConnection.Pilot) { Limit = NetworkSessionQuery.MaxLimit },
                new NetworkSessionQuery(null, fromUtc, toUtc, ArrivalIcao: icao, Connection: NetworkConnection.Pilot) { Limit = NetworkSessionQuery.MaxLimit },
            })
            {
                var found = await network.SessionsAsync(query, cancellationToken);
                if (found is null)
                {
                    run.TrackerDown = true;
                    return null;
                }

                cut |= found.Count >= NetworkSessionQuery.MaxLimit;
                pilots.UnionWith(found.Select(session => session.Vid));
            }
        }

        var bookers = await CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
            .Where(booking => booking.EventId == row.Id)
            .Select(booking => booking.BookerVid)
            .Distinct()
            .ToListAsync(cancellationToken);
        pilots.ExceptWith(bookers);

        if (cut)
        {
            logger.LogWarning("The flights without a booking of the event {EventId} reached the most the tracker is read for: the count is a least.", row.Id);
        }

        return pilots.Count;
    }
    /// <summary>
    /// Counts who flew without booking at the event's next airports, as many as the run's budget holds (two questions each), and saves the
    /// piece with the statistics. True when every airport is counted — or there is none —; false when some are left for the next run, or the
    /// tracker did not answer.
    /// </summary>
    private async Task<bool> CountUnbookedAsync(Event row, Run run, DateTime now, CancellationToken cancellationToken)
    {
        var airports = await database.Airports.AsNoTracking()
            .Where(airport => airport.EventId == row.Id)
            .OrderBy(airport => airport.Ordinal)
            .ThenBy(airport => airport.Id)
            .Select(airport => airport.Icao)
            .ToListAsync(cancellationToken);
        var done = await CrudSource.BackOffice<EventStats>(database).AsNoTracking()
            .Where(stats => stats.EventId == row.Id)
            .Select(stats => stats.FlownUnbookedAirports)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
        if (done >= airports.Count)
        {
            return true;
        }

        var piece = airports.Skip(done).Take((Budget - run.Spent) / (2 * ByAirportCost)).ToList();
        if (piece.Count == 0 || run.TrackerDown || !run.Take(piece.Count * 2 * ByAirportCost))
        {
            return false;
        }

        if (await FlownUnbookedAsync(row, piece, run, cancellationToken) is not { } pilots)
        {
            return false;
        }

        await using (var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            await statistics.WriteAsync(row, now, cancellationToken, (pilots, piece.Count));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        database.ChangeTracker.Clear();
        return done + piece.Count >= airports.Count;
    }

    /// <summary>
    /// The <c>Auto</c> reports of an event (§5.1, E14b): for each member with the preference not looked at yet, in the order of their VIDs, as many
    /// as the run's budget holds (<see cref="AutoReports.Cost"/> each), their reports written and the member marked done on the event
    /// (<see cref="Event.AutoReportsCursor"/>). True when every member is done; false when some are left for the next run, or the tracker did
    /// not answer. An event in person has no flights nor shifts to report.
    /// </summary>
    private async Task<bool> AutoReportsAsync(Event row, EventsSettings settings, Run run, DateTime now, CancellationToken cancellationToken)
    {
        if (row.InPerson)
        {
            return true;
        }

        var holders = await autoReports.HoldersAsync(row.AutoReportsCursor, cancellationToken);
        if (holders.Count == 0)
        {
            return true;
        }

        var scope = await autoReports.ScopeAsync(row, cancellationToken);
        foreach (var vid in holders)
        {
            if (run.TrackerDown || !run.Take(AutoReports.Cost))
            {
                return false;
            }

            if (!await autoReports.WriteAsync(row, vid, scope, settings.AttendanceMinimumMinutes, settings.MinimumFlightMinutes, now, cancellationToken))
            {
                run.TrackerDown = true;
                return false;
            }

            // The job's bookkeeping, as after_done_at: the row version of the event does not move, and its audit has no row for it.
            await database.Database.ExecuteSqlAsync(
                $"UPDATE evt_events SET auto_reports_cursor = {vid}, row_version = row_version WHERE id = {row.Id}",
                cancellationToken);
            database.ChangeTracker.Clear();
        }

        return true;
    }

    /// <summary>What a run has spent, and the sources that stopped answering in it.</summary>
    private sealed class Run
    {
        public int Spent { get; private set; }

        public bool TrackerDown { get; set; }

        public bool ArchiveDown { get; set; }

        /// <summary>Takes <paramref name="cost"/> units if the run has them left: a run never spends more than <see cref="Budget"/>.</summary>
        public bool Take(int cost)
        {
            if (Spent + cost > Budget)
            {
                return false;
            }

            Spent += cost;
            return true;
        }
    }
}
