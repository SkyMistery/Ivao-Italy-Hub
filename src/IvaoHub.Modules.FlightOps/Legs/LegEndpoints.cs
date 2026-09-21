using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>
/// The legs of a tour (design M2 §1.4, §8.4): <b>the declared exception</b> to the list and form engine (plan 0.79,
/// §16.6). The editor is a table where every row is saved by itself with its version, and inserting, deleting and
/// retiring change the numbers of the rows around it — which no generated form does. Six hand written verbs, counted:
/// read the grid, add a leg (after another, or at the end), change one, ask what removing it would do, remove it,
/// restore it. Every write answers with the whole grid, renumbered, so the editor never guesses a number.
/// <para>The resource is the <b>tour</b>: reading asks <c>Tours.View</c> on it, writing <c>Tours.Edit</c>, through the
/// one handler. A leg carries the tour's departments (Carmine, 18 September 2026), so the interceptor's guard reads it
/// the same way. A template has no legs (§1.10), and neither has an <c>Open</c> tour or a container (§2.6, §2.7).</para>
/// <para>A write of a leg of a <b>ready</b> tour passes the checks of "ready" with the legs as it leaves them, as every
/// save of a ready tour does (T6a): a ready tour stays ready only as long as it could be marked ready.</para>
/// </summary>
public static class LegEndpoints
{
    public const string Pattern = "/api/flightops/tours/{tourId:long}/legs";

