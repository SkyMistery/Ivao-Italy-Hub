using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.FlightOps.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>
/// A pilot's reports (design M2 §3): the sessions to choose from, the send, the pilot's own place in a tour, reading one of
/// their reports, correcting it and withdrawing it. Six hand written verbs, because a report is not a form over a row — it
/// is a choice among flights the tracker lists and a set of checks across the pilot's other reports — and the answer to
/// every refusal is the <c>ProblemDetails</c> of the generated forms, field by field.
/// <para>Any signed in member: a pilot is not a role. Every verb reads only the caller's own reports, on a tour the public
/// can see — a hidden tour is gone for its pilots too (§1.2.2). The staff's side of a report is T13's.</para>
/// </summary>
public static class PirepEndpoints
{
    public const string TourPattern = "/api/flightops/tours/{tourId:long}/reports";

    public const string Pattern = "/api/flightops/reports";

    public static IEndpointRouteBuilder MapPirepEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var tour = app.MapGroup(TourPattern).WithTags("FlightOpsReports").RequireAuthorization(HubPolicies.SignedIn);

        tour.MapGet("/sessions", SessionsAsync)
            .WithName("FlightOpsReportSessions")
            .Produces<IReadOnlyList<TrackerSessionDto>>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        tour.MapGet("/atc", AtcAsync)
            .WithName("FlightOpsReportAtc")
            .Produces<AtcProposalDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        tour.MapGet("/mine", MineAsync)
            .WithName("FlightOpsReportsMine")
            .Produces<MyTourDto>()
            .Produces(StatusCodes.Status404NotFound);

        tour.MapPost("/", SendAsync)
            .WithName("FlightOpsReportSend")
            .Produces<PirepDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        var report = app.MapGroup(Pattern).WithTags("FlightOpsReports").RequireAuthorization(HubPolicies.SignedIn);

        report.MapGet("/{id:long}", ReadAsync)
            .WithName("FlightOpsReport")
            .Produces<PirepDto>()
            .Produces(StatusCodes.Status404NotFound);

        report.MapPut("/{id:long}", CorrectAsync)
            .WithName("FlightOpsReportCorrect")
            .Produces<PirepDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        report.MapPost("/{id:long}/withdraw", WithdrawAsync)
            .WithName("FlightOpsReportWithdraw")
            .Produces<PirepDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>
    /// The sessions of the caller the tour may take: between the airports of <c>?legId=</c>, or of <c>?departure=</c> and
    /// <c>?arrival=</c> — the flight after a diversion —, or anywhere on an <c>Open</c> tour.
    /// </summary>
    private static async Task<IResult> SessionsAsync(
        long tourId,
        long? legId,
        string? departure,
        string? arrival,
        PirepSubmission submission,
        FlightOpsDbContext database,
        HttpContext http)
    {
        var pilot = await submission.TourAsync(tourId, http.RequestAborted);
        if (pilot is null)
        {
            return Results.NotFound();
        }

        var leg = legId is { } id
            ? await database.Legs.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id && row.TourId == tourId, http.RequestAborted)
            : null;
        if (legId is not null && leg is null)
        {
            return Results.NotFound();
        }

        var sessions = await submission.SessionsAsync(pilot, leg, Code(departure), Code(arrival), http.RequestAborted);

