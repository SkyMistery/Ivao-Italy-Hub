using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>
/// The staff's side of the trainings (design M3 §2.3, §2.4, §4.2): the list, generated, with its views — to approve, to assign,
/// in progress, to close, the history — and the page of one training with its verbs: read it, the trainers it may be given,
/// accept, refuse, assign. Whoever holds <c>Training.View</c> reads every training, open and closed (R.1, d2); what they may do on
/// one is the handler's answer on the row, and every refusal is a <c>ProblemDetails</c> field by field.
/// </summary>
public static class StaffEndpoints
{
    /// <summary>The list: read only, the trainings change through the verbs of the page.</summary>
    public const string QueuePattern = "/api/training/queue";

    public const string Pattern = "/api/training/trainings";

    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var vocabulary = app.ServiceProvider.GetRequiredService<RatingVocabulary>();
        var clock = app.ServiceProvider.GetRequiredService<IClock>();
        var zone = app.ServiceProvider.GetRequiredService<IOptions<DivisionOptions>>().Value.ResolveTimeZone();
        var noNames = new Dictionary<int, string>();

        // Newest first unless the reader sorts, the queues oldest first as the page asks them: filter[queue] is a view,
        // filter[kind] a ladder, filter[traineeVid] and filter[trainerVid] a person; ?q= a VID or a position.
        app.MapCrud<Training, StaffTrainingRowDto, StaffTrainingRowDto, TrainingDecisionDto>(QueuePattern, options =>
        {
            options.PermissionArea = TrainingPermissions.Area;
            options.Name = "TrainingQueue";
            options.ReadPolicy = TrainingPermissions.View;
            options.WritePolicy = TrainingPermissions.Edit;
            options.ReadOnly = true;
            options.ContextType = typeof(TrainingDbContext);
            options.DefaultOrder = training => training.CreatedAt;
            options.Sortable.Add(nameof(Training.CreatedAt));
            options.Sortable.Add(nameof(Training.ScheduledStartUtc));
            options.Filterable.Add(nameof(Training.Kind));
            options.Filterable.Add(nameof(Training.TraineeVid));
            options.Filterable.Add(nameof(Training.TrainerVid));
            options.SearchFields.Add(training => training.Position);
            options.SearchFields.Add(training => training.TraineeVid.ToString());
            options.CustomFilters[StaffQueue.Filter] = (query, view) => StaffQueue.Narrow(query, view, StaffQueue.HeldBefore(clock.UtcNow, zone));

            options.ToList = training => StaffTrainings.Row(training, noNames, vocabulary);
            options.ToListPage = (trainings, services, cancellationToken) =>
                services.GetRequiredService<StaffTrainings>().RowsAsync(trainings, cancellationToken);
            options.ToDetail = training => StaffTrainings.Row(training, noNames, vocabulary);
        });

        var trainings = app.MapGroup(Pattern).WithTags("TrainingStaff").RequireAuthorization(TrainingPermissions.View);

        trainings.MapGet("/{id:long}", ReadAsync)
            .WithName("TrainingStaffPage")
            .Produces<StaffTrainingDto>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        trainings.MapGet("/{id:long}/trainers", TrainersAsync)
            .WithName("TrainingTrainers")
            .Produces<IReadOnlyList<TrainerCandidateDto>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        trainings.MapPost("/{id:long}/accept", (long id, TrainingDecisionDto body, StaffTrainings staff, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => staff.AcceptAsync(training, body.RowVersion, http.RequestAborted)))
            .Step("TrainingAccept");

        trainings.MapPost("/{id:long}/reject", (long id, TrainingRejectionDto body, StaffTrainings staff, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => staff.RejectAsync(training, body, http.RequestAborted)))
            .Step("TrainingReject");

        trainings.MapPost("/{id:long}/assign", (long id, TrainingAssignmentDto body, StaffTrainings staff, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => staff.AssignAsync(training, body, http.RequestAborted)))
            .Step("TrainingAssign");

        return app;
    }

    /// <summary>What every step answers: the page as it is afterwards, or why not.</summary>
    private static RouteHandlerBuilder Step(this RouteHandlerBuilder builder, string name) =>
        builder.WithName(name)
            .Produces<StaffTrainingDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

    private static async Task<IResult> ReadAsync(long id, StaffTrainings staff, HttpContext http)
    {
        var training = await staff.FindAsync(id, tracked: false, http.RequestAborted);
        if (training is null)
        {
            return Results.NotFound();
        }

        return await staff.MayAsync(training, TrainingPermissions.View)
            ? Results.Ok(await staff.PageAsync(training, http.RequestAborted))
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    /// <summary>The trainers the training may be given, to whoever may assign it: nobody else needs the list.</summary>
    private static async Task<IResult> TrainersAsync(long id, StaffTrainings staff, HttpContext http)
    {
        var training = await staff.FindAsync(id, tracked: false, http.RequestAborted);
        if (training is null)
        {
            return Results.NotFound();
        }

        return await staff.MayAsync(training, TrainingPermissions.Assign)
            ? Results.Ok(await staff.CandidatesAsync(training, http.RequestAborted))
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    /// <summary>One step of the staff on a tracked training: the page as it is afterwards, or why not.</summary>
    private static async Task<IResult> StepAsync(
        long id,
        StaffTrainings staff,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http,
        Func<Training, Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)>> step)
    {
        var training = await staff.FindAsync(id, tracked: true, http.RequestAborted);
        if (training is null)
        {
            return Results.NotFound();
        }

        try
        {
            var (result, problems) = await step(training);
            return result switch
            {
                StaffResult.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                StaffResult.Refused => CrudProblems.Validation(problems!, new Dictionary<string, string[]>(), catalog, currentUser.Locale),
                _ => Results.Ok(await staff.PageAsync(training, http.RequestAborted)),
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody else moved the training since the page was read: the reader reads it again.
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ConflictTitleKey));
        }
    }
}
