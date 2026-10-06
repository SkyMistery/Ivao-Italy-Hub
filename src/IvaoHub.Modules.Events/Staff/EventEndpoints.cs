using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// The events in the back office (design M4 §7.2, E3a): a resource of the CRUD engine, read with <c>Events.View</c>, written
/// with <c>Events.Edit</c>, deleted with <c>Events.Delete</c> too — which only the coordinator and the assistant of the base
/// department hold, so whoever collaborates never deletes (§6.3). Its list has the views of <see cref="EventViews"/>.
/// <para>⚠️ Three hand written endpoints hang off it: <b>publish</b> (§2.2, E3b) and <b>cancel</b> (§2.3), two verbs that are not
/// fields of the form; and the <b>presets of the kinds</b>, a read: the form presets the switches from them when the kind changes
/// (§1.12), and whoever writes events reads them there — the settings themselves are read only by whoever manages them, which an
/// advisor of the department who creates events does not.</para>
/// </summary>
public static class EventEndpoints
{
    public const string Pattern = "/api/events/events";

    /// <summary>What each kind presets, to whoever writes events.</summary>
    public const string KindPresetsPattern = "/api/events/kind-presets";

    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var clock = app.ServiceProvider.GetRequiredService<IClock>();

        var group = app.MapCrud<Event, EventListDto, EventDetailDto, EventWriteDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.Area;
            options.Name = "Events";
            options.ReadPolicy = EventsPermissions.View;
            options.WritePolicy = EventsPermissions.Edit;
            options.DeletePolicy = EventsPermissions.Delete;
            options.ContextType = typeof(EventsDbContext);

            options.DefaultOrder = row => row.StartsAtUtc;
            options.Sortable.Add(nameof(Event.StartsAtUtc));
            options.Sortable.Add(nameof(Event.EndsAtUtc));
            options.Sortable.Add(nameof(Event.UpdatedAt));
            options.Filterable.Add(nameof(Event.Kind));
            options.SearchFields.Add(row => row.Title);
            options.SearchFields.Add(row => row.Slug);
            options.CustomFilters[EventViews.Filter] = (query, view) =>
                EventViews.Where(view, clock.UtcNow) is { } where ? query.Where(where) : null;

