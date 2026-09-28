using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training.Requests;

/// <summary>
/// The trainee's endpoints (design M3 §2.2, §4.1): their page — where they stand on each ladder, the question on the theory,
/// their trainings —, the request, one training of theirs, and its cancellation. Four hand written verbs, because a request is
/// not a form over a row: it is a set of checks across the trainee's history, whose every refusal is the <c>ProblemDetails</c>
/// of the generated forms, field by field.
/// <para>Any signed in member: a trainee is not a role. Every verb reads only the caller's own trainings, through a DTO with no
/// field of the staff's (§1.1); the staff's side is A7's. The pages that read them are A6b's.</para>
/// </summary>
public static class RequestEndpoints
{
    public const string Pattern = "/api/training/mine";

    public static IEndpointRouteBuilder MapRequestEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mine = app.MapGroup(Pattern).WithTags("TrainingMine").RequireAuthorization(HubPolicies.SignedIn);

        mine.MapGet("/", MineAsync)
            .WithName("TrainingMine")
            .Produces<MyTrainingDto>()
            .Produces(StatusCodes.Status404NotFound);

        mine.MapPost("/", RequestAsync)
            .WithName("TrainingRequest")
            .Produces<TraineeTrainingDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        mine.MapGet("/{id:long}", ReadAsync)
            .WithName("TrainingMineOne")
            .Produces<TraineeTrainingDto>()
            .Produces(StatusCodes.Status404NotFound);

        mine.MapPost("/{id:long}/cancel", CancelAsync)
            .WithName("TrainingCancel")
            .Produces<TraineeTrainingDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> MineAsync(TrainingRequests requests, HttpContext http) =>
        await requests.MineAsync(http.RequestAborted) is { } mine ? Results.Ok(mine) : Results.NotFound();

    /// <summary>
    /// A request: 201 with the training written — a request refused by the hub for the theory is one, and its state says so —,
    /// or 400 with the refusals.
    /// </summary>
    private static async Task<IResult> RequestAsync(
        TrainingRequestWriteDto payload,
        TrainingRequests requests,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var (training, problems) = await requests.RequestAsync(payload, http.RequestAborted);

        return training is null
            ? CrudProblems.Validation(problems!, new Dictionary<string, string[]>(), catalog, currentUser.Locale)
            : Results.Created($"{Pattern}/{training.Id}", requests.ToDto(training));
    }

    private static async Task<IResult> ReadAsync(long id, TrainingRequests requests, HttpContext http) =>
        await requests.OwnAsync(id, tracked: false, http.RequestAborted) is { } training
            ? Results.Ok(requests.ToDto(training))
            : Results.NotFound();

    private static async Task<IResult> CancelAsync(
        long id,
        TrainingCancellation payload,
        TrainingRequests requests,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var training = await requests.OwnAsync(id, tracked: true, http.RequestAborted);
        if (training is null)
        {
            return Results.NotFound();
        }

        try
        {
            var problems = await requests.CancelAsync(training, payload.RowVersion, http.RequestAborted);
            return problems is null
                ? Results.Ok(requests.ToDto(training))
                : CrudProblems.Validation(problems, new Dictionary<string, string[]>(), catalog, currentUser.Locale);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ConflictTitleKey));
        }
    }
}
