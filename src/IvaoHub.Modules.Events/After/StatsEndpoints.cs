using System.Data;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.After;

/// <summary>
/// The numbers of an event as the tab «Statistics» reads them (design M4 §1.10, §7.2, E13a): numbers only — the page writes their words in the
/// language on screen (Carmine's answer 6 on #255). The sums of the answers and the seats by activity stay in their own tabs, which read them
/// from the statistics once the rows are gone (E17); here, how many registered and how many seats were taken in all. And when the checks after the
/// event were done (<c>after_done_at</c>). <b>Who flew without booking</b> is empty when it was not counted — an event of the whole division, one
/// without airports, one closed after thirty days before any count — and says at how many of the event's airports it was counted: «not
/// counted» is never a 0 (Carmine's condition on reading 3, #272).
/// </summary>
public sealed record EventStatsDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    int PublicSlots,
    int PrivateSlots,
    int Booked,
    int BookedFlown,
    int BookedUnflown,
    int? FlownUnbooked,
    int? FlownUnbookedAirports,
    int Airports,
    int AtcAvailabilities,
    int AtcShifts,
    int AtcAttended,
    int AtcNoShows,
    int AtcExcused,
    int ReportsSubmitted,
    int ReportsAccepted,
    int AwardSignals,
    int Registrations,
    int ActivitySeats,
    DateTime ComputedAt,
    DateTime? ChecksDoneAt)
{
    public static EventStatsDto Of(EventStats stats, DateTime? checksDoneAt, int airports)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return new EventStatsDto(
            stats.Id,
            stats.EventId,
            stats.OwnerDepartment,
            stats.PublicSlots,
            stats.PrivateSlots,
            stats.Booked,
            stats.BookedFlown,
            stats.BookedUnflown,
            stats.FlownUnbookedAirports is null ? null : stats.FlownUnbooked,
            stats.FlownUnbookedAirports,
            airports,
            stats.AtcAvailabilities,
            stats.AtcShifts,
            stats.AtcAttended,
            stats.AtcNoShows,
            stats.AtcExcused,
            stats.ReportsSubmitted,
            stats.ReportsAccepted,
            stats.AwardSignals,
            stats.Registrations,
            InPersonRetention.Seats(stats).Values.Sum(),
            stats.ComputedAt,
            checksDoneAt);
    }
}

/// <summary>
/// The statistics of the events (design M4 §1.10, §5.1, §7.2, E13a; note 2026-10-16-le-verifiche-dopo-l-evento, reading 9).
/// <list type="bullet">
/// <item><b>The read</b>: a resource of the CRUD engine in read only, <c>/api/events/stats</c>, with <c>Events.View</c> on the event's care,
/// narrowed to one event with <c>filter[eventId]</c> — one row, or none before the first count.</item>
/// <item>⚠️ <b>«Recalculate»</b>, a verb the design names (§1.10: «ricalcolabile dallo staff»): <c>POST /api/events/events/{id}/stats</c>, with
/// <c>Events.Edit</c> on the event, for an event published, not cancelled and over. The numbers again from the rows
/// (<see cref="EventStatistics"/>); who flew without booking stays as the check counted it.</item>
/// </list>
/// </summary>
public static class StatsEndpoints
{
    public const string Pattern = "/api/events/stats";

    /// <summary>«Recalculate»: <c>/api/events/events/{id}/stats</c>.</summary>
    public const string RecalculatePattern = "/api/events/events/{id:long}/stats";

    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapCrud<EventStats, EventStatsDto, EventStatsDto, EventStatsDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.Area;
            options.Name = "EventStats";
            options.ReadPolicy = EventsPermissions.View;
            options.WritePolicy = EventsPermissions.Edit;
            options.ReadOnly = true;
            options.ContextType = typeof(EventsDbContext);

            options.DefaultOrder = stats => stats.EventId;
            options.Filterable.Add(nameof(EventStats.EventId));

            // A page says when the checks of its events were done and how many airports they have, in one query each; a row alone does not.
            options.ToList = stats => EventStatsDto.Of(stats, checksDoneAt: null, airports: 0);
            options.ToListPage = (rows, services, cancellationToken) => PageAsync(services.GetRequiredService<EventsDbContext>(), rows, cancellationToken);
            options.ToDetail = stats => EventStatsDto.Of(stats, checksDoneAt: null, airports: 0);
        });

        app.MapPost(RecalculatePattern, RecalculateAsync)
            .WithTags("Events")
            .WithName("EventsRecalculateStats")
            .Produces<EventStatsDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(EventsPermissions.Edit);

        return app;
    }

    /// <summary>The numbers of an event over, worked out again from its rows and saved; who flew without booking kept.</summary>
    private static async Task<IResult> RecalculateAsync(
        long id,
        EventsDbContext database,
        EventStatistics statistics,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        IClock clock,
        HttpContext http)
    {
        var row = await CrudSource.BackOffice<Event>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, http.RequestAborted);
        if (row is null)
        {
            return Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, row, EventsPermissions.Edit)).Succeeded)
        {
            return Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser);
        }

        var now = clock.UtcNow;
        var refusal = row.Status != PublishStatus.Published ? "events:errors.statsOfADraft"
            : row.CancelledAt is not null ? "events:errors.alreadyCancelled"
            : !EventState.IsOver(row, now) ? "events:errors.statsBeforeEnd"
            : null;
        if (refusal is not null)
        {
            return CrudProblems.Validation(new Refusals().Add("id", refusal), catalog, currentUser.Locale);
        }

        // Under the lock of the event's row, as the job's close: the first row of the statistics is inserted once (the review of #278, point 3).
        EventStats stats;
        await using (var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, http.RequestAborted))
        {
            stats = await statistics.WriteAsync(row, now, http.RequestAborted);
            await database.SaveChangesAsync(http.RequestAborted);
            await transaction.CommitAsync(http.RequestAborted);
        }

        var airports = await database.Airports.AsNoTracking().CountAsync(airport => airport.EventId == id, http.RequestAborted);
        return Results.Ok(EventStatsDto.Of(stats, row.AfterDoneAt, airports));
    }

    private static async Task<IReadOnlyList<EventStatsDto>> PageAsync(EventsDbContext database, IReadOnlyList<EventStats> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Select(stats => stats.EventId).Distinct().ToList();
        var done = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.AfterDoneAt, cancellationToken);

        var airports = await database.Airports.AsNoTracking()
            .Where(airport => ids.Contains(airport.EventId))
            .GroupBy(airport => airport.EventId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        return [.. rows.Select(stats => EventStatsDto.Of(stats, done.GetValueOrDefault(stats.EventId), airports.GetValueOrDefault(stats.EventId)))];
    }

    private static IResult Problem(int status, string titleKey, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: status, title: catalog.Resolve(currentUser.Locale, titleKey));
}
