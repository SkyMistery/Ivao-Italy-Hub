using System.Data;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Reports;

/// <summary>What a member's send or a decision of the staff came to: done, refused field by field, nothing there, not theirs, or changed meanwhile.</summary>
public enum ReportResult
{
    Done,
    Refused,
    NotFound,
    Forbidden,
    Changed,
}

/// <summary>
/// One item of a report as the member and the staff read it (§1.8): what was declared — a flight's callsign, airports and day, or a shift's
/// position and hours — and what the check found: the session, the minutes, the distance, whether it is verified (none: not asked). Data
/// only: the page writes the words.
/// </summary>
public sealed record ReportItemDto(
    long Id,
    int Ordinal,
    string Callsign,
    string? DepartureIcao,
    string? ArrivalIcao,
    DateTime? FlightDay,
    DateTime? FromUtc,
    DateTime? ToUtc,
    long? SessionId,
    int? Minutes,
    decimal? DistanceNm,
    bool? Verified)
{
    public static ReportItemDto Of(EventReportItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ReportItemDto(
            item.Id,
            item.Ordinal,
            item.Callsign,
            item.DepartureIcao,
            item.ArrivalIcao,
            item.FlightDay?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            item.FromUtc,
            item.ToUtc,
            item.SessionId,
            item.Minutes,
            item.DistanceNm,
            item.Verified);
    }
}

/// <summary>
/// A member's own report (§5.2, §7.1): its event, its kind, where it stands, what the check made of it, the validator's note, and whether
/// they may still correct it — not decided, and until <see cref="ClosesAtUtc"/>.
/// </summary>
public sealed record MyReportDto(
    long Id,
    long EventId,
    string EventSlug,
    Localized<string> EventTitle,
    ReportKind Kind,
    ReportState State,
    ReportVerification Verification,
    DateTime SubmittedAt,
    DateTime CheckedAt,
    DateTime? DecidedAt,
    string? DecisionNote,
    DateTime ClosesAtUtc,
    bool Correctable,
    IReadOnlyList<ReportItemDto> Items);

/// <summary>
/// An event a member may send a report for now (reading 3): its head, until when, and its airports — a flight touches one of them, unless the
/// event is of the whole division.
/// </summary>
public sealed record ReportableEventDto(
    long Id,
    string Slug,
    Localized<string> Title,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime ClosesAtUtc,
    bool WholeDivision,
    IReadOnlyList<string> Airports);

/// <summary>The member's reports, the latest event first, and the events still open to one.</summary>
public sealed record MyReportsDto(IReadOnlyList<MyReportDto> Reports, IReadOnlyList<ReportableEventDto> Open);

