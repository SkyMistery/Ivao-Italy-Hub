using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Checks;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Threads;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.FlightOps.Review;

/// <summary>
/// The validation (design M2 §4, §8.5): the queue as a generated list, and the page with its verbs — read, tracks, take,
/// let go, decide, reopen, and decide a dispute (T14b). Anybody who may validate one tour reads every report (§4.1); what they may do on one is the
/// handler's answer on the row, and every refusal is a <c>ProblemDetails</c> field by field.
/// </summary>
public static class ReviewEndpoints
{
    public const string QueuePattern = "/api/flightops/review/queue";

    public const string Pattern = "/api/flightops/review";

    /// <summary><c>filter[open]=true</c>, the default: waiting or in review. <c>false</c>: decided.</summary>
    public const string OpenFilter = "open";

    /// <summary><c>filter[disputed]=true</c>: the rejections whose dispute is open (§3.8) — decided, so off the default view.</summary>
    public const string DisputedFilter = "disputed";

    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The queue (§4.1): one list, by tour with filter[tourId]. Ordered by date — the report that has waited longest
        // first — or with sort=tourId grouped by tour and by date within it; the page keeps the choice as the reader's
        // preference (flightops.reviewQueueOrder). Read only: a report is taken and decided through the verbs below.
        app.MapCrud<Pirep, ReviewQueueRowDto, ReviewQueueRowDto, ReviewStepDto>(QueuePattern, options =>
        {
            options.PermissionArea = TourPermissions.Area;
            options.Name = "FlightOpsReviewQueue";
            options.ReadPolicy = TourPermissions.Validate;
            options.WritePolicy = TourPermissions.Validate;
            options.ReadOnly = true;
            options.ContextType = typeof(FlightOpsDbContext);
            options.Source = database => CrudSource.BackOffice<Pirep>(database).Where(report => report.Status != PirepStatus.Withdrawn);
            options.DefaultOrder = report => report.QueuedAt;
            options.Sortable.Add(nameof(Pirep.QueuedAt));
            options.Sortable.Add(nameof(Pirep.TourId));
            options.Sortable.Add(nameof(Pirep.TakeoffAt));
            options.Filterable.Add(nameof(Pirep.TourId));
            options.Filterable.Add(nameof(Pirep.Status));
            options.Filterable.Add(nameof(Pirep.Vid));
            options.CustomFilters[OpenFilter] = (query, raw) => raw switch
            {
                "true" => query.Where(report => report.Status == PirepStatus.Queued || report.Status == PirepStatus.InReview),
                "false" => query.Where(report => report.Status == PirepStatus.Accepted
                    || report.Status == PirepStatus.ToModify
                    || report.Status == PirepStatus.Rejected),
                _ => null,
            };
            options.CustomFilters[DisputedFilter] = (query, raw) => raw == "true" ? query.Where(report => report.IsDisputed) : null;
            options.DefaultFilters[OpenFilter] = "true";

            options.ToList = report => Row(report, null, null, new Dictionary<int, string>(), canTake: false, isOwn: false);
            options.ToListPage = QueuePageAsync;
            options.ToDetail = report => Row(report, null, null, new Dictionary<int, string>(), canTake: false, isOwn: false);
        });

        var review = app.MapGroup(Pattern).WithTags("FlightOpsReview").RequireAuthorization(TourPermissions.Validate);

        review.MapGet("/{id:long}", ReadAsync)
            .WithName("FlightOpsReview")
            .Produces<ReviewDto>()
            .Produces(StatusCodes.Status404NotFound);

        review.MapGet("/{id:long}/tracks", TracksAsync)
            .WithName("FlightOpsReviewTracks")
            .Produces<IReadOnlyList<ReviewTrackDto>>()
            .Produces(StatusCodes.Status404NotFound);

        // The suggestion for the errors being ticked (§4.3), asked while the validator decides: the rule stays on the server.
        review.MapGet("/{id:long}/suggestion", SuggestionAsync)
            .WithName("FlightOpsReviewSuggestion")
            .Produces<SuggestionDto>()
            .Produces(StatusCodes.Status404NotFound);

