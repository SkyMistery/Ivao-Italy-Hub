using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Modules.Events.Registers;

/// <summary>
/// The registers and the decisions on them (design M4 §3.7, §4.5, §7.1, §7.2, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings
/// 1, 4, 5, 6 and 8).
/// <list type="bullet">
/// <item><b>Three reads written by hand</b>, because a register is a count per person that no list of rows gives: the controllers'
/// (<c>EventAtc.View</c>), the pilots' (<c>EventBookings.View</c>), and a member's own two (any signed in member, their own alone), which the
/// page that refuses a booking reads to say until when. The rows behind them are the lists of the engine: the shifts, the bookings. The two
/// registers count the whole division, so they answer only whoever holds their permission <b>without a resource scope</b> — a department, a
/// FIR team, a grant from outside, the super administrator —: a grant on one event or one row reads that event's rows in the lists, never
/// every person's counts (Carmine's answer (b) on #283, with the core's <c>HasUnscoped</c> of E10u).</item>
/// <item><b>Two verbs the design names</b> (§7.2: «confermare un no-show»; §3.7: «lo staff toglie una voce solo a mano»): how a shift went, and
/// a booking not flown taken out of its pilot's record — each asked of the one handler on its row (<see cref="RegisterDecisions"/>).</item>
/// </list>
/// </summary>
public static class RegisterEndpoints
{
    public const string ControllersPattern = "/api/events/atc/register";

    public const string PilotsPattern = "/api/events/bookings/register";

    public const string MinePattern = "/api/events/mine/record";

    /// <summary>The key a 409 says: the row changed from elsewhere a moment before, read it again.</summary>
    public const string ChangedKey = "events:errors.registerChanged";

    public static IEndpointRouteBuilder MapRegisterEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ControllersPattern, async (
                    int? page,
                    int? pageSize,
                    string? q,
                    ControllerRegister register,
                    ICurrentUser currentUser,
                    LocaleCatalog catalog,
                    HttpContext http) =>
                currentUser.HasUnscoped(EventsPermissions.AtcView)
                    ? Results.Ok(await register.PageAsync(page ?? 1, pageSize ?? 50, q, http.RequestAborted))
                    : Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser))
            .WithName("EventsControllersRegister")
            .WithTags("EventAtc")
            .Produces<PagedResult<ControllerRegisterRowDto>>()
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization(EventsPermissions.AtcView);

        app.MapGet(PilotsPattern, async (
                    int? page,
                    int? pageSize,
                    string? q,
                    PilotRegister register,
                    ICurrentUser currentUser,
                    LocaleCatalog catalog,
                    IClock clock,
                    HttpContext http) =>
                currentUser.HasUnscoped(EventsPermissions.BookingsView)
                    ? Results.Ok(await register.PageAsync(page ?? 1, pageSize ?? 50, q, clock.UtcNow, http.RequestAborted))
                    : Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser))
            .WithName("EventsPilotsRegister")
            .WithTags("EventBookings")
            .Produces<PagedResult<PilotRegisterRowDto>>()
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization(EventsPermissions.BookingsView);

        app.MapGet(MinePattern, async (
                    long? eventId,
                    PilotRegister pilots,
                    ControllerRegister controllers,
                    ICurrentUser currentUser,
                    IClock clock,
                    HttpContext http) =>
                new MemberRecordDto(
                    await pilots.OfAsync(currentUser.Vid, eventId, clock.UtcNow, http.RequestAborted),
                    await controllers.OfAsync(currentUser.Vid, clock.UtcNow, http.RequestAborted)))
            .WithName("EventsMyRecord")
            .WithTags("EventBookings")
            .Produces<MemberRecordDto>()
            .RequireAuthorization(HubPolicies.SignedIn);

        app.MapPost("/api/events/atc/shifts/{id:long}/attendance", async (
                    long id,
                    AttendanceDecision decision,
                    RegisterDecisions decisions,
                    LocaleCatalog catalog,
                    ICurrentUser currentUser,
                    HttpContext http) =>
                Answer(await decisions.DecideAsync(id, decision, http.User, http.RequestAborted), catalog, currentUser))
            .WithName("EventsDecideAttendance")
            .WithTags("EventAtc")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.AtcEdit);

        app.MapPost("/api/events/bookings/{id:long}/excuse", async (
                    long id,
                    UnflownExcuse excuse,
                    RegisterDecisions decisions,
                    LocaleCatalog catalog,
                    ICurrentUser currentUser,
                    HttpContext http) =>
                Answer(await decisions.ExcuseAsync(id, excuse, http.User, http.RequestAborted), catalog, currentUser))
            .WithName("EventsExcuseUnflown")
            .WithTags("EventBookings")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.BookingsEdit);

        return app;
    }

    private static IResult Answer((DecisionResult Result, Refusals Problems) outcome, LocaleCatalog catalog, ICurrentUser currentUser) =>
        outcome.Result switch
        {
            DecisionResult.Done => Results.NoContent(),
            DecisionResult.Refused => CrudProblems.Validation(outcome.Problems, catalog, currentUser.Locale),
            DecisionResult.Forbidden => Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser),
            DecisionResult.Changed => Problem(StatusCodes.Status409Conflict, ChangedKey, catalog, currentUser),
            _ => Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser),
        };

    private static IResult Problem(int status, string titleKey, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: status, title: catalog.Resolve(currentUser.Locale, titleKey));
}
