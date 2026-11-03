using System.Data;
using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>
/// The legs of a tour (design M2 §1.4, §8.4): <b>the declared exception</b> to the list and form engine (plan 0.79,
/// §16.6). The editor is a table where every row is saved by itself with its version, and inserting, deleting and
/// retiring change the numbers of the rows around it — which no generated form does. Eight hand written verbs, counted:
/// read the grid, add a leg (after another, or at the end), change one, ask what removing it would do, remove it,
/// restore it; and the import of a file (T8), previewed and then applied. Every write answers with the whole grid,
/// renumbered, so the editor never guesses a number.
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

        group.MapPost("/import/preview", PreviewImportAsync)
            .WithName("FlightOpsLegImportPreview")
            .Produces<LegImportPreviewDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.Edit);

        group.MapPost("/import", ImportAsync)
            .WithName("FlightOpsLegImport")
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

    /// <summary>
    /// The differences between the file and the shape of the tour, written nowhere (design M2 §8.4): what is added,
    /// changed, restored, kept, deleted and retired, and whether the import owes a reason. On a hub tour, by rotation, and
    /// the hubs added and deleted (note 2026-10-28-hub-e-rotazioni-da-file).
    /// </summary>
    private static async Task<IResult> PreviewImportAsync(long tourId, LegImportRequest body, LegRequest request, HttpContext http)
    {
        var (tour, refused) = await request.WritableTourAsync(tourId, http);
        if (tour is null)
        {
            return refused!;
        }

        var (shape, plan, refusal) = await PlanImportAsync(tour, body, request, http);
        if (plan is null)
        {
            return refusal!;
        }

        var hubs = tour.Kind == TourKind.Hub;
        return Results.Ok(new LegImportPreviewDto(
            plan.Lines(),
            plan.ReasonRequired,
            shape!.Fingerprint,
            hubs ? plan.HubsAdded : null,
            hubs ? [.. plan.DeletedHubs.Select(hub => hub.Icao)] : null));
    }

    /// <summary>
    /// The import, in one transaction: the new hubs, then the new rotations — a new leg goes into a rotation that has an
    /// identifier —, then the legs the file matches take its values, the new ones are added, the absent units are left,
    /// deleted or retired by the mode (§1.4.1), every number is the file's order; the rotations and hubs the plan removes go.
    /// Refused if the shape is no longer the one the preview looked at; a ready tour stays ready or nothing is written, as
    /// with every write, and a refusal rolls everything back.
    /// <para>The tour's row is locked first (<c>FOR UPDATE</c>, under READ COMMITTED): a second import of the same tour waits
    /// for the first, then reads the shape the first left, and is a 409 — never two new hubs of one ICAO.</para>
    /// </summary>
    private static async Task<IResult> ImportAsync(long tourId, LegImportRequest body, LegRequest request, HttpContext http)
    {
        var (tour, refused) = await request.WritableTourAsync(tourId, http);
        if (tour is null)
        {
            return refused!;
        }

        var database = request.Database;
        var cancellationToken = http.RequestAborted;
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await database.Database.ExecuteSqlAsync($"SELECT id FROM fo_tours WHERE id = {tour.Id} FOR UPDATE", cancellationToken);

        var (shape, plan, refusal) = await PlanImportAsync(tour, body, request, http);
        if (plan is null)
        {
            return refusal!;
        }

        if (body.Fingerprint != shape!.Fingerprint)
        {
            return request.Conflict();
        }

        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
        if (plan.ReasonRequired && reason is null)
        {
            return request.Refused("reason", "flightops:errors.importReasonRequired");
        }

        // As a restore by hand: nothing comes back into a tour nobody can start any more (§1.4.1).
        if (plan.Restores && TourState.Of(tour, request.Clock.UtcNow) is TourStateKind.Closing or TourStateKind.Closed)
        {
            return request.Refused("rows", "flightops:errors.tourClosed");
        }

        plan.Place();

        var newHubs = plan.Hubs.Where(hub => hub.Id == 0).ToList();
        foreach (var hub in newHubs)
        {
            hub.TourId = tour.Id;
            hub.OwnerDepartment = tour.OwnerDepartment;
            hub.OwnerDepartmentMask = tour.OwnerDepartmentMask;
            database.Hubs.Add(hub);
        }

        if (newHubs.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        var hubIds = plan.Hubs.ToDictionary(hub => hub.Icao, hub => hub.Id, StringComparer.Ordinal);
        var newRotations = plan.Units.Where(unit => unit.Rotation is { Id: 0 }).ToList();
        foreach (var unit in newRotations)
        {
            var rotation = unit.Rotation!;
            rotation.TourId = tour.Id;
            rotation.HubId = hubIds[unit.HubIcao!];
            rotation.OwnerDepartment = tour.OwnerDepartment;
            rotation.OwnerDepartmentMask = tour.OwnerDepartmentMask;
            database.Rotations.Add(rotation);
        }

        if (newRotations.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        var (added, deleted) = plan.Apply(request.Clock.UtcNow, reason);
        database.Legs.AddRange(added);
        database.Legs.RemoveRange(deleted);
        database.Rotations.RemoveRange(plan.DeletedRotations);
        database.Hubs.RemoveRange(plan.DeletedHubs);

        var after = shape.Legs.Except(deleted).Concat(added).ToList();
        if (tour.Kind != TourKind.Hub)
        {
            return await request.SaveAsync(tour, after, added, http, transaction: transaction);
        }

        var rotations = plan.Units.Select(unit => unit.Rotation).OfType<Rotation>().ToList();
        return await request.SaveAsync(tour, after, added, http, plan.Hubs, rotations, transaction);
    }

    /// <summary>
    /// What the preview and the import share: every row made a leg by the same code as a leg of the editor, the file checked
    /// whole — on a hub tour by <see cref="FileProblems"/>, on any other a row that names a hub or a rotation is refused —,
    /// refusals under <c>rows[n].field</c>, the shape it is compared with, and the plan.
    /// </summary>
    private static async Task<(ImportShape? Shape, LegImportPlan? Plan, IResult? Refusal)> PlanImportAsync(
        Tour tour,
        LegImportRequest body,
        LegRequest request,
        HttpContext http)
    {
        if (await request.InvalidAsync(body, http) is { } invalid)
        {
            return (null, null, invalid);
        }

        var cancellationToken = http.RequestAborted;
        var isHub = tour.Kind == TourKind.Hub;
        var (incoming, problems) = await RowsAsync(tour, body, request, http);
        var hubs = isHub ? await request.Database.Hubs.Where(hub => hub.TourId == tour.Id).ToListAsync(cancellationToken) : [];

        if (isHub)
        {
            FileProblems(body.Rows, incoming, hubs, problems);
        }
        else
        {
            for (var row = 0; row < body.Rows.Count; row++)
            {
                if (Named(body.Rows[row].Hub) is not null || Named(body.Rows[row].Rotation) is not null)
                {
                    problems[$"rows[{row}].hub"] = ["flightops:errors.legHubOnly"];
                }
            }
        }

        if (problems.Count > 0)
        {
            return (null, null, request.Refused(problems));
        }

        var rotations = isHub
            ? await request.Database.Rotations.Where(rotation => rotation.TourId == tour.Id).ToListAsync(cancellationToken)
            : [];
        var legs = await request.Book.LegsAsync(tour.Id, cancellationToken);
        var withReports = await request.Reports.LegsWithReportsAsync(tour.Id, cancellationToken);
        var rows = incoming
            .Select((leg, row) => new ImportRow(row, leg!, Hub(body.Rows[row].Hub), Named(body.Rows[row].Rotation)))
            .ToList();

        return (new ImportShape(legs, hubs, rotations), LegImportPlan.Make(hubs, rotations, legs, rows, body.Mode, withReports), null);
    }

    /// <summary>Every row made a leg by the same code as a leg of the editor; a row that cannot be is null, its refusals filed under it.</summary>
    private static async Task<(List<Leg?> Legs, Dictionary<string, string[]> Problems)> RowsAsync(
        Tour tour,
        LegImportRequest body,
        LegRequest request,
        HttpContext http)
    {
        var problems = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var incoming = new List<Leg?>();
        for (var row = 0; row < body.Rows.Count; row++)
        {
            var line = body.Rows[row];
            var connection = tour.Kind == TourKind.Hub && Hub(line.Hub) is null;
            var payload = new LegWriteDto(
                line.DepartureIcao ?? string.Empty,
                line.ArrivalIcao ?? string.Empty,
                line.Callsigns,
                line.FlightNumbers,
                new AllowedAircraft(line.AircraftTypes ?? [], []),
                line.ReleaseAt,
                ChangeReason: null,
                RowVersion: default,
                Kind: connection ? LegKind.HubConnection : LegKind.Normal);

            var leg = new Leg();
            var found = request.Check(payload)
                ?? await request.Book.ApplyAsync(tour, leg, payload, hasReports: false, http.RequestAborted);
            if (found is null)
            {
                incoming.Add(leg);
                continue;
            }

            incoming.Add(null);
            foreach (var (field, keys) in found)
            {
                problems[$"rows[{row}].{field}"] = keys;
            }
        }

        return (incoming, problems);
    }

    /// <summary>
    /// The shape of a hub tour's file, which no row says by itself: a hub with a rotation and a rotation with a hub; the
    /// rows of a rotation together, of a size a rotation has; on the legs the rows made, the rules of
    /// <see cref="TourShape.RotationProblem"/> and <see cref="TourShape.ConnectsHubs"/>, the shape's own, with the hubs of the
    /// file and of the tour. Filed under the row and the column the file shows (the first row of a rotation for the rotation).
    /// </summary>
    private static void FileProblems(
        IReadOnlyList<LegImportRowDto> rows,
        IReadOnlyList<Leg?> legs,
        IReadOnlyList<TourHub> tourHubs,
        Dictionary<string, string[]> problems)
    {
        var hubs = rows.Select(row => Hub(row.Hub)).OfType<string>()
            .Concat(tourHubs.Select(hub => hub.Icao))
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Refuse(int row, string field, string key) => problems.TryAdd($"rows[{row}].{field}", [key]);

        var start = 0;
        while (start < rows.Count)
        {
            var hub = Hub(rows[start].Hub);
            var label = Named(rows[start].Rotation);

            if (hub is null)
            {
                if (label is not null)
                {
                    Refuse(start, "hub", "errors.required");
                }
                else if (legs[start] is { } connection && !TourShape.ConnectsHubs(connection, hubs))
                {
                    Refuse(start, "hub", "flightops:errors.connectionNotBetweenHubs");
                }

                start++;
                continue;
            }

            if (label is null)
            {
                Refuse(start, "rotation", "errors.required");
                start++;
                continue;
            }

            var end = start;
            while (end + 1 < rows.Count && Hub(rows[end + 1].Hub) == hub && Named(rows[end + 1].Rotation) == label)
            {
                end++;
            }

            var size = end - start + 1;
            var rotation = legs.Skip(start).Take(size).OfType<Leg>().ToList();

            // The same rotation further down is a second piece of it, not a second rotation with the same name.
            if (!seen.Add($"{hub}|{label}"))
            {
                Refuse(start, "rotation", "flightops:errors.importRotationSplit");
            }
            else if (!Rotation.Sizes.Contains(size))
            {
                Refuse(start, "rotation", "flightops:errors.rotationSizeChoice");
            }
            else if (rotation.Count == size && TourShape.RotationProblem(hub, size, rotation) is { } key)
            {
                Refuse(start, "rotation", key);
            }

            start = end + 1;
        }
    }

    /// <summary>The shape of the tour an import is compared with, tracked, and the fingerprint it is made of.</summary>
    private sealed record ImportShape(List<Leg> Legs, List<TourHub> Hubs, List<Rotation> Rotations)
    {
        public string Fingerprint => LegImportPlan.Fingerprint(Legs, Hubs, Rotations);
    }

    private static string? Named(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Hub(string? value) => Named(value)?.ToUpperInvariant();

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

        if (tour.PurgedAt is not null)
        {
            return (null, Refused("tourId", TourState.PurgedKey));
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
    /// then the runways of what was written fetched, and the grid answered. The shape is checked as the write leaves it:
    /// the legs, and the hubs and rotations when the write changes them too (the import of a hub tour). A write inside a
    /// transaction has it committed with the save; a refusal leaves it to the caller to roll back.
    /// </summary>
    public async Task<IResult> SaveAsync(
        Tour tour,
        IReadOnlyList<Leg> legs,
        IReadOnlyList<Leg> written,
        HttpContext http,
        IReadOnlyList<TourHub>? hubs = null,
        IReadOnlyList<Rotation>? rotations = null,
        IDbContextTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(http);

        LegBook.SequenceRotations(legs);

        if (tour.Status == PublishStatus.Published)
        {
            var problems = await readiness.ProblemsAsync(
                tour,
                parts => parts with { Legs = legs, Hubs = hubs ?? parts.Hubs, Rotations = rotations ?? parts.Rotations },
                http.RequestAborted);
            if (!problems.IsEmpty)
            {
                return CrudProblems.Validation(problems, catalog, currentUser.Locale);
            }
        }

        await database.SaveChangesAsync(http.RequestAborted);
        if (transaction is not null)
        {
            await transaction.CommitAsync(http.RequestAborted);
        }

        await book.FetchRunwaysAsync(written, http.RequestAborted);

        return Results.Ok(await book.GridAsync(tour, legs, http.RequestAborted));
    }

    /// <summary>The rules one leg answers by itself, as the payload's validator says them; null when there are none.</summary>
    public Dictionary<string, string[]>? Check(LegWriteDto payload)
    {
        var result = new LegWriteDtoValidator().Validate(payload);
        return result.IsValid
            ? null
            : result.Errors
                .GroupBy(error => CrudProblems.FieldName(error.PropertyName), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray(),
                    StringComparer.Ordinal);
    }

    public IResult Conflict() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: catalog.Resolve(currentUser.Locale, CrudProblems.ConflictTitleKey));

    public IResult Refused(string field, string key) =>
        Refused(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [key] });

    public IResult Refused(IReadOnlyDictionary<string, string[]> errors) =>
        CrudProblems.Validation(errors, new Dictionary<string, string[]>(StringComparer.Ordinal), catalog, currentUser.Locale);

    public IResult NotFound() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: catalog.Resolve(currentUser.Locale, CrudProblems.NotFoundTitleKey));
}
