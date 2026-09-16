using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>
/// The tours in the back office (design M2 §8.3): a resource of the CRUD engine, read with <c>Tours.View</c>, written
/// with <c>Tours.Edit</c>, deleted with <c>Tours.Delete</c> too, and a template written with <c>Tours.ManageTemplates</c>.
/// <para>⚠️ Four hand written verbs hang off the group, counted as the content's are: the state of a tour — ready, draft,
/// hidden, shown — is one small state machine and not a field of the form (§1.2.1, §1.2.2); what stands in the way of
/// "ready", asked before pressing it rather than after being refused; and the two copies of a template (§1.10), which
/// create a row from another row. Each runs the same rules as the engine's save, <see cref="TourSaving"/>.</para>
/// </summary>
public static class TourEndpoints
{
    public const string Pattern = "/api/flightops/tours";

    /// <summary><c>filter[needsOwnDailyLimit]=true</c>: the tours that forbid switching the division's limit off (§3.7).</summary>
    public const string NeedsOwnDailyLimitFilter = "needsOwnDailyLimit";

    public static IEndpointRouteBuilder MapTourEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new TourMapper();
        var clock = app.ServiceProvider.GetRequiredService<IClock>();

        var group = app.MapCrud<Tour, TourListDto, TourDetailDto, TourWriteDto>(Pattern, options =>
        {
            options.PermissionArea = TourPermissions.Area;
            options.Name = "FlightOpsTours";
            options.ReadPolicy = TourPermissions.View;
            options.WritePolicy = TourPermissions.Edit;
            options.DeletePolicy = TourPermissions.Delete;
            options.ContextType = typeof(FlightOpsDbContext);

            // A template is a tool of the department, not a tour: its own permission to change (§1.10).
            options.ExtraWritePolicy = tour => tour.IsTemplate ? TourPermissions.ManageTemplates : null;

            options.DefaultOrder = tour => tour.ReleaseAt;
            options.Sortable.Add(nameof(Tour.ReleaseAt));
            options.Sortable.Add(nameof(Tour.CloseAt));
            options.Sortable.Add(nameof(Tour.Kind));
            options.Sortable.Add(nameof(Tour.UpdatedAt));
            options.Filterable.Add(nameof(Tour.IsTemplate));
            options.Filterable.Add(nameof(Tour.Kind));
            options.Filterable.Add(nameof(Tour.IsHidden));
            options.DefaultFilters[nameof(Tour.IsTemplate)] = "false";
            options.SearchFields.Add(tour => tour.Title);
            options.SearchFields.Add(tour => tour.Slug);

            options.CustomFilters[NeedsOwnDailyLimitFilter] = (query, raw) =>
                bool.TryParse(raw, out var needs)
                    ? needs ? query.Where(TourState.NeedsOwnDailyLimit(clock.UtcNow)) : query
                    : null;

            options.ToList = tour => mapper.ToList(tour, clock.UtcNow);
            options.ToDetail = tour => mapper.ToDetail(tour, clock.UtcNow);
            options.Apply = mapper.Apply;
            options.BeforeSave = (tour, saving) =>
                saving.Services.GetRequiredService<TourSaving>().PrepareAsync(tour, saving.IsNew, saving.CancellationToken);

            // A tour somebody has reported on is hidden, never deleted (§1.2.2).
            options.Delete = async (tour, services, cancellationToken) =>
            {
                if (await services.GetRequiredService<ITourReports>().AnyAsync(tour.Id, cancellationToken))
                {
                    throw new DomainRefusalException("id", "flightops:errors.tourHasReports");
                }

                services.GetRequiredService<FlightOpsDbContext>().Tours.Remove(tour);
            };
        });