/// <summary>
/// A member's reports of support (design M4 §5.2, §5.3, §7.1, E14a; note 2026-10-17-il-pirep-di-supporto, readings 3–6): sending one —
/// a pilot's with their flights, a controller's with their shifts —, sending it again to correct it until it is decided, and reading their
/// own with the events still open to one. Any signed in member, on an event over, published, not cancelled and not in person, until
/// <c>reportDays</c> after its end; nobody else's report, ever.
/// <para>The check (<see cref="ReportCheck"/>) runs first, outside any lock — the network may take seconds —; then, <b>under the lock of the
/// report's row</b>, the report is read again: one decided meanwhile refuses the correction, one not there yet is created, and its items are
/// replaced with the ones just checked. Two first sends of the same member in the same instant meet at the unique index, and the second
/// becomes a correction of the first.</para>
/// </summary>
public sealed class MemberReports(
    EventsDbContext database,
    ReportCheck check,
    IAirportDirectory airports,
    ModuleSettingsStore settingsStore,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>Sends a report of an event, or sends it again to correct it (readings 4–6).</summary>
    public async Task<(ReportResult Result, MyReportDto? Report, bool Created, Refusals Problems)> SendAsync(
        long eventId,
        ReportKind kind,
        ReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Refusals();
        var now = clock.UtcNow;
        var vid = currentUser.Vid;
        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);

        var row = await CrudSource.BackOffice<Event>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        if (row is null || row.Status != PublishStatus.Published || !Enum.IsDefined(kind))
        {
            return (ReportResult.NotFound, null, false, problems);
        }

        if (!ReportRules.IsOpen(row, settings.ReportDays, now))
        {
            return (ReportResult.Refused, null, false, problems.Add("id", ReportRules.ClosedKey));
        }

        // A report decided is told at once, before the check asks the network for nothing.
        if (await database.Reports.AsNoTracking().AnyAsync(
                report => report.EventId == eventId && report.Vid == vid && report.Kind == kind && report.State != ReportState.Submitted,
                cancellationToken))
        {
            return (ReportResult.Refused, null, false, problems.Add("id", ReportRules.DecidedKey));
        }

        var eventAirports = await database.Airports.AsNoTracking()
            .Where(airport => airport.EventId == eventId)
            .Select(airport => airport.Icao)
            .ToListAsync(cancellationToken);
        var codes = (request.Items ?? [])
            .Where(item => item is not null)
            .SelectMany(item => new[] { ReportRules.Code(item.DepartureIcao), ReportRules.Code(item.ArrivalIcao) })
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var known = codes.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await airports.FindAsync(codes, cancellationToken)).Keys.ToHashSet(StringComparer.Ordinal);

        var drafts = ReportRules.Check(kind, row, eventAirports, known, request.Items, problems);
        if (drafts is null)
        {
            return (ReportResult.Refused, null, false, problems);
        }

        var findings = await check.CheckAsync(row, vid, kind, drafts, settings.AttendanceMinimumMinutes, cancellationToken);
        var verification = ReportRules.Verification([.. findings.Select(finding => finding.Verified)]);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var (result, report, created) = await WriteAsync(row, kind, drafts, findings, verification, now, cancellationToken);
                if (result is not ReportResult.Done)
                {
                    return (result, null, false, result == ReportResult.Refused ? problems.Add("id", ReportRules.DecidedKey) : problems);
                }

                var items = await ItemsAsync([report!.Id], cancellationToken);
                return (ReportResult.Done, Mine(report, row, settings.ReportDays, items[report.Id], now), created, problems);
            }
            catch (Exception exception) when (attempt == 0 && DatabaseErrors.Duplicated(exception))
            {
                // The same member sent the same kind from another window in the same instant, and the unique index answered: their report is
                // there now, and this request is a correction of it.
                database.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>
    /// The member's reports and the events still open to one (reading 3). The reports by their event's start, the latest first; the events by
    /// their end, the latest first.
    /// </summary>
    public async Task<MyReportsDto> MineAsync(CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var now = clock.UtcNow;
        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);

        var reports = await database.Reports.AsNoTracking().Where(report => report.Vid == vid).ToListAsync(cancellationToken);
        var eventIds = reports.Select(report => report.EventId).Distinct().ToList();
        var events = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var items = await ItemsAsync([.. reports.Select(report => report.Id)], cancellationToken);

        // The events open now: over, and their end within reportDays — every one of them, not only those the member booked (choice 2).
        var since = now.AddDays(-settings.ReportDays);
        var open = (await CrudSource.BackOffice<Event>(database).AsNoTracking()
                .Where(row => row.Status == PublishStatus.Published
                    && row.CancelledAt == null
                    && !row.InPerson
                    && row.EndsAtUtc <= now
                    && row.EndsAtUtc > since)
                .OrderByDescending(row => row.EndsAtUtc)
                .ThenBy(row => row.Id)
                .ToListAsync(cancellationToken))
            .Where(row => ReportRules.IsOpen(row, settings.ReportDays, now))
            .ToList();
        var openIds = open.Select(row => row.Id).ToList();
        var openAirports = (await database.Airports.AsNoTracking()
                .Where(airport => openIds.Contains(airport.EventId))
                .OrderBy(airport => airport.Icao)
                .ToListAsync(cancellationToken))
            .ToLookup(airport => airport.EventId, airport => airport.Icao);

        return new MyReportsDto(
            [
                .. reports
                    .Where(report => events.ContainsKey(report.EventId))
                    .OrderByDescending(report => events[report.EventId].StartsAtUtc)
                    .ThenBy(report => report.Kind)
                    .Select(report => Mine(report, events[report.EventId], settings.ReportDays, items[report.Id], now)),
            ],
            [
                .. open.Select(row => new ReportableEventDto(
                    row.Id,
                    row.Slug,
                    row.Title,
                    row.StartsAtUtc,
                    row.EndsAtUtc,
                    ReportRules.ClosesAt(row, settings.ReportDays),
                    row.WholeDivision,
                    [.. openAirports[row.Id]])),
            ]);
    }

    /// <summary>The items of these reports, each report's in its order.</summary>
    internal async Task<ILookup<long, ReportItemDto>> ItemsAsync(IReadOnlyCollection<long> reportIds, CancellationToken cancellationToken) =>
        (await CrudSource.BackOffice<EventReportItem>(database).AsNoTracking()
            .Where(item => reportIds.Contains(item.ReportId))
            .OrderBy(item => item.Ordinal)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken))
        .ToLookup(item => item.ReportId, ReportItemDto.Of);

    /// <summary>
    /// The report and its items written under the lock of its row: created when there is none, refused when it was decided meanwhile, and its
    /// items replaced — each deleted and added with its row in the audit.
    /// </summary>
    private async Task<(ReportResult Result, EventReport? Report, bool Created)> WriteAsync(
        Event row,
        ReportKind kind,
        IReadOnlyList<ReportItemDraft> drafts,
        IReadOnlyList<ItemFinding> findings,
        ReportVerification verification,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var word = kind.ToString();

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_reports WHERE event_id = {row.Id} AND vid = {vid} AND kind = {word} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var report = await database.Reports.FirstOrDefaultAsync(
            candidate => candidate.EventId == row.Id && candidate.Vid == vid && candidate.Kind == kind,
            cancellationToken);
        var created = report is null;
        if (report is null)
        {
            // In the event's care: the departments and the scope of every row of the event (§1.1).
            report = new EventReport
            {
                EventId = row.Id,
                Vid = vid,
                Kind = kind,
                Source = ReportSource.Manual,
                State = ReportState.Submitted,
                CreatedAt = now,
                OwnerDepartment = row.OwnerDepartment,
                OwnerDepartmentMask = row.OwnerDepartmentMask,
            };
            database.Reports.Add(report);
        }
        else if (report.State != ReportState.Submitted)
        {
            return (ReportResult.Refused, null, false);
        }
        else
        {
            database.ReportItems.RemoveRange(await database.ReportItems
                .Where(item => item.ReportId == report.Id)
                .ToListAsync(cancellationToken));
        }

        report.Verification = verification;
        report.SubmittedAt = now;
        report.CheckedAt = now;
        report.UpdatedAt = now;

        // A new report has its id at the first save: its items follow in the same transaction.
        await database.SaveChangesAsync(cancellationToken);

        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            var finding = findings[index];
            database.ReportItems.Add(new EventReportItem
            {
                ReportId = report.Id,
                EventId = row.Id,
                Vid = vid,
                Ordinal = index,
                Callsign = draft.Callsign,
                DepartureIcao = draft.DepartureIcao,
                ArrivalIcao = draft.ArrivalIcao,
                FlightDay = draft.FlightDay,
                FromUtc = draft.FromUtc,
                ToUtc = draft.ToUtc,
                SessionId = finding.SessionId,
                Minutes = finding.Minutes,
                DistanceNm = finding.DistanceNm,
                Verified = finding.Verified,
                OwnerDepartment = report.OwnerDepartment,
                OwnerDepartmentMask = report.OwnerDepartmentMask,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (ReportResult.Done, report, created);
    }

    private static MyReportDto Mine(EventReport report, Event row, int reportDays, IEnumerable<ReportItemDto> items, DateTime now) =>
        new(
            report.Id,
            row.Id,
            row.Slug,
            row.Title,
            report.Kind,
            report.State,
            report.Verification,
            report.SubmittedAt,
            report.CheckedAt,
            report.DecidedAt,
            report.DecisionNote,
            ReportRules.ClosesAt(row, reportDays),
            report.State == ReportState.Submitted && ReportRules.IsOpen(row, reportDays, now),
            [.. items]);
}