        review.MapPost("/{id:long}/take", (long id, ReviewStepDto body, PirepReview reviews, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, reviews, catalog, user, http, pirep => reviews.TakeAsync(pirep, body.RowVersion, http.RequestAborted)))
            .Step("FlightOpsReviewTake");

        review.MapPost("/{id:long}/release", (long id, ReviewStepDto body, PirepReview reviews, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, reviews, catalog, user, http, pirep => reviews.ReleaseAsync(pirep, body.RowVersion, http.RequestAborted)))
            .Step("FlightOpsReviewRelease");

        review.MapPost("/{id:long}/decide", (long id, ReviewDecisionDto body, PirepReview reviews, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, reviews, catalog, user, http, pirep => reviews.DecideAsync(pirep, body, http.RequestAborted)))
            .Step("FlightOpsReviewDecide");

        review.MapPost("/{id:long}/reopen", (long id, ReviewReopenDto body, PirepReview reviews, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, reviews, catalog, user, http, pirep => reviews.ReopenAsync(pirep, body, http.RequestAborted)))
            .Step("FlightOpsReviewReopen");

        // A dispute upheld or turned down (§3.8, T14b), by who holds Tours.ReopenDecisions and did not decide the report.
        review.MapPost("/{id:long}/dispute", (long id, DisputeDecisionDto body, PirepReview reviews, PirepDisputes disputes, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, reviews, catalog, user, http, pirep => disputes.DecideAsync(pirep, body, http.RequestAborted)))
            .Step("FlightOpsReviewDispute");

