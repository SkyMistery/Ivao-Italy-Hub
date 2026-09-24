using System.Globalization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Agent;

/// <summary>
/// The agent's endpoints (design M2 §6.6, T19b; <c>docs/agent-contract.md</c>): the contract, open to anybody — an agent asks it
/// before it has a token —, and the queue, a report and its results, opened only by a personal token of
/// <see cref="AgentContract.Audience"/> and only with an accepted <c>Hub-Agent-Contract</c>. Not the CRUD engine: a program's
/// contract, frozen in a version, is the exception of the plan's §16.10 and is counted with the endpoints written by hand.
/// </summary>
public static class AgentEndpoints
{
    public const string Pattern = "/api/flightops/agent";

    public const string ContractPattern = "/api/flightops/agent/contract";

    public const string PirepsPattern = "/api/flightops/agent/pireps";

    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ContractPattern, (AgentDesk desk, HttpContext http) =>
            {
                http.Response.Headers[AgentContract.Header] = AgentContract.Current.ToString(CultureInfo.InvariantCulture);
                return TypedResults.Ok(desk.Contract());
            })
            .AllowAnonymous()
            .WithTags("FlightOpsAgent")
            .WithName("FlightOpsAgentContract");

        var agent = app.MapGroup(Pattern)
            .WithTags("FlightOpsAgent")
            .RequireAuthorization(PersonalTokenPolicy.For(AgentContract.Audience))
            .AddEndpointFilter(AgentContract.RequireVersionAsync);

        agent.MapGet("/pireps", async (bool? pending, AgentDesk desk, HttpContext http) =>
                TypedResults.Ok(await desk.QueueAsync(pending ?? false, http.RequestAborted)))
            .WithName("FlightOpsAgentQueue")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        agent.MapGet("/pireps/{id:long}", ReadAsync)
            .WithName("FlightOpsAgentPirep")
            .Produces<AgentPirepDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        agent.MapPost("/pireps/{id:long}/checks", WriteAsync)
            .WithName("FlightOpsAgentChecks")
            .Produces<AgentChecksWrittenDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ReadAsync(long id, AgentDesk desk, HttpContext http)
    {
        var pirep = await desk.FindAsync(id, tracked: false, http.RequestAborted);
        if (pirep is null)
        {
            return Results.NotFound();
        }

        // The page lets anybody who validates one tour read every report (§4.1); the agent reads only what its member decides.
        return await desk.MayValidateAsync(pirep)
            ? Results.Ok(await desk.ReadAsync(pirep, http.RequestAborted))
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> WriteAsync(
        long id,
        AgentChecksWriteDto body,
        AgentDesk desk,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var pirep = await desk.FindAsync(id, tracked: true, http.RequestAborted);
        if (pirep is null)
        {
            return Results.NotFound();
        }

        try
        {
            var (result, problems, written) = await desk.WriteAsync(pirep, body, http.RequestAborted);
            return result switch
            {
                AgentWriteResult.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                AgentWriteResult.NotWaiting => Conflict(catalog, currentUser, "errors.agentNotWaiting", "agentNotWaiting"),
                AgentWriteResult.Refused => CrudProblems.Validation(problems!, new Dictionary<string, string[]>(), catalog, currentUser.Locale),
                _ => Results.Ok(written),
            };
        }
        catch (DbUpdateException)
        {
            // Two runs on the same report at once, or a decision meanwhile: the agent reads it again and sends again.
            return Conflict(catalog, currentUser, CrudProblems.ConflictTitleKey, "conflict");
        }
    }

    private static IResult Conflict(LocaleCatalog catalog, ICurrentUser currentUser, string titleKey, string code) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: catalog.Resolve(currentUser.Locale, titleKey),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code });
}
