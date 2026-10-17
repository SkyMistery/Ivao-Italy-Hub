using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.People;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.Reports;

/// <summary>
/// A report of support as the staff's generated list reads it (§7.2, E14a; note 2026-10-17-il-pirep-di-supporto, reading 9): its member, its
/// kind and where it stands, what the check made of it, who decided and their note, its items with what the check found of each, and whether
/// the reader may decide it now — an event over and <c>EventReports.Edit</c> from the one handler on the row, never on their own. Data only:
/// the page writes the words. Names and items are read for a page at once; a row alone has neither.
/// </summary>
public sealed record EventReportRowDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    EventMemberDto Member,
    ReportKind Kind,
    ReportSource Source,
    ReportState State,
    ReportVerification Verification,
    DateTime SubmittedAt,
    DateTime CheckedAt,
    EventMemberDto? DecidedBy,
    DateTime? DecidedAt,
    string? DecisionNote,
    IReadOnlyList<ReportItemDto> Items,
    bool Decidable);

/// <summary>
/// The reports of support (design M4 §5.2, §5.3, §7.1, §7.2, E14a; note 2026-10-17-il-pirep-di-supporto, readings 6, 7 and 9).
/// <list type="bullet">
/// <item><b>The staff's list</b>: a resource of the CRUD engine in read only, <c>/api/events/reports</c>, with <c>EventReports.View</c> on the
/// event's care, narrowed with <c>filter[eventId]</c>, <c>filter[state]</c>, <c>filter[kind]</c>; <c>?q=</c> a VID.</item>
/// <item><b>Three verbs the design names</b> (§7.2: «mandare e decidere un PIREP») and the member's read of their own: <c>GET
/// /api/events/mine/reports</c>, <c>PUT /api/events/mine/reports/{eventId}/{kind}</c> to send or correct (<see cref="MemberReports"/>),
/// and <c>POST /api/events/reports/{id}/decision</c> (<see cref="ReportDecisions"/>).</item>
/// </list>
/// </summary>
public static class ReportEndpoints
{
    public const string Pattern = "/api/events/reports";

    public const string MinePattern = "/api/events/mine/reports";

    /// <summary>The key a 409 says: the report changed from elsewhere a moment before, read it again.</summary>
    public const string ChangedKey = "events:errors.reportChanged";

    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var noNames = new Dictionary<int, string>();
        app.MapCrud<EventReport, EventReportRowDto, EventReportRowDto, EventReportRowDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.ReportsArea;
            options.Name = "EventReports";
            options.ReadPolicy = EventsPermissions.ReportsView;
            options.WritePolicy = EventsPermissions.ReportsEdit;
            options.ReadOnly = true;
            options.ContextType = typeof(EventsDbContext);

            // As they came: the first sent first.
            options.DefaultOrder = report => report.Id;
            options.Sortable.Add(nameof(EventReport.SubmittedAt));
            options.Filterable.Add(nameof(EventReport.EventId));
            options.Filterable.Add(nameof(EventReport.State));
            options.Filterable.Add(nameof(EventReport.Kind));
            options.SearchFields.Add(report => report.Vid.ToString());

