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
/// check proposed, shifts it left unknown when it closed the event, requests to hand a shift over. Numbers: the page writes the words.
/// </summary>
public sealed record StaffQueueEventDto(
    long Id,
    string Slug,
    Localized<string> Title,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int NoShows,
    int ToMark,
    int Transfers);

/// <summary>
/// The block <c>events.staffQueue</c> (design M4 §7.3, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, reading 11): on the dashboards of
/// the events' and the ATC's staff, what waits for a decision of theirs — a no-show to confirm or excuse, a shift to mark, a request to hand a
/// shift over —, one row per event, the latest first. The reports of support join with E14a.
/// <para>Always live and the reader's: each row is counted only where the one handler gives them <c>EventAtc.Edit</c> on it — on its event's
/// care and its position's FIR, and never on a row about themselves, the super administrator included. A visitor gets
/// <c>signedIn: false</c>.</para>
/// <para>What it reads is bounded (the review of #283, point 4): nothing at all for a reader who holds <c>EventAtc.Edit</c> nowhere; the
/// reader's own rows are left out by the query, before the handler; and the window is the latest rows, so that a backlog of shifts nobody
/// marked, which grows season after season, never hides the rows of the last events.</para>
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

        // Nothing to decide for whoever decides nothing anywhere: no row is read, no handler asked.
        if (!currentUser.HasAny(EventsPermissions.AtcEdit))
        {
            return new JsonObject { ["signedIn"] = true, ["events"] = new JsonArray() };
        }

        var now = clock.UtcNow;
        var vid = currentUser.Vid;

        // The shifts to decide (reading 11): a no-show proposed, or a shift unknown of an event the check closed — before, it is looking at it.
        var shifts = await (
                from shift in CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
                join row in CrudSource.BackOffice<Event>(database) on shift.EventId equals row.Id
                where row.CancelledAt == null
                    && row.EndsAtUtc <= now
                    && shift.ControllerVid != vid
                    && (shift.Attendance == ShiftAttendance.NoShowProposed
                        || (shift.Attendance == ShiftAttendance.Unknown && row.AfterDoneAt != null))
                orderby row.EndsAtUtc descending, shift.Id descending
                select shift)
            .Take(Window)
            .ToListAsync(cancellationToken);

        // The requests to hand a shift over, as events.atcCoverage counts them (E12).
        var transfers = await CrudSource.BackOffice<EventAtcTransfer>(database).AsNoTracking()
            .Where(transfer => transfer.Status == TransferStatus.Pending && transfer.ControllerVid != vid)
            .OrderByDescending(transfer => transfer.Id)
            .Take(Window)
            .ToListAsync(cancellationToken);

        var counts = new Dictionary<long, (int NoShows, int ToMark, int Transfers)>();
        foreach (var shift in shifts)
        {
            if ((await authorization.AuthorizeAsync(request.User, shift, EventsPermissions.AtcEdit)).Succeeded)
            {
                var count = counts.GetValueOrDefault(shift.EventId);
                counts[shift.EventId] = shift.Attendance == ShiftAttendance.NoShowProposed
                    ? (count.NoShows + 1, count.ToMark, count.Transfers)
                    : (count.NoShows, count.ToMark + 1, count.Transfers);
            }
        }

        foreach (var transfer in transfers)
        {
            if ((await authorization.AuthorizeAsync(request.User, transfer, EventsPermissions.AtcEdit)).Succeeded)
            {
                var count = counts.GetValueOrDefault(transfer.EventId);
                counts[transfer.EventId] = (count.NoShows, count.ToMark, count.Transfers + 1);
            }
        }

        var ids = counts.Keys.ToList();
        var events = (await CrudSource.BackOffice<Event>(database).AsNoTracking()
                .Where(row => ids.Contains(row.Id))
                .ToListAsync(cancellationToken))
            .OrderByDescending(row => row.EndsAtUtc)
            .ThenByDescending(row => row.Id)
            .Take(MaxEvents)
            .Select(row => new StaffQueueEventDto(
                row.Id,
                row.Slug,
                row.Title,
                row.StartsAtUtc,
                row.EndsAtUtc,
                counts[row.Id].NoShows,
                counts[row.Id].ToMark,
                counts[row.Id].Transfers))
            .ToList();

        return new JsonObject
        {
            ["signedIn"] = true,
            ["events"] = JsonSerializer.SerializeToNode(events, Json),
        };
    }
}