            // The page adds the division's word for each kind; one row alone is what the engine asks for when it has no page.
            options.ToList = row => EventMapper.ToList(row, clock.UtcNow, kindLabel: null);
            options.ToListPage = PageAsync;
            options.ToDetail = row => EventMapper.ToDetail(row, clock.UtcNow);
            options.Apply = EventMapper.Apply;
            options.BeforeSave = (row, saving) =>
                saving.Services.GetRequiredService<EventSaving>().PrepareAsync(row, saving.IsNew, saving.CancellationToken);
            options.Delete = (row, services, cancellationToken) =>
                services.GetRequiredService<EventSaving>().DeleteAsync(row, cancellationToken);
        });

        group.MapPost("/{id:long}/publish", PublishAsync)
            .WithName("EventsPublish")
            .Produces<EventDetailDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.Edit);

        group.MapPost("/{id:long}/cancel", CancelAsync)
            .WithName("EventsCancel")
            .Produces<EventDetailDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.Edit);

        app.MapGet(KindPresetsPattern, KindPresetsAsync)
            .WithName("EventsKindPresets")
            .WithTags("Events")
            .Produces<IReadOnlyList<KindPreset>>()
            .RequireAuthorization(EventsPermissions.Edit);

        return app;
    }

    /// <summary>
    /// Publishes an event (§2.2): what <see cref="EventPublishing"/> asks, field by field. Published, the event is seen from its
    /// «seen from», or at once when that is empty, and its calendar entry and its line in the search follow — at once, or with
    /// <see cref="EventReleaseJob"/> at the release (E3b). Once: a published event stays published, and a cancelled one is not
    /// published.
    /// </summary>
    private static async Task<IResult> PublishAsync(
        long id,
        EventPublishRequest request,
        EventsDbContext database,
        EventPublishing publishing,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        IClock clock,
        HttpContext http)
    {
        var row = await CrudSource.BackOffice<Event>(database).FirstOrDefaultAsync(candidate => candidate.Id == id, http.RequestAborted);
        if (row is null)
        {
            return Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, row, EventsPermissions.Edit)).Succeeded)
        {
            return Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser);
        }

        var refusals = row.CancelledAt is not null
            ? new Refusals().Add("id", "events:errors.alreadyCancelled")
            : row.Status == PublishStatus.Published
                ? new Refusals().Add("id", "events:errors.alreadyPublished")
                : await publishing.ProblemsAsync(row, http.RequestAborted);

        if (!refusals.IsEmpty)
        {
            return CrudProblems.Validation(refusals, catalog, currentUser.Locale);
        }

        // The version the reader read, as for a cancellation: an event changed since is not published as it no longer is.
        if (request.RowVersion != default)
        {
            database.Entry(row).Property(candidate => candidate.RowVersion).OriginalValue = request.RowVersion;
        }

        var now = clock.UtcNow;
        row.Status = PublishStatus.Published;
        row.PublishedAt = now;
        await database.SaveChangesAsync(http.RequestAborted);

        return Results.Ok(EventMapper.ToDetail(row, now));
    }

    /// <summary>
    /// Cancels an event (§2.3): when, who, and why in every language of the division — the page says it until the end. An event
    /// cancelled stays cancelled, and one that is over is not cancelled any more: it happened. Whoever booked, has a shift or
    /// registered is told (<see cref="EventsNotifications.EventCancelled"/>) once their rows exist (E6a, E12, E16).
    /// </summary>
    private static async Task<IResult> CancelAsync(
        long id,
        EventCancelRequest request,
        EventsDbContext database,
        IValidator<EventCancelRequest> validator,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        IClock clock,
        HttpContext http)
    {
        var row = await CrudSource.BackOffice<Event>(database).FirstOrDefaultAsync(candidate => candidate.Id == id, http.RequestAborted);
        if (row is null)
        {
            return Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, row, EventsPermissions.Edit)).Succeeded)
        {
            return Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser);
        }

        var validation = await validator.ValidateAsync(request, http.RequestAborted);
        if (!validation.IsValid)
        {
            return CrudProblems.Validation(validation, catalog, currentUser.Locale);
        }

        var now = clock.UtcNow;
        var refusals = new Refusals();

        if (row.CancelledAt is not null)
        {
            refusals.Add("id", "events:errors.alreadyCancelled");
        }
        else if (now >= row.EndsAtUtc)
        {
            refusals.Add("id", "events:errors.eventOver");
        }

        if (!refusals.IsEmpty)
        {
            return CrudProblems.Validation(refusals, catalog, currentUser.Locale);
        }

        // The version the reader read: somebody who changed the event since is not overwritten by a cancellation made on what
        // they no longer see.
        if (request.RowVersion != default)
        {
            database.Entry(row).Property(candidate => candidate.RowVersion).OriginalValue = request.RowVersion;
        }

        row.CancelledAt = now;
        row.CancelledBy = currentUser.Vid;
        row.CancellationNote = request.Note;
        await database.SaveChangesAsync(http.RequestAborted);

        return Results.Ok(EventMapper.ToDetail(row, now));
    }

    /// <summary>The presets of the kinds, as the settings hold them: the form of an event reads them, and changes nothing.</summary>
    private static async Task<IResult> KindPresetsAsync(ModuleSettingsStore settings, HttpContext http) =>
        Results.Ok((await settings.GetAsync<EventsSettings>(EventsModule.ModuleKey, http.RequestAborted)).KindPresets);

    /// <summary>Each event with the division's word for its kind: one query per page, not per row.</summary>
    private static async Task<IReadOnlyList<EventListDto>> PageAsync(
        IReadOnlyList<Event> rows,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var keys = rows.Select(row => row.Kind).Distinct(StringComparer.Ordinal).ToArray();
        var kinds = await services.GetRequiredService<HubDbContext>().CalendarKinds.AsNoTracking()
            .Where(kind => keys.Contains(kind.Key))
            .ToListAsync(cancellationToken);

        // The database does not mind the case; the kind of an event is spelled as the calendar spells it.
        var labels = kinds
            .Where(kind => keys.Contains(kind.Key, StringComparer.Ordinal))
            .ToDictionary(kind => kind.Key, kind => kind.Label, StringComparer.Ordinal);
        var now = services.GetRequiredService<IClock>().UtcNow;

        return [.. rows.Select(row => EventMapper.ToList(row, now, labels.GetValueOrDefault(row.Kind)))];
    }

    private static IResult Problem(int status, string titleKey, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: status, title: catalog.Resolve(currentUser.Locale, titleKey));
}
