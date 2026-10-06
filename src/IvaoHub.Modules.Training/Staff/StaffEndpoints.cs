using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>
/// The staff's side of the trainings (design M3 §2.3, §2.4, §2.5, §2.6, §2.7, §4.2): the list, generated, with its views — to
/// approve, to assign, in progress, to close, the history — and the page of one training with its verbs: read it, the trainers it
/// may be given, accept, refuse, assign; its dates (A8) — what a date meets, the dates proposed and one taken back, the date set by
/// hand —, and closing it; and what its session came to (A9) — rescheduled, not attended, reported. Whoever holds
/// <c>Training.View</c> reads every training, open and closed (R.1, d2), through the one function that leaves out what is reserved
/// for its trainee; what they may do on one is the handler's answer on the row, and every refusal is a <c>ProblemDetails</c> field by
/// field.
/// <para>A head of a FIR — its chief or assistant chief — holds <c>Training.View</c> and <c>Training.Assign</c> on the trainings of
/// their FIR alone (A11b; design M3 §3.2, §4.2): the list holds those, as the CRUD engine narrows it, and the page, the trainers and
/// the assignment answer them on those, as the one handler does on the row; a pilot's training has no FIR, and is none of theirs.
/// Nothing here names a FIR: the permission carries it.</para>
/// </summary>
public static class StaffEndpoints
{
    /// <summary>
    /// The list: read only, the trainings change through the verbs of the page. It is the only generic resource of the training,
    /// and it maps no write at all — no <c>DELETE</c> whoever asks, the trainer who conducts one included: a training leaves the
    /// register through no verb (§6; A7b, the reviewer's point 2 on #146).
    /// </summary>
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

            options.ToList = training => StaffTrainings.Row(training, noNames, vocabulary, StaffQueue.HeldBefore(clock.UtcNow, zone));
            options.ToListPage = (trainings, services, cancellationToken) =>
                services.GetRequiredService<StaffTrainings>().RowsAsync(trainings, cancellationToken);
            options.ToDetail = training => StaffTrainings.Row(training, noNames, vocabulary, StaffQueue.HeldBefore(clock.UtcNow, zone));
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

        // The dates (A8): what a date meets, the dates proposed and one taken back, the date set by hand; and the closing.
        trainings.MapGet("/{id:long}/conflicts", ConflictsAsync)
            .WithName("TrainingDateConflicts")
            .Produces<DateConflictsDto>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        trainings.MapPost("/{id:long}/slots", (long id, TrainingSlotsWriteDto body, StaffTrainings staff, TrainingDates dates, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => dates.ProposeAsync(training, body, http.RequestAborted)))
            .Step("TrainingSlotsPropose");

        trainings.MapPost("/{id:long}/slots/{slotId:long}/withdraw", (long id, long slotId, TrainingSlotWithdrawalDto body, StaffTrainings staff, TrainingDates dates, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => dates.WithdrawAsync(training, slotId, body.RowVersion, http.RequestAborted)))
            .Step("TrainingSlotWithdraw");

        trainings.MapPost("/{id:long}/date", (long id, TrainingDateWriteDto body, StaffTrainings staff, TrainingDates dates, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => dates.SetAsync(training, body, http.RequestAborted)))
            .Step("TrainingDateSet");

        trainings.MapPost("/{id:long}/close", (long id, TrainingClosureDto body, StaffTrainings staff, TrainingDates dates, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => dates.CloseAsync(training, body, http.RequestAborted)))
            .Step("TrainingClose");

        // After the session (A9): rescheduled, not attended, or reported.
        trainings.MapPost("/{id:long}/reschedule", (long id, TrainingRescheduleDto body, StaffTrainings staff, TrainingSessions sessions, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => sessions.RescheduleAsync(training, body, http.RequestAborted)))
            .Step("TrainingReschedule");

        trainings.MapPost("/{id:long}/no-show", (long id, TrainingNoShowDto body, StaffTrainings staff, TrainingSessions sessions, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => sessions.NoShowAsync(training, body, http.RequestAborted)))
            .Step("TrainingNoShow");

        trainings.MapPost("/{id:long}/report", (long id, TrainingReportDto body, StaffTrainings staff, TrainingSessions sessions, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                StepAsync(id, staff, catalog, user, http, training => sessions.ReportAsync(training, body, http.RequestAborted)))
            .Step("TrainingReport");

        return app;
    }

    /// <summary>
    /// What a date meets (§2.5), to whoever may conduct the training — the dates are theirs to propose and to set —: the policy and
    /// the warnings of the days from <paramref name="startsAtUtc"/> to <paramref name="endsAtUtc"/>, or of the day it starts on.
    /// </summary>
    private static async Task<IResult> ConflictsAsync(
        long id,
        DateTime startsAtUtc,
        DateTime? endsAtUtc,
        StaffTrainings staff,
        TrainingDates dates,
        HttpContext http)
    {
        var training = await staff.FindAsync(id, tracked: false, http.RequestAborted);
        if (training is null)
        {
            return Results.NotFound();
        }

        return await staff.MayAsync(training, TrainingPermissions.Conduct)
            ? Results.Ok(await dates.ConflictsAsync(training, startsAtUtc, endsAtUtc, http.RequestAborted))
            : Results.StatusCode(StatusCodes.Status403Forbidden);
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
            ? Results.Ok(await staff.PageAsync(training, withHistory: true, http.RequestAborted))
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
                _ => Results.Ok(await staff.PageAsync(training, withHistory: true, http.RequestAborted)),
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
