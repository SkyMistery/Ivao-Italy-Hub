using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>Where the roster of an event stands (§4.3, §4.4): availabilities still open, closed and to be proposed, proposed, published.</summary>
public enum RosterStage
{
    Open,
    ToPropose,
    Proposed,
    Published,
}

/// <summary>
/// One event of the block <c>events.atcCoverage</c> (§7.3, E12): its head and link, where its roster stands with the moments it closes and is
/// published, how many gave their availability, how many pieces of the positions the reader reads nobody holds out of how many, and how many
/// requests to hand a shift over wait for the reader. Numbers and a word of a closed set: the page writes them.
/// </summary>
public sealed record AtcCoverageEventDto(
    long Id,
    string Slug,
    Localized<string> Title,
    DateTime StartsAtUtc,
    DateTime ClosesAtUtc,
    DateTime PublishesAtUtc,
    RosterStage Stage,
    int Availabilities,
    int Pieces,
    int Uncovered,
    int Transfers);

/// <summary>
/// The block <c>events.atcCoverage</c> (design M4 §7.3, E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, reading 8): on
/// <c>/staff</c> and the dashboards of the events' and the ATC's staff, the events with a roster that start within <c>days</c> — published,
/// not cancelled, not over — and need somebody: pieces nobody holds, availabilities still open, requests to hand a shift over to decide.
/// <para>Always live and the reader's: the uncovered pieces are counted on the positions the one handler lets them read — the staff of a FIR
/// on theirs —, with the roster's own function (<see cref="StaffRoster.Gaps"/> on <see cref="RosterProposer.Pieces(DateTime, DateTime, int)"/>),
/// and the requests on the ones it lets them decide. An event of which they read no position is not theirs. A visitor gets
/// <c>signedIn: false</c>.</para>
/// </summary>
public sealed class AtcCoverageProvider(
    EventsDbContext database,
    ModuleSettingsStore settingsStore,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    ICurrentUser currentUser,
    IClock clock) : IDataBlockProvider
{
    public const string BlockType = "events.atcCoverage";

    /// <summary>How many days ahead the block looks: nothing written, <see cref="DefaultDays"/>, as the block's schema.</summary>
    public const string DaysProperty = "days";

    public const int DefaultDays = 14;

    public const int MaxDays = 60;

    /// <summary>The events read before the permissions narrow them: a dashboard is not the list.</summary>
    private const int Window = 500;

    /// <summary>The most events the block shows, once the permissions narrowed them (the review of #268, point 6).</summary>
    public const int MaxEvents = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LocalizedJsonConverterFactory(), new JsonStringEnumConverter() },
    };

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || http.HttpContext is not { } request)
        {
            return new JsonObject { ["signedIn"] = false };
        }

        var days = Math.Clamp(BlockProps.Number(props, DaysProperty) ?? DefaultDays, 1, MaxDays);
        var now = clock.UtcNow;
        var until = now.AddDays(days);
        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);

        var rows = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => row.HasRoster
                && row.Status == PublishStatus.Published
                && row.CancelledAt == null
                && row.EndsAtUtc > now
                && row.StartsAtUtc <= until)
            .OrderBy(row => row.StartsAtUtc)
            .ThenBy(row => row.Id)
            .Take(Window)
            .ToListAsync(cancellationToken);

        var events = new List<AtcCoverageEventDto>();
        foreach (var row in rows)
        {
            if (events.Count == MaxEvents)
            {
                break;
            }

            if (await CoverageAsync(row, settings, request.User, now, cancellationToken) is { } coverage
                && (coverage.Uncovered > 0 || coverage.Stage == RosterStage.Open || coverage.Transfers > 0))
            {
                events.Add(coverage);
            }
        }

        return new JsonObject
        {
            ["signedIn"] = true,
            ["days"] = days,
            ["events"] = JsonSerializer.SerializeToNode(events, Json),
        };
    }

    /// <summary>One event as the reader may see it; null when they read none of its positions.</summary>
    private async Task<AtcCoverageEventDto?> CoverageAsync(
        Event row,
        EventsSettings settings,
        System.Security.Claims.ClaimsPrincipal user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var readable = new List<EventAtcPosition>();
        foreach (var position in await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking()
                     .Where(position => position.EventId == row.Id)
                     .ToListAsync(cancellationToken))
        {
            if ((await authorization.AuthorizeAsync(user, position, EventsPermissions.AtcView)).Succeeded)
            {
                readable.Add(position);
            }
        }

        if (readable.Count == 0)
        {
            return null;
        }

        var ids = readable.Select(position => position.Id).ToList();
        var shifts = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
            .Where(shift => ids.Contains(shift.PositionId))
            .ToListAsync(cancellationToken);
        var minutes = RosterData.Rules(row, settings).ShiftMinutes;

        var pieces = 0;
        var uncovered = 0;
        foreach (var position in readable)
        {
            var own = shifts.Where(shift => shift.PositionId == position.Id).ToList();
            foreach (var (fromUtc, toUtc) in RosterProposer.Pieces(position.FromUtc, position.ToUtc, minutes))
            {
                pieces++;
                uncovered += StaffRoster.Gaps(fromUtc, toUtc, own).Count > 0 ? 1 : 0;
            }
        }

        var transfers = 0;
        foreach (var transfer in await CrudSource.BackOffice<EventAtcTransfer>(database).AsNoTracking()
                     .Where(transfer => transfer.EventId == row.Id && transfer.Status == TransferStatus.Pending)
                     .ToListAsync(cancellationToken))
        {
            if ((await authorization.AuthorizeAsync(user, transfer, EventsPermissions.AtcEdit)).Succeeded)
            {
                transfers++;
            }
        }

        // The availabilities on the positions the reader reads (reading 8): a window that meets one of them.
        var availabilities = (await CrudSource.BackOffice<EventAtcWindow>(database).AsNoTracking()
                .Where(window => window.EventId == row.Id)
                .ToListAsync(cancellationToken))
            .Where(window => readable.Any(position => AtcRules.Meet(window.FromUtc, window.ToUtc, position.FromUtc, position.ToUtc)))
            .Select(window => window.AvailabilityId)
            .Distinct()
            .Count();
        var closes = AtcRules.ClosesAt(row, settings);

        return new AtcCoverageEventDto(
            row.Id,
            row.Slug,
            row.Title,
            row.StartsAtUtc,
            closes,
            RosterPublication.PublishesAt(row, settings),
            Stage(row, closes, now),
            availabilities,
            pieces,
            uncovered,
            transfers);
    }

    /// <summary>Where a roster stands: published, proposed, closed and to be proposed, or open to the availabilities.</summary>
    public static RosterStage Stage(Event row, DateTime closesAtUtc, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.RosterPublishedAt is not null ? RosterStage.Published
            : row.RosterProposedAt is not null ? RosterStage.Proposed
            : closesAtUtc <= now ? RosterStage.ToPropose
            : RosterStage.Open;
    }
}