    public static IEndpointRouteBuilder MapLegEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Pattern).WithTags("FlightOpsLegs");

        group.MapGet("/", ReadAsync)
            .WithName("FlightOpsLegs")
            .Produces<TourLegsDto>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.View);

        group.MapPost("/", CreateAsync)
            .WithName("FlightOpsLegCreate")
            .Produces<TourLegsDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.Edit);

        group.MapPut("/{legId:long}", UpdateAsync)
            .WithName("FlightOpsLegUpdate")
            .Produces<TourLegsDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(TourPermissions.Edit);

        group.MapGet("/{legId:long}/removal", RemovalAsync)
            .WithName("FlightOpsLegRemoval")
            .Produces<LegRemovalDto>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.Edit);

        group.MapPost("/{legId:long}/remove", RemoveAsync)
            .WithName("FlightOpsLegRemove")
            .Produces<TourLegsDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(TourPermissions.Edit);

        group.MapPost("/{legId:long}/restore", RestoreAsync)
            .WithName("FlightOpsLegRestore")
            .Produces<TourLegsDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(TourPermissions.Edit);

        return app;
    }

    private static async Task<IResult> ReadAsync(long tourId, LegRequest request, HttpContext http)
    {
        var (tour, refusal) = await request.TourAsync(tourId, TourPermissions.View, http);
        if (tour is null)
        {
            return refusal!;
        }

        var legs = await request.Book.LegsAsync(tourId, http.RequestAborted);

        return Results.Ok(await request.Book.GridAsync(tour, legs, http.RequestAborted));
    }

    /// <summary>A new leg, after <c>?after=</c> — the editor's "duplicate", "follows" and "close the tour" — or at the end.</summary>
    private static async Task<IResult> CreateAsync(long tourId, long? after, LegWriteDto payload, LegRequest request, HttpContext http)
    {
        var (tour, refusal) = await request.WritableTourAsync(tourId, http);
        if (tour is null)
        {
            return refusal!;
        }

        if (await request.InvalidAsync(payload, http) is { } invalid)
        {
            return invalid;
        }

        var legs = await request.Book.LegsAsync(tourId, http.RequestAborted);
        var previous = after is { } afterId ? legs.FirstOrDefault(leg => leg.Id == afterId) : null;
        if (after is not null && previous is null)
        {
            return request.Refused("after", "flightops:errors.legUnknown");
        }

        var leg = new Leg { Kind = LegKind.Normal };
        if (await request.Book.ApplyAsync(tour, leg, payload, hasReports: false, http.RequestAborted) is { } problems)
        {
            return request.Refused(problems);
        }

        // Room after the one it follows; at the end otherwise.
        if (previous is not null)
        {
            foreach (var later in legs.Where(row => row.Number > previous.Number))
            {
                later.Number++;
            }

            leg.Number = previous.Number + 1;
        }
        else
        {
            leg.Number = legs.Count == 0 ? 1 : legs.Max(row => row.Number) + 1;
        }

        legs.Add(leg);
        LegBook.Renumber(legs);
        request.Database.Legs.Add(leg);

        return await request.SaveAsync(tour, legs, [leg], http);
    }

    private static async Task<IResult> UpdateAsync(long tourId, long legId, LegWriteDto payload, LegRequest request, HttpContext http)
    {
        var (tour, refusal) = await request.WritableTourAsync(tourId, http);
        if (tour is null)
        {
            return refusal!;
        }

        if (await request.InvalidAsync(payload, http) is { } invalid)
        {
            return invalid;
        }

        var legs = await request.Book.LegsAsync(tourId, http.RequestAborted);
        var leg = legs.FirstOrDefault(row => row.Id == legId);
        if (leg is null)
        {
            return request.NotFound();
        }

        var withReports = await request.Reports.LegsWithReportsAsync(tourId, http.RequestAborted);
        if (await request.Book.ApplyAsync(tour, leg, payload, withReports.Contains(leg.Id), http.RequestAborted) is { } problems)
        {
            return request.Refused(problems);
        }

        request.CarryVersion(leg, payload.RowVersion);

        return await request.SaveAsync(tour, legs, [leg], http);
    }

    /// <summary>What removing the leg will do, asked before the editor confirms: decided here, never by the editor (§1.4.1).</summary>
    private static async Task<IResult> RemovalAsync(long tourId, long legId, LegRequest request, HttpContext http)
    {
        var (tour, refusal) = await request.WritableTourAsync(tourId, http);
        if (tour is null)
        {
            return refusal!;
        }

        var legs = await request.Book.LegsAsync(tourId, http.RequestAborted);
        var leg = legs.FirstOrDefault(row => row.Id == legId);
        if (leg is null)
        {
            return request.NotFound();
        }

        var withReports = await request.Reports.LegsWithReportsAsync(tourId, http.RequestAborted);
        var (outcome, touched) = Removal(leg, legs, withReports);

        return Results.Ok(new LegRemovalDto(outcome, [.. touched.Select(row => row.Number).Order()]));
    }

    /// <summary>
    /// Deleted when no report points at it, and the legs after it renumbered; retired with a reason otherwise — and
    /// with it the whole rotation it belongs to, which is never retired one leg at a time (Carmine, 15 September 2026).
    /// </summary>
    private static async Task<IResult> RemoveAsync(long tourId, long legId, LegReasonRequest body, LegRequest request, HttpContext http)
    {
        var (tour, refusal) = await request.WritableTourAsync(tourId, http);
        if (tour is null)
        {
            return refusal!;
        }

        if (await request.InvalidAsync(body, http) is { } invalid)
        {
            return invalid;
        }

        var legs = await request.Book.LegsAsync(tourId, http.RequestAborted);
        var leg = legs.FirstOrDefault(row => row.Id == legId);
        if (leg is null)
        {
            return request.NotFound();
        }

        request.CarryVersion(leg, body.RowVersion);
        var withReports = await request.Reports.LegsWithReportsAsync(tourId, http.RequestAborted);
        var (outcome, touched) = Removal(leg, legs, withReports);
        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();

        if (outcome == LegRemovalOutcome.Delete)
        {
            request.Database.Legs.Remove(leg);
            legs.Remove(leg);
            LegBook.Renumber(legs);

            return await request.SaveAsync(tour, legs, [], http);
        }

        if (leg.IsRetired)
        {
            return request.Refused("id", "flightops:errors.legAlreadyRetired");
        }

        if (reason is null)
        {
            return request.Refused("reason", "flightops:errors.retireReasonRequired");
        }

        var now = request.Clock.UtcNow;
        foreach (var row in touched.Where(row => !row.IsRetired))
        {
            row.RetiredAt = now;
            row.RetiredReason = reason;
        }

        return await request.SaveAsync(tour, legs, [], http);
    }

    /// <summary>A retired leg back in the tour, with a reason, while the tour is not closed (§1.4.1); a rotation comes back whole.</summary>
    private static async Task<IResult> RestoreAsync(long tourId, long legId, LegReasonRequest body, LegRequest request, HttpContext http)
    {
        var (tour, refusal) = await request.WritableTourAsync(tourId, http);
        if (tour is null)
        {
            return refusal!;
        }

        if (await request.InvalidAsync(body, http) is { } invalid)
        {
            return invalid;
        }

        var legs = await request.Book.LegsAsync(tourId, http.RequestAborted);
        var leg = legs.FirstOrDefault(row => row.Id == legId);
        if (leg is null)
        {
            return request.NotFound();
        }

        if (!leg.IsRetired)
        {
            return request.Refused("id", "flightops:errors.legNotRetired");
        }

        // "Closing" looks closed to everybody: nothing comes back into a tour nobody can start any more.
        if (TourState.Of(tour, request.Clock.UtcNow) is TourStateKind.Closing or TourStateKind.Closed)
        {
            return request.Refused("id", "flightops:errors.tourClosed");
        }

        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
        if (reason is null)
        {
            return request.Refused("reason", "flightops:errors.restoreReasonRequired");
        }

        request.CarryVersion(leg, body.RowVersion);

        foreach (var row in leg.RotationId is { } rotation ? legs.Where(row => row.RotationId == rotation) : [leg])
        {
            row.RetiredAt = null;
            row.RetiredReason = null;
            row.ChangeReason = reason;
        }

        return await request.SaveAsync(tour, legs, [], http);
    }

    /// <summary>The rule of §1.4.1, once: which outcome, and which legs it touches.</summary>
    private static (LegRemovalOutcome Outcome, IReadOnlyList<Leg> Touched) Removal(Leg leg, IReadOnlyList<Leg> legs, IReadOnlySet<long> withReports)
    {
        if (!withReports.Contains(leg.Id))
        {
            return (LegRemovalOutcome.Delete, [leg]);
        }

        return leg.RotationId is { } rotation
            ? (LegRemovalOutcome.RetireRotation, [.. legs.Where(row => row.RotationId == rotation)])
            : (LegRemovalOutcome.Retire, [leg]);
    }
}