        group.MapPost("/{id:long}/status", ChangeStatusAsync)
            .WithName("FlightOpsTourStatus")
            .Produces<TourDetailDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.Edit);

        group.MapGet("/{id:long}/ready-problems", ReadyProblemsAsync)
            .WithName("FlightOpsTourReadyProblems")
            .Produces<TourReadyProblemsDto>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.View);

        group.MapPost("/from-template/{templateId:long}", CreateFromTemplateAsync)
            .WithName("FlightOpsTourFromTemplate")
            .Produces<TourDetailDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.Edit);

        group.MapPost("/{id:long}/save-as-template", SaveAsTemplateAsync)
            .WithName("FlightOpsTourSaveAsTemplate")
            .Produces<TourDetailDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.ManageTemplates);

        return app;
    }

    private static async Task<IResult> ChangeStatusAsync(
        long id,
        TourStatusRequest request,
        FlightOpsDbContext database,
        TourReadiness readiness,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        IClock clock,
        HttpContext http)
    {
        var tour = await CrudSource.BackOffice<Tour>(database).FirstOrDefaultAsync(row => row.Id == id, http.RequestAborted);
        if (tour is null)
        {
            return NotFound(catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, tour, TourPermissions.Edit)).Succeeded)
        {
            return Forbidden(catalog, currentUser);
        }

        // A template is never ready, hidden or shown: it is a tool, and its only states are those of its rows' copies.
        if (tour.IsTemplate)
        {
            return Refused("status", "flightops:errors.templateNeverReady", catalog, currentUser);
        }

        var now = clock.UtcNow;

        switch (request.Action)
        {
            case TourStatusAction.Ready:
                var problems = await readiness.ProblemsAsync(tour, http.RequestAborted);
                if (!problems.IsEmpty)
                {
                    return CrudProblems.Validation(problems.Errors, problems.Localized, catalog, currentUser.Locale);
                }

                tour.Status = PublishStatus.Published;
                tour.PublishedAt = now;
                break;

            // Back to draft is how the kind is changed before the release; after it, the tour is hidden instead.
            case TourStatusAction.Draft when tour.ReleaseAt <= now && tour.Status == PublishStatus.Published:
                return Refused("status", "flightops:errors.releasedStaysReady", catalog, currentUser);

            case TourStatusAction.Draft:
                tour.Status = PublishStatus.Draft;
                break;

            case TourStatusAction.Hide:
                tour.IsHidden = true;
                break;

            case TourStatusAction.Show:
                tour.IsHidden = false;
                break;

            default:
                return Refused("action", "errors.required", catalog, currentUser);
        }

        await database.SaveChangesAsync(http.RequestAborted);

        return Results.Ok(new TourMapper().ToDetail(tour, now));
    }

    /// <summary>The answer "ready" would give, without marking anything: nothing written.</summary>
    private static async Task<IResult> ReadyProblemsAsync(
        long id,
        FlightOpsDbContext database,
        TourReadiness readiness,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var tour = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == id, http.RequestAborted);
        if (tour is null)
        {
            return NotFound(catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, tour, TourPermissions.View)).Succeeded)
        {
            return Forbidden(catalog, currentUser);
        }

        var problems = await readiness.ProblemsAsync(tour, http.RequestAborted);

        return Results.Ok(new TourReadyProblemsDto(problems.Errors, problems.Localized));
    }

    private static async Task<IResult> CreateFromTemplateAsync(
        long templateId,
        TourFromTemplateRequest request,
        FlightOpsDbContext database,
        IValidator<TourFromTemplateRequest> validator,
        BlockDocumentWalker walker,
        TourSaving saving,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        IClock clock,
        HttpContext http)
    {
        var template = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == templateId && row.IsTemplate, http.RequestAborted);
        if (template is null)
        {
            return NotFound(catalog, currentUser);
        }

        // Using a template needs reading it and writing the tour; changing templates is not asked.
        if (!(await authorization.AuthorizeAsync(http.User, template, TourPermissions.View)).Succeeded)
        {
            return Forbidden(catalog, currentUser);
        }

        var validation = await validator.ValidateAsync(request, http.RequestAborted);
        if (!validation.IsValid)
        {
            return CrudProblems.Validation(validation, catalog, currentUser.Locale);
        }

        var tour = TourCopy.Settings(template, walker);
        tour.Title = request.Title;
        tour.Slug = request.Slug.Trim().ToLowerInvariant();

        return await CreateAsync(tour, TourPermissions.Edit, database, saving, authorization, currentUser, catalog, clock, http);
    }

    private static async Task<IResult> SaveAsTemplateAsync(
        long id,
        TourSaveAsTemplateRequest request,
        FlightOpsDbContext database,
        IValidator<TourSaveAsTemplateRequest> validator,
        BlockDocumentWalker walker,
        TourSaving saving,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        IClock clock,
        HttpContext http)
    {
        var source = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == id, http.RequestAborted);
        if (source is null)
        {
            return NotFound(catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, source, TourPermissions.View)).Succeeded)
        {
            return Forbidden(catalog, currentUser);
        }

        var validation = await validator.ValidateAsync(request, http.RequestAborted);
        if (!validation.IsValid)
        {
            return CrudProblems.Validation(validation, catalog, currentUser.Locale);
        }

        var template = TourCopy.Settings(source, walker);
        template.IsTemplate = true;
        template.Title = request.Title;

        return await CreateAsync(template, TourPermissions.ManageTemplates, database, saving, authorization, currentUser, catalog, clock, http);
    }

    /// <summary>The end both copies share: permission on the row as it will be, the rules of every save, the save.</summary>
    private static async Task<IResult> CreateAsync(
        Tour tour,
        string permission,
        FlightOpsDbContext database,
        TourSaving saving,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        IClock clock,
        HttpContext http)
    {
        if (!(await authorization.AuthorizeAsync(http.User, tour, permission)).Succeeded)
        {
            return Forbidden(catalog, currentUser);
        }

        database.Tours.Add(tour);

        if (await saving.PrepareAsync(tour, isNew: true, http.RequestAborted) is { } refused)
        {
            return CrudProblems.Validation(refused, new Dictionary<string, string[]>(StringComparer.Ordinal), catalog, currentUser.Locale);
        }

        await database.SaveChangesAsync(http.RequestAborted);

        return Results.Created($"{Pattern}/{tour.Id}", new TourMapper().ToDetail(tour, clock.UtcNow));
    }

    private static IResult Refused(string field, string key, LocaleCatalog catalog, ICurrentUser currentUser) =>
        CrudProblems.Validation(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [key] },
            new Dictionary<string, string[]>(StringComparer.Ordinal),
            catalog,
            currentUser.Locale);

    private static IResult NotFound(LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: catalog.Resolve(currentUser.Locale, CrudProblems.NotFoundTitleKey));

    private static IResult Forbidden(LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: catalog.Resolve(currentUser.Locale, CrudProblems.ForbiddenTitleKey));
}
