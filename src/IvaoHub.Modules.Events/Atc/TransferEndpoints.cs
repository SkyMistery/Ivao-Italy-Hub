using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// A controller's shifts and handing one over (design M4 §4.4, §4.4-bis, §7.1, §7.2, E12; note
/// 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, readings 6, 7 and 10).
/// <list type="bullet">
/// <item><b>The member's</b>, under <c>/api/events/mine/shifts</c>: reading their own shifts of the published rosters, asking to hand one over,
/// taking the request back. Verbs of a member's flow, written by hand as an availability's are; any signed in member, their own alone.</item>
/// <item><b>The staff's</b>: the requests as a resource of the engine in read only (<c>/api/events/atc/transfers</c>, <c>EventAtc.View</c>, by
/// event and state), and the two decisions the design names — approve, refuse —, with <c>EventAtc.Edit</c> asked of the one handler on the
/// request (<see cref="StaffTransfers"/>).</item>
/// </list>
/// </summary>
public static class TransferEndpoints
{
    /// <summary>The member's own shifts, and the verbs on them.</summary>
    public const string MinePattern = "/api/events/mine/shifts";

    /// <summary>The requests as the staff of the ATC reach them.</summary>
    public const string StaffPattern = "/api/events/atc/transfers";

    /// <summary>The key a 409 says: the request changed from elsewhere a moment before, read it again.</summary>
    public const string ChangedKey = "events:errors.transferChanged";

    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mine = app.MapGroup(MinePattern).WithTags("EventAtc").RequireAuthorization(HubPolicies.SignedIn);

        mine.MapGet("/", (MemberShifts shifts, HttpContext http) => shifts.MineAsync(http.RequestAborted))
            .WithName("EventsMyShifts")
            .Produces<IReadOnlyList<MyShiftDto>>();

        mine.MapPost("/{id:long}/transfer", AskAsync)
            .WithName("EventsAskTransfer")
            .Produces<MyTransferDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        mine.MapDelete("/{id:long}/transfer", WithdrawAsync)
            .WithName("EventsWithdrawTransfer")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // The staff's list: every request, with the shift, the people and the warnings; filter[eventId] and filter[status]. Read only: a
        // request changes through the member's verbs and the two decisions.
        app.MapCrud<EventAtcTransfer, EventAtcTransferDto, EventAtcTransferDto, EventAtcTransferDto>(StaffPattern, options =>
        {
            options.PermissionArea = EventsPermissions.AtcArea;
            options.Name = "EventAtcTransfers";
            options.ReadPolicy = EventsPermissions.AtcView;
            options.WritePolicy = EventsPermissions.AtcEdit;
            options.ReadOnly = true;
            options.ContextType = typeof(EventsDbContext);

            // As they came: the first asked is the first to decide.
            options.DefaultOrder = transfer => transfer.Id;
            options.Sortable.Add(nameof(EventAtcTransfer.CreatedAt));
            options.Filterable.Add(nameof(EventAtcTransfer.EventId));
            options.Filterable.Add(nameof(EventAtcTransfer.Status));

            options.ToList = transfer => StaffTransfers.Row(transfer, null, new Dictionary<long, EventAtcPosition>(), new Dictionary<int, string>(), []);
            options.ToListPage = (transfers, services, cancellationToken) =>
                services.GetRequiredService<StaffTransfers>().RowsAsync(transfers, cancellationToken);
            options.ToDetail = transfer => StaffTransfers.Row(transfer, null, new Dictionary<long, EventAtcPosition>(), new Dictionary<int, string>(), []);
        });

        app.MapPost($"{StaffPattern}/{{id:long}}/approve", ApproveAsync)
            .WithName("EventsApproveTransfer")
            .WithTags("EventAtc")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.AtcEdit);

        app.MapPost($"{StaffPattern}/{{id:long}}/refuse", RefuseAsync)
            .WithName("EventsRefuseTransfer")
            .WithTags("EventAtc")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.AtcEdit);

        return app;
    }

    private static async Task<IResult> AskAsync(
        long id,
        TransferRequest request,
        MemberShifts shifts,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var (result, transfer, problems) = await shifts.AskAsync(id, request, http.RequestAborted);

        // No address of its own: a request is read under its shift, in the member's list.
        return result switch
        {
            TransferResult.Done => Results.Created((string?)null, transfer),
            TransferResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> WithdrawAsync(long id, MemberShifts shifts, LocaleCatalog catalog, ICurrentUser currentUser, HttpContext http) =>
        await shifts.WithdrawAsync(id, http.RequestAborted) switch
        {
            TransferResult.Done => Results.NoContent(),
            TransferResult.Changed => Problem(StatusCodes.Status409Conflict, ChangedKey, catalog, currentUser),
            _ => Results.NotFound(),
        };

    private static async Task<IResult> ApproveAsync(
        long id,
        TransferApproval approval,
        StaffTransfers transfers,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var (result, problems) = await transfers.ApproveAsync(id, approval, http.User, http.RequestAborted);
        return Answer(result, problems, catalog, currentUser);
    }

    private static async Task<IResult> RefuseAsync(
        long id,
        TransferRefusal refusal,
        StaffTransfers transfers,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var (result, problems) = await transfers.RefuseAsync(id, refusal, http.User, http.RequestAborted);
        return Answer(result, problems, catalog, currentUser);
    }

    private static IResult Answer(TransferResult result, Refusals problems, LocaleCatalog catalog, ICurrentUser currentUser) => result switch
    {
        TransferResult.Done => Results.NoContent(),
        TransferResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
        TransferResult.Forbidden => Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser),
        TransferResult.Changed => Problem(StatusCodes.Status409Conflict, ChangedKey, catalog, currentUser),
        _ => Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser),
    };

    private static IResult Problem(int status, string titleKey, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: status, title: catalog.Resolve(currentUser.Locale, titleKey));
}
