using System.Globalization;
using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.FlightOps.Threads;

/// <summary>A pilot's report of a problem on a leg (§3.11): what they saw, in their words.</summary>
public sealed record LegIssueReportDto(string? Body);

/// <summary>An issue as the staff's list and form show it: the tour, the leg, who wrote it and what, where it is.</summary>
public sealed record LegIssueDto(
    long Id,
    long TourId,
    Localized<string> TourTitle,
    long LegId,
    int? LegNumber,
    string? Route,
    MemberDto Pilot,
    string Body,
    LegIssueStatus Status,
    string? StaffNote,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>What the staff writes on an issue: whether it is dealt with, and a note for the others.</summary>
public sealed record LegIssueWriteDto(LegIssueStatus Status, string? StaffNote, DateTime RowVersion);

public sealed class LegIssueWriteDtoValidator : AbstractValidator<LegIssueWriteDto>
{
    public LegIssueWriteDtoValidator()
    {
        RuleFor(issue => issue.Status).IsInEnum().WithMessage("errors.required");
        RuleFor(issue => issue.StaffNote).MaximumLength(LegIssue.MaxBodyLength).WithMessage("errors.text.tooLong");
    }
}

/// <summary>
/// The issues on the legs (design M2 §3.11; note 2026-09-23-contestazioni-chiarimenti-segnalazioni §3.3): a pilot writes one from
/// the tour's page — one hand written verb, like the contact form, because the writer is any member and the leg decides where it
/// goes — and it reaches the mailbox of the tour's department; the staff read and close them through the generic list and form,
/// <c>Tours.View</c> to read and <c>Tours.Edit</c> to close (§7.1).
/// </summary>
public static class LegIssueEndpoints
{
    public const string ReportPattern = "/api/flightops/tours/{tourId:long}/legs/{legId:long}/issues";

    public const string Pattern = "/api/flightops/leg-issues";

    public static IEndpointRouteBuilder MapLegIssueEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(ReportPattern, ReportAsync)
            .WithTags("FlightOpsLegIssues")
            .WithName("FlightOpsLegIssueReport")
            .RequireAuthorization(HubPolicies.SignedIn)
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        app.MapCrud<LegIssue, LegIssueDto, LegIssueDto, LegIssueWriteDto>(Pattern, options =>
        {
            options.PermissionArea = TourPermissions.Area;
            options.Name = "FlightOpsLegIssues";
            options.ReadPolicy = TourPermissions.View;
            options.WritePolicy = TourPermissions.Edit;
            options.ContextType = typeof(FlightOpsDbContext);

            // Written by pilots, only closed here; and kept, as a record of what was reported.
            options.MapCreate = false;
            options.AllowDelete = false;

            options.DefaultOrder = issue => issue.CreatedAt;
            options.Sortable.Add(nameof(LegIssue.CreatedAt));
            options.Sortable.Add(nameof(LegIssue.TourId));
            options.Filterable.Add(nameof(LegIssue.Status));
            options.Filterable.Add(nameof(LegIssue.TourId));
            options.SearchFields.Add(issue => issue.Body);

            options.ToList = issue => Row(issue, null, null, new Dictionary<int, string>());
            options.ToListPage = PageAsync;
            options.ToDetail = issue => Row(issue, null, null, new Dictionary<int, string>());
            options.Apply = (payload, issue) =>
            {
                issue.Status = payload.Status;
                issue.StaffNote = string.IsNullOrWhiteSpace(payload.StaffNote) ? null : payload.StaffNote.Trim();
            };
        });

        return app;
    }

    /// <summary>
    /// A pilot reports a problem on a leg still in a tour the public sees; the tour's mailbox hears about it. The row is written
    /// by the member themselves, which <see cref="ISubmittedByMembers"/> lets through.
    /// </summary>
    private static async Task<IResult> ReportAsync(
        long tourId,
        long legId,
        LegIssueReportDto payload,
        PirepSubmission submission,
        FlightOpsDbContext database,
        INotificationService notifications,
        LocaleCatalog catalog,
        IOptions<DivisionOptions> division,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var pilot = await submission.TourAsync(tourId, http.RequestAborted);
        var leg = pilot is null
            ? null
            : await database.Legs.AsNoTracking().FirstOrDefaultAsync(row => row.Id == legId && row.TourId == tourId && row.RetiredAt == null, http.RequestAborted);
        if (pilot is null || leg is null)
        {
            return Results.NotFound();
        }

        var body = string.IsNullOrWhiteSpace(payload?.Body) ? null : payload.Body.Trim();
        if (body is null || body.Length > LegIssue.MaxBodyLength)
        {
            return CrudProblems.Validation(
                new Dictionary<string, string[]> { ["body"] = [body is null ? "errors.required" : "errors.text.tooLong"] },
                new Dictionary<string, string[]>(),
                catalog,
                currentUser.Locale);
        }

        var issue = new LegIssue
        {
            TourId = tourId,
            LegId = legId,
            Body = body,
            Status = LegIssueStatus.Open,
            OwnerDepartment = pilot.Tour.OwnerDepartment,
            OwnerDepartmentMask = pilot.Tour.OwnerDepartmentMask,
        };

        database.LegIssues.Add(issue);
        await database.SaveChangesAsync(http.RequestAborted);

        // The mailbox of the tour's department, and only that (design M2 §9): no staff member one by one.
        var options = division.Value;
        if (options.DepartmentMailboxes.TryGetValue(issue.OwnerDepartment.ToString(), out var mailbox) && !string.IsNullOrWhiteSpace(mailbox))
        {
            var locale = options.DefaultLocale;
            await notifications.QueueAsync(
                new NotificationIntent(
                    FlightOpsNotifications.LegIssueReported,
                    [NotificationRecipient.Mailbox(mailbox)],
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["tour"] = pilot.Tour.Title.Resolve(locale, options.DefaultLocale) ?? string.Empty,
                        ["leg"] = $"{leg.Number.ToString(CultureInfo.InvariantCulture)} {leg.DepartureIcao} → {leg.ArrivalIcao}",
                        ["body"] = body,
                        ["vid"] = currentUser.Vid.ToString(CultureInfo.InvariantCulture),
                        ["url"] = $"https://{options.Domain}/staff/tours/issues/{issue.Id}",
                    }),
                http.RequestAborted);
        }

        return Results.Created($"{Pattern}/{issue.Id}", null);
    }

    /// <summary>The rows of one page: the tours' titles, the legs' numbers and routes, the pilots' names.</summary>
    private static async Task<IReadOnlyList<LegIssueDto>> PageAsync(
        IReadOnlyList<LegIssue> issues,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FlightOpsDbContext>();
        var reviews = services.GetRequiredService<PirepReview>();

        var tourIds = issues.Select(issue => issue.TourId).Distinct().ToList();
        var titles = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .Where(tour => tourIds.Contains(tour.Id))
            .ToDictionaryAsync(tour => tour.Id, tour => tour.Title, cancellationToken);
        var legIds = issues.Select(issue => issue.LegId).Distinct().ToList();
        var legs = await CrudSource.BackOffice<Legs.Leg>(database).AsNoTracking()
            .Where(leg => legIds.Contains(leg.Id))
            .ToDictionaryAsync(leg => leg.Id, cancellationToken);
        var names = await reviews.NamesAsync(issues.Select(issue => (int?)issue.CreatedBy), cancellationToken);

        return [.. issues.Select(issue => Row(issue, titles.GetValueOrDefault(issue.TourId), legs.GetValueOrDefault(issue.LegId), names))];
    }

    private static LegIssueDto Row(LegIssue issue, Localized<string>? title, Legs.Leg? leg, IReadOnlyDictionary<int, string> names) =>
        new(
            issue.Id,
            issue.TourId,
            title ?? Localized<string>.Empty,
            issue.LegId,
            leg?.Number,
            leg is null ? null : $"{leg.DepartureIcao} → {leg.ArrivalIcao}",
            PirepReview.Member(issue.CreatedBy, names)!,
            issue.Body,
            issue.Status,
            issue.StaffNote,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.RowVersion);
}