        return app;
    }

    /// <summary>What every step answers: the page as it is afterwards, or why not.</summary>
    private static RouteHandlerBuilder Step(this RouteHandlerBuilder builder, string name) =>
        builder.WithName(name)
            .Produces<ReviewDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

    private static async Task<IResult> ReadAsync(long id, PirepReview reviews, HttpContext http)
    {
        var pirep = await reviews.FindAsync(id, tracked: false, http.RequestAborted);
        return pirep is null ? Results.NotFound() : Results.Ok(await reviews.PageAsync(pirep, http.RequestAborted));
    }

    private static async Task<IResult> SuggestionAsync(long id, [FromQuery] long[]? errorIds, PirepReview reviews, HttpContext http)
    {
        var pirep = await reviews.FindAsync(id, tracked: false, http.RequestAborted);
        return pirep is null ? Results.NotFound() : Results.Ok(await reviews.SuggestAsync(pirep, errorIds ?? [], http.RequestAborted));
    }

    private static async Task<IResult> TracksAsync(long id, PirepReview reviews, HttpContext http)
    {
        var pirep = await reviews.FindAsync(id, tracked: false, http.RequestAborted);
        return pirep is null ? Results.NotFound() : Results.Ok(await reviews.TracksAsync(pirep, http.RequestAborted));
    }

    /// <summary>One step of the review on a tracked report: the page as it is afterwards, or why not.</summary>
    private static async Task<IResult> StepAsync(
        long id,
        PirepReview reviews,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http,
        Func<Pirep, Task<(ReviewResult Result, IReadOnlyDictionary<string, string[]>? Problems)>> step)
    {
        var pirep = await reviews.FindAsync(id, tracked: true, http.RequestAborted);
        if (pirep is null)
        {
            return Results.NotFound();
        }

        try
        {
            var (result, problems) = await step(pirep);
            return result switch
            {
                ReviewResult.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                ReviewResult.Refused => CrudProblems.Validation(problems!, new Dictionary<string, string[]>(), catalog, currentUser.Locale),
                _ => Results.Ok(await reviews.PageAsync(pirep, http.RequestAborted)),
            };
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException || IsDeadlock(exception))
        {
            // Two takes at once: the first wins (§4.2), and the second reads the report again. On MariaDB the second can also
            // lose as a deadlock — both writes lock the report through the key of the history row they add, then both want
            // to change it — and the database has already rolled it back: it is the same answer. EF reports that one wrapped,
            // as a transient failure, so the whole chain is looked at.
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ConflictTitleKey));
        }
    }

    private static bool IsDeadlock(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is MySqlConnector.MySqlException { ErrorCode: MySqlConnector.MySqlErrorCode.LockDeadlock })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The rows of one page: the tours' titles, the legs' numbers, the names, and whether the reader may take each.</summary>
    private static async Task<IReadOnlyList<ReviewQueueRowDto>> QueuePageAsync(
        IReadOnlyList<Pirep> reports,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FlightOpsDbContext>();
        var reviews = services.GetRequiredService<PirepReview>();
        var currentUser = services.GetRequiredService<ICurrentUser>();
        var now = services.GetRequiredService<IClock>().UtcNow;

        var tourIds = reports.Select(report => report.TourId).Distinct().ToList();
        var titles = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .Where(tour => tourIds.Contains(tour.Id))
            .ToDictionaryAsync(tour => tour.Id, tour => tour.Title, cancellationToken);
        var legIds = reports.Select(report => report.LegId).OfType<long>().Distinct().ToList();
        var numbers = await database.Legs.AsNoTracking()
            .Where(leg => legIds.Contains(leg.Id))
            .ToDictionaryAsync(leg => leg.Id, leg => leg.Number, cancellationToken);
        var names = await reviews.NamesAsync([.. reports.Select(report => (int?)report.Vid), .. reports.Select(report => report.AssignedToVid)], cancellationToken);

        // What the checks propose (T17): how many failed, and where their suggested errors lead — as the page words it.
        var ids = reports.Select(report => report.Id).ToList();
        var failed = await database.CheckResults.AsNoTracking()
            .Where(result => ids.Contains(result.PirepId) && result.Outcome == CheckOutcome.Failed)
            .GroupBy(result => result.PirepId)
            .Select(group => new { PirepId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.PirepId, row => row.Count, cancellationToken);
        var suggested = (await database.PirepErrors.AsNoTracking()
                .Where(error => ids.Contains(error.PirepId) && error.SuggestedByCheck)
                .Select(error => new { error.PirepId, error.ErrorId })
                .ToListAsync(cancellationToken))
            .ToLookup(error => error.PirepId, error => error.ErrorId);

        var rows = new List<ReviewQueueRowDto>(reports.Count);
        foreach (var report in reports)
        {
            var canTake = reviews.IsTakable(report, now) && await reviews.MayValidateAsync(report);
            var suggestion = report.ChecksRanAt is null
                ? null
                : (PirepStatus?)(await reviews.SuggestAsync(report, [.. suggested[report.Id]], cancellationToken)).Outcome;
            rows.Add(Row(
                report,
                titles.GetValueOrDefault(report.TourId),
                report.LegId is { } legId && numbers.TryGetValue(legId, out var number) ? number : null,
                names,
                canTake,
                report.Vid == currentUser.Vid) with
            {
                FailedChecks = failed.GetValueOrDefault(report.Id),
                CheckSuggestion = suggestion,
            });
        }

        return rows;
    }

    private static ReviewQueueRowDto Row(
        Pirep report,
        Localized<string>? title,
        int? legNumber,
        IReadOnlyDictionary<int, string> names,
        bool canTake,
        bool isOwn) =>
        new(
            report.Id,
            report.TourId,
            title ?? Localized<string>.Empty,
            report.LegId,
            legNumber,
            report.DepartureIcao,
            report.ArrivalIcao,
            PirepReview.Member(report.Vid, names)!,
            report.TakeoffAt,
            report.QueuedAt,
            report.Status,
            PirepReview.Member(report.AssignedToVid, names),
            report.LeaseUntil,
            report.IsDisputed,
            isOwn,
            canTake,
            FailedChecks: 0,
            CheckSuggestion: null);
}