            options.ToList = report => Row(report, noNames, [], decidable: false);
            options.ToListPage = (reports, services, cancellationToken) => RowsAsync(services, reports, cancellationToken);
            options.ToDetail = report => Row(report, noNames, [], decidable: false);
        });

        var mine = app.MapGroup(MinePattern).WithTags("EventReports").RequireAuthorization(HubPolicies.SignedIn);

        mine.MapGet("/", (MemberReports reports, HttpContext http) => reports.MineAsync(http.RequestAborted))
            .WithName("EventsMyReports")
            .Produces<MyReportsDto>();

        mine.MapPut("/{eventId:long}/{kind}", SendAsync)
            .WithName("EventsSendReport")
            .Produces<MyReportDto>()
            .Produces<MyReportDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost($"{Pattern}/{{id:long}}/decision", async (
                    long id,
                    ReportDecision decision,
                    ReportDecisions decisions,
                    LocaleCatalog catalog,
                    ICurrentUser currentUser,
                    HttpContext http) =>
                Answer(await decisions.DecideAsync(id, decision, http.User, http.RequestAborted), catalog, currentUser))
            .WithName("EventsDecideReport")
            .WithTags("EventReports")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.ReportsEdit);

        return app;
    }

    /// <summary>Sends a report, or sends it again: 201 the first time, 200 after; a refusal lands on the field of its item.</summary>
    private static async Task<IResult> SendAsync(
        long eventId,
        string kind,
        ReportRequest request,
        MemberReports reports,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        if (!Enum.TryParse<ReportKind>(kind, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            return Results.NotFound();
        }

        var (result, report, created, problems) = await reports.SendAsync(eventId, parsed, request, http.RequestAborted);

        return result switch
        {
            ReportResult.Done => created ? Results.Created((string?)null, report) : Results.Ok(report),
            ReportResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
            _ => Results.NotFound(),
        };
    }

    /// <summary>
    /// A page of reports with their members' and deciders' names and their items, one query each, never one per row; and whether the reader may
    /// decide each now, the one handler asked on the reports of events over.
    /// </summary>
    private static async Task<IReadOnlyList<EventReportRowDto>> RowsAsync(
        IServiceProvider services,
        IReadOnlyList<EventReport> reports,
        CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<EventsDbContext>();
        var hub = services.GetRequiredService<HubDbContext>();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var user = services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;
        var authorization = services.GetRequiredService<IAuthorizationService>();

        var eventIds = reports.Select(report => report.EventId).Distinct().ToList();
        var over = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id) && row.EndsAtUtc <= now)
            .Select(row => row.Id)
            .ToListAsync(cancellationToken);

        // A report an award was assigned from is decided for good (Carmine's limit on #287, E14b): its decision is not offered.
        var assigned = await services.GetRequiredService<ReportAwards>()
            .AssignedAmongAsync([.. reports.Select(report => report.Id)], cancellationToken);
        var decidable = new HashSet<long>();
        foreach (var report in reports)
        {
            if (user is not null
                && over.Contains(report.EventId)
                && !assigned.Contains(report.Id)
                && (await authorization.AuthorizeAsync(user, report, EventsPermissions.ReportsEdit)).Succeeded)
            {
                decidable.Add(report.Id);
            }
        }

        var reportIds = reports.Select(report => report.Id).ToList();
        var items = (await CrudSource.BackOffice<EventReportItem>(database).AsNoTracking()
                .Where(item => reportIds.Contains(item.ReportId))
                .OrderBy(item => item.Ordinal)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken))
            .ToLookup(item => item.ReportId, ReportItemDto.Of);
        var names = await MemberNames.ReadAsync(
            hub,
            reports.Select(report => report.Vid).Concat(reports.Select(report => report.DecidedBy).OfType<int>()),
            cancellationToken);

        return [.. reports.Select(report => Row(report, names, [.. items[report.Id]], decidable.Contains(report.Id)))];
    }

    private static EventReportRowDto Row(
        EventReport report,
        IReadOnlyDictionary<int, string> names,
        IReadOnlyList<ReportItemDto> items,
        bool decidable) => new(
        report.Id,
        report.EventId,
        report.OwnerDepartment,
        EventsPeople.Member(report.Vid, names),
        report.Kind,
        report.Source,
        report.State,
        report.Verification,
        report.SubmittedAt,
        report.CheckedAt,
        report.DecidedBy is { } by ? EventsPeople.Member(by, names) : null,
        report.DecidedAt,
        report.DecisionNote,
        items,
        decidable);

    private static IResult Answer((ReportResult Result, Refusals Problems) outcome, LocaleCatalog catalog, ICurrentUser currentUser) =>
        outcome.Result switch
        {
            ReportResult.Done => Results.NoContent(),
            ReportResult.Refused => CrudProblems.Validation(outcome.Problems, catalog, currentUser.Locale),
            ReportResult.Forbidden => Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser),
            ReportResult.Changed => Problem(StatusCodes.Status409Conflict, ChangedKey, catalog, currentUser),
            _ => Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser),
        };

    private static IResult Problem(int status, string titleKey, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: status, title: catalog.Resolve(currentUser.Locale, titleKey));
}