        // "We could not look" is not "there is nothing": a pilot sure of having flown is not told there is no flight (T2).
        return sessions is null ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable) : Results.Ok(sessions);
    }

    /// <summary>
    /// The controllers online along the flights of <c>?sessionIds=</c> — two for a diversion, with <c>?diversionIcao=</c> —,
    /// proposed to the pilot while they fill the form in (§3.3). An archive that is missing is <c>Available: false</c>, not
    /// an error: the form works the same.
    /// </summary>
    private static async Task<IResult> AtcAsync(
        long tourId,
        [FromQuery] long[]? sessionIds,
        string? diversionIcao,
        PirepSubmission submission,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var pilot = await submission.TourAsync(tourId, http.RequestAborted);
        if (pilot is null)
        {
            return Results.NotFound();
        }

        var (proposal, problems) = await submission.ProposeAsync(pilot, sessionIds ?? [], diversionIcao, http.RequestAborted);

        return proposal is null
            ? CrudProblems.Validation(problems!, new Dictionary<string, string[]>(), catalog, currentUser.Locale)
            : Results.Ok(proposal);
    }

    private static async Task<IResult> MineAsync(long tourId, PirepSubmission submission, HttpContext http)
    {
        var pilot = await submission.TourAsync(tourId, http.RequestAborted);
        return pilot is null ? Results.NotFound() : Results.Ok(await submission.MineAsync(pilot, http.RequestAborted));
    }

    private static async Task<IResult> SendAsync(
        long tourId,
        PirepWriteDto payload,
        PirepSubmission submission,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var pilot = await submission.TourAsync(tourId, http.RequestAborted);
        if (pilot is null)
        {
            return Results.NotFound();
        }

        var (pirep, problems) = await submission.SendAsync(pilot, payload, correcting: null, http.RequestAborted);

        return pirep is null
            ? CrudProblems.Validation(problems!, new Dictionary<string, string[]>(), catalog, currentUser.Locale)
            : Results.Created($"{Pattern}/{pirep.Id}", PirepSubmission.ToDto(pirep));
    }

    private static async Task<IResult> ReadAsync(long id, PirepSubmission submission, FlightOpsDbContext database, ICurrentUser currentUser, HttpContext http)
    {
        var pirep = await OwnAsync(id, database, currentUser, tracked: false, http);
        return pirep is null || await submission.TourAsync(pirep.TourId, http.RequestAborted) is null
            ? Results.NotFound()
            : Results.Ok(PirepSubmission.ToDto(pirep));
    }

    /// <summary>A report «to modify», corrected and sent again (§3.1): everything may change but the leg; it goes back to the queue.</summary>
    private static async Task<IResult> CorrectAsync(
        long id,
        PirepWriteDto payload,
        PirepSubmission submission,
        FlightOpsDbContext database,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var pirep = await OwnAsync(id, database, currentUser, tracked: true, http);
        var pilot = pirep is null ? null : await submission.TourAsync(pirep.TourId, http.RequestAborted);
        if (pirep is null || pilot is null)
        {
            return Results.NotFound();
        }

        if (pirep.Status != PirepStatus.ToModify)
        {
            return CrudProblems.Validation(
                new Dictionary<string, string[]> { ["status"] = ["flightops:errors.reportNotCorrectable"] },
                new Dictionary<string, string[]>(),
                catalog,
                currentUser.Locale);
        }

        try
        {
            var (sent, problems) = await submission.SendAsync(pilot, payload, pirep, http.RequestAborted);
            return sent is null
                ? CrudProblems.Validation(problems!, new Dictionary<string, string[]>(), catalog, currentUser.Locale)
                : Results.Ok(PirepSubmission.ToDto(sent));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(catalog, currentUser);
        }
    }

    private static async Task<IResult> WithdrawAsync(
        long id,
        PirepWithdrawal payload,
        PirepSubmission submission,
        FlightOpsDbContext database,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var pirep = await OwnAsync(id, database, currentUser, tracked: true, http);
        if (pirep is null || await submission.TourAsync(pirep.TourId, http.RequestAborted) is null)
        {
            return Results.NotFound();
        }

        try
        {
            var problems = await submission.WithdrawAsync(pirep, payload.RowVersion, http.RequestAborted);
            return problems is null
                ? Results.Ok(PirepSubmission.ToDto(pirep))
                : CrudProblems.Validation(problems, new Dictionary<string, string[]>(), catalog, currentUser.Locale);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(catalog, currentUser);
        }
    }

    /// <summary>The caller's own report, with its flights; nobody else's, whatever they hold (the staff's side is T13).</summary>
    private static Task<Pirep?> OwnAsync(long id, FlightOpsDbContext database, ICurrentUser currentUser, bool tracked, HttpContext http)
    {
        var reports = tracked ? database.Pireps : database.Pireps.AsNoTracking();
        return reports
            .Include(report => report.Flights)
            .Include(report => report.Errors)
            .FirstOrDefaultAsync(report => report.Id == id && report.Vid == currentUser.Vid, http.RequestAborted);
    }

    private static IResult Conflict(LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: catalog.Resolve(currentUser.Locale, CrudProblems.ConflictTitleKey));

    private static string? Code(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToUpperInvariant();
}

/// <summary>The version of the report the pilot saw when they pressed «withdraw».</summary>
public sealed record PirepWithdrawal(DateTime RowVersion);
