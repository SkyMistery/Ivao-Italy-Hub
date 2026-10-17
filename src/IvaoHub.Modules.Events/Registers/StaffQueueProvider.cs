using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Registers;

/// <summary>
/// One event of the block <c>events.staffQueue</c> (§7.3, E13b): its head and link, and how many of its rows wait for the reader — no-shows the
/// check proposed, shifts it left unknown when it closed the event, requests to hand a shift over — and, with E14a, reports of support to
/// validate. Numbers: the page writes the words.
/// </summary>
public sealed record StaffQueueEventDto(
    long Id,
    string Slug,
    Localized<string> Title,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int NoShows,
    int ToMark,
    int Transfers,
    int Reports);

/// <summary>
/// The block <c>events.staffQueue</c> (design M4 §7.3, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, reading 11): on the dashboards of
/// the events' and the ATC's staff, what waits for a decision of theirs — a no-show to confirm or excuse, a shift to mark, a request to hand a
/// shift over —, and the reports of support to validate (E14a; note 2026-10-17-il-pirep-di-supporto, reading 10), one row per event, the one
/// ended longest ago first.
/// <para>Always live and the reader's: each row is counted only where the one handler gives them <c>EventAtc.Edit</c> on it — on its event's
/// care and its position's FIR, and never on a row about themselves, the super administrator included —, a report where it gives them
/// <c>EventReports.Edit</c>, never their own. A visitor gets
/// <c>signedIn: false</c>.</para>
/// </summary>
public sealed class StaffQueueProvider(
    EventsDbContext database,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    ICurrentUser currentUser,
    IClock clock) : IDataBlockProvider
{
    public const string BlockType = "events.staffQueue";

    /// <summary>The rows read before the permissions narrow them: a dashboard is not the list.</summary>
    private const int Window = 2000;

    /// <summary>The most events the block shows.</summary>
    public const int MaxEvents = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LocalizedJsonConverterFactory() },
    };

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || http.HttpContext is not { } request)
        {
            return new JsonObject { ["signedIn"] = false };
        }

        var now = clock.UtcNow;

        // The shifts to decide (reading 11): a no-show proposed, or a shift unknown of an event the check closed — before, it is looking at it.
        var shifts = await (
                from shift in CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
                join row in CrudSource.BackOffice<Event>(database) on shift.EventId equals row.Id
                where row.CancelledAt == null
                    && row.EndsAtUtc <= now
                    && (shift.Attendance == ShiftAttendance.NoShowProposed
                        || (shift.Attendance == ShiftAttendance.Unknown && row.AfterDoneAt != null))
                orderby row.EndsAtUtc, shift.Id
                select shift)
            .Take(Window)
            .ToListAsync(cancellationToken);

        // The requests to hand a shift over, as events.atcCoverage counts them (E12).
        var transfers = await CrudSource.BackOffice<EventAtcTransfer>(database).AsNoTracking()
            .Where(transfer => transfer.Status == TransferStatus.Pending)
            .OrderBy(transfer => transfer.Id)
            .Take(Window)
            .ToListAsync(cancellationToken);

        // The reports of support waiting for a validator (E14a), read already narrowed to their state.
        var reports = await CrudSource.BackOffice<EventReport>(database).AsNoTracking()
            .Where(report => report.State == ReportState.Submitted)
            .OrderBy(report => report.Id)
            .Take(Window)
            .ToListAsync(cancellationToken);

        var counts = new Dictionary<long, (int NoShows, int ToMark, int Transfers, int Reports)>();
        foreach (var shift in shifts)
        {
            if ((await authorization.AuthorizeAsync(request.User, shift, EventsPermissions.AtcEdit)).Succeeded)
            {
                var count = counts.GetValueOrDefault(shift.EventId);
                counts[shift.EventId] = shift.Attendance == ShiftAttendance.NoShowProposed
                    ? (count.NoShows + 1, count.ToMark, count.Transfers, count.Reports)
                    : (count.NoShows, count.ToMark + 1, count.Transfers, count.Reports);
            }
        }

        foreach (var transfer in transfers)
        {
            if ((await authorization.AuthorizeAsync(request.User, transfer, EventsPermissions.AtcEdit)).Succeeded)
            {
                var count = counts.GetValueOrDefault(transfer.EventId);
                counts[transfer.EventId] = (count.NoShows, count.ToMark, count.Transfers + 1, count.Reports);
            }
        }

        foreach (var report in reports)
        {
            if ((await authorization.AuthorizeAsync(request.User, report, EventsPermissions.ReportsEdit)).Succeeded)
            {
                var count = counts.GetValueOrDefault(report.EventId);
                counts[report.EventId] = (count.NoShows, count.ToMark, count.Transfers, count.Reports + 1);
            }
        }

        var ids = counts.Keys.ToList();
        var events = (await CrudSource.BackOffice<Event>(database).AsNoTracking()
                .Where(row => ids.Contains(row.Id))
                .ToListAsync(cancellationToken))
            .OrderBy(row => row.EndsAtUtc)
            .ThenBy(row => row.Id)
            .Take(MaxEvents)
            .Select(row => new StaffQueueEventDto(
                row.Id,
                row.Slug,
                row.Title,
                row.StartsAtUtc,
                row.EndsAtUtc,
                counts[row.Id].NoShows,
                counts[row.Id].ToMark,
                counts[row.Id].Transfers,
                counts[row.Id].Reports))
            .ToList();

        return new JsonObject
        {
            ["signedIn"] = true,
            ["events"] = JsonSerializer.SerializeToNode(events, Json),
        };
    }
}