/// <summary>
/// What every verb of the legs is handed, and what they share: the tour found and authorised, the payload validated,
/// a refusal said the way the form reads it, and the save — with the checks of "ready" when the tour is ready.
/// </summary>
public sealed class LegRequest(
    FlightOpsDbContext database,
    LegBook book,
    TourReadiness readiness,
    ITourReports reports,
    IAuthorizationService authorization,
    IServiceProvider services,
    ICurrentUser currentUser,
    LocaleCatalog catalog,
    IClock clock)
{
    public FlightOpsDbContext Database => database;

    public LegBook Book => book;

    public ITourReports Reports => reports;

    public IClock Clock => clock;

    /// <summary>The tour, if it exists and the caller holds this permission on it; the answer to give otherwise.</summary>
    public async Task<(Tour? Tour, IResult? Refusal)> TourAsync(long tourId, string permission, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        var tour = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == tourId, http.RequestAborted);
        if (tour is null)
        {
            return (null, NotFound());
        }

        if (!(await authorization.AuthorizeAsync(http.User, tour, permission)).Succeeded)
        {
            return (null, Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ForbiddenTitleKey)));
        }

        return (tour, null);
    }

    /// <summary>The tour, writable, and of a kind that has legs.</summary>
    public async Task<(Tour? Tour, IResult? Refusal)> WritableTourAsync(long tourId, HttpContext http)
    {
        var (tour, refusal) = await TourAsync(tourId, TourPermissions.Edit, http);
        if (tour is null)
        {
            return (null, refusal);
        }

        if (tour.IsTemplate)
        {
            return (null, Refused("tourId", "flightops:errors.templateHasNoLegs"));
        }

        return TourShape.HasLegs(tour.Kind) ? (tour, null) : (null, Refused("tourId", "flightops:errors.kindHasNoLegs"));
    }

    public async Task<IResult?> InvalidAsync<TPayload>(TPayload payload, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        if (services.GetService(typeof(IValidator<TPayload>)) is not IValidator<TPayload> validator)
        {
            return null;
        }

        var result = await validator.ValidateAsync(payload, http.RequestAborted);
        return result.IsValid ? null : CrudProblems.Validation(result, catalog, currentUser.Locale);
    }

    /// <summary>The version the editor saw, in the <c>WHERE</c> of the update: a stale one is a 409 (design M0 §3.9).</summary>
    public void CarryVersion(Leg leg, DateTime rowVersion)
    {
        if (rowVersion != default)
        {
            database.Entry(leg).Property(row => row.RowVersion).OriginalValue = rowVersion;
        }
    }

    /// <summary>
    /// The save of a write: refused with the problems of "ready" when a ready tour would stop being one, then saved,
    /// then the runways of what was written fetched, and the grid answered.
    /// </summary>
    public async Task<IResult> SaveAsync(Tour tour, IReadOnlyList<Leg> legs, IReadOnlyList<Leg> written, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(http);

        LegBook.SequenceRotations(legs);

        if (tour.Status == PublishStatus.Published)
        {
            var problems = await readiness.ProblemsAsync(tour, legs, http.RequestAborted);
            if (!problems.IsEmpty)
            {
                return CrudProblems.Validation(problems.Errors, problems.Localized, catalog, currentUser.Locale);
            }
        }

        await database.SaveChangesAsync(http.RequestAborted);
        await book.FetchRunwaysAsync(written, http.RequestAborted);

        return Results.Ok(await book.GridAsync(tour, legs, http.RequestAborted));
    }

    public IResult Refused(string field, string key) =>
        Refused(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [key] });

    public IResult Refused(IReadOnlyDictionary<string, string[]> errors) =>
        CrudProblems.Validation(errors, new Dictionary<string, string[]>(StringComparer.Ordinal), catalog, currentUser.Locale);

    public IResult NotFound() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: catalog.Resolve(currentUser.Locale, CrudProblems.NotFoundTitleKey));
}
