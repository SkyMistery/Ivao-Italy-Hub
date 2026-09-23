using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.People;

/// <summary>
/// A pilot as the tours' staff read them (design M2 §8.7): the errors confirmed, per category and per error, in the calendar year
/// asked and ever; every leg flown with its outcome and who decided it; the disputes, the clarifications, the bans; the tours with
/// how far they got. Nothing of it reaches the pilot, who has their own block (<c>flightops.myTours</c>) without the counts.
/// </summary>
public sealed record PilotPageDto(
    MemberDto Pilot,
    int Year,
    IReadOnlyList<PilotCategoryDto> Categories,
    IReadOnlyList<PilotErrorDto> Errors,
    IReadOnlyList<PilotFlightDto> Flights,
    PilotDisputesDto Disputes,
    IReadOnlyList<PilotThreadDto> Threads,
    IReadOnlyList<BanDto> Bans,
    IReadOnlyList<PilotTourDto> Tours,
    bool CanBan);

/// <summary>How many errors of a category were confirmed on the pilot's accepted and rejected reports, in the year and ever.</summary>
public sealed record PilotCategoryDto(ErrorCategory Category, int InYear, int Ever);

/// <summary>One error, with the name the reports froze (§5.4): an error gone from the catalogue still reads.</summary>
public sealed record PilotErrorDto(long ErrorId, Localized<string> Name, ErrorCategory Category, int InYear, int Ever);

/// <summary>A leg flown — a report not withdrawn — with its outcome, who decided it, and its dispute if any.</summary>
public sealed record PilotFlightDto(
    long PirepId,
    long TourId,
    Localized<string> TourTitle,
    int? LegNumber,
    string DepartureIcao,
    string ArrivalIcao,
    DateTime TakeoffAt,
    PirepStatus Status,
    MemberDto? DecidedBy,
    DateTime? DecidedAt,
    DisputeStatus? DisputeStatus);

public sealed record PilotDisputesDto(int Open, int Upheld, int Dismissed);

/// <summary>
/// A thread of the pilot with the tours' department — a dispute or a clarification — that the reader may open, in the contacts
/// of its department (T15b links it there).
/// </summary>
public sealed record PilotThreadDto(long Id, string Kind, string Subject, ContactStatus Status, DateTime CreatedAt, Department Department);

/// <summary>A tour the pilot is in, and how far: done out of target, in the unit of its kind (<see cref="PilotStanding"/>).</summary>
public sealed record PilotTourDto(
    long TourId,
    Localized<string> Title,
    long? ParentTourId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int Done,
    int Target,
    ProgressUnit Unit);

/// <summary>
/// The pilot's page of the staff, read with <c>Tours.ViewPilots</c> (coordinator, assistant, advisor, and every validator — answer
/// 17). The threads are the ones the reader may read in the contacts of their department, as everywhere else.
/// </summary>
public sealed class Pilots(
    FlightOpsDbContext database,
    HubDbContext hub,
    PilotProgress progress,
    PirepReview reviews,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<PilotPageDto?> ReadAsync(int vid, int year, CancellationToken cancellationToken)
    {
        var reports = await CrudSource.BackOffice<Pirep>(database).AsNoTracking()
            .Include(report => report.Errors)
            .Where(report => report.Vid == vid && report.Status != PirepStatus.Withdrawn)
            .OrderByDescending(report => report.TakeoffAt)
            .ToListAsync(cancellationToken);
        var enrolments = await database.Enrolments.AsNoTracking()
            .Where(row => row.Vid == vid)
            .OrderByDescending(row => row.StartedAt)
            .ToListAsync(cancellationToken);
        var names = await reviews.NamesAsync([vid, .. reports.Select(report => report.DecidedByVid)], cancellationToken);
        if (reports.Count == 0 && enrolments.Count == 0 && !names.ContainsKey(vid))
        {
            return null;
        }

        var tourIds = reports.Select(report => report.TourId).Union(enrolments.Select(row => row.TourId)).ToList();
        var tours = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .Where(tour => tourIds.Contains(tour.Id))
            .ToDictionaryAsync(tour => tour.Id, cancellationToken);

        var (categories, errors) = Errors(reports, year);
        var now = clock.UtcNow;
        var bans = await CrudSource.BackOffice<Ban>(database).AsNoTracking()
            .Where(ban => ban.Vid == vid)
            .OrderByDescending(ban => ban.StartsAt)
            .ToListAsync(cancellationToken);
        var standings = new List<PilotTourDto>();
        foreach (var enrolment in enrolments.Where(row => tours.ContainsKey(row.TourId)))
        {
            var tour = tours[enrolment.TourId];
            var standing = await progress.OfAsync(tour, vid, null, cancellationToken);
            standings.Add(new PilotTourDto(
                tour.Id,
                tour.Title,
                tour.ParentTourId,
                enrolment.StartedAt,
                enrolment.CompletedAt,
                standing.Done,
                standing.Target,
                standing.Unit));
        }

        return new PilotPageDto(
            PirepReview.Member(vid, names)!,
            year,
            categories,
            errors,
            [
                .. reports.Where(report => tours.ContainsKey(report.TourId)).Select(report => new PilotFlightDto(
                    report.Id,
                    report.TourId,
                    tours[report.TourId].Title,
                    PirepSubmission.LegSnapshot(report).Number,
                    report.DepartureIcao,
                    report.ArrivalIcao,
                    report.TakeoffAt,
                    report.Status,
                    PirepReview.Member(report.DecidedByVid, names),
                    report.DecidedAt,
                    report.DisputeStatus)),
            ],
            new PilotDisputesDto(
                reports.Count(report => report.DisputeStatus == DisputeStatus.Open),
                reports.Count(report => report.DisputeStatus == DisputeStatus.Upheld),
                reports.Count(report => report.DisputeStatus == DisputeStatus.Dismissed)),
            await ThreadsAsync(vid, cancellationToken),
            await BanEndpoints.RowsAsync(bans, database, reviews, now, cancellationToken),
            standings,
            currentUser.HasAny(TourPermissions.Ban));
    }

    /// <summary>
    /// The errors confirmed on the accepted and rejected reports, the ones that count (T13a): a report to modify counts nothing.
    /// The year is the calendar year (UTC) of the take-off, as the validation counts it.
    /// </summary>
    private static (IReadOnlyList<PilotCategoryDto> Categories, IReadOnlyList<PilotErrorDto> Errors) Errors(IReadOnlyList<Pirep> reports, int year)
    {
        var counted = reports.Where(report => report.Status is PirepStatus.Accepted or PirepStatus.Rejected).ToList();
        var names = counted
            .SelectMany(PirepSubmission.Snapshot)
            .SelectMany(rule => rule.Errors)
            .GroupBy(error => error.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var marked = counted
            .SelectMany(report => report.Errors.Where(error => error.Confirmed).Select(error => (error.ErrorId, error.Category, InYear: report.TakeoffAt.Year == year)))
            .ToList();

        var categories = marked
            .GroupBy(error => error.Category)
            .OrderBy(group => group.Key)
            .Select(group => new PilotCategoryDto(group.Key, group.Count(error => error.InYear), group.Count()))
            .ToList();

        var errors = marked
            .GroupBy(error => error.ErrorId)
            .Select(group => new PilotErrorDto(
                group.Key,
                names.TryGetValue(group.Key, out var frozen) ? frozen.Name : new Localized<string>(new Dictionary<string, string>()),
                group.First().Category,
                group.Count(error => error.InYear),
                group.Count()))
            .OrderByDescending(error => error.Ever)
            .ThenBy(error => error.ErrorId)
            .ToList();

        return (categories, errors);
    }

    /// <summary>The pilot's disputes and clarifications with the tours, among the contacts the reader reads.</summary>
    private async Task<IReadOnlyList<PilotThreadDto>> ThreadsAsync(int vid, CancellationToken cancellationToken)
    {
        if (!currentUser.HasAny(CorePermissions.ContactsView))
        {
            return [];
        }

        var cited = hub.ContactReferences.Where(reference => reference.SourceModule == FlightOpsModule.ModuleKey).Select(reference => reference.MessageId);
        var threads = await CrudSource.BackOffice<ContactMessage>(hub).AsNoTracking()
            .Where(message => message.CreatedBy == vid
                && ((message.Kind == ContactKinds.Dispute && message.SourceModule == FlightOpsModule.ModuleKey)
                    || (message.Kind == ContactKinds.Clarification && cited.Contains(message.Id))))
            .OrderByDescending(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

        return
        [
            .. threads
                .Where(message => currentUser.Has(CorePermissions.ContactsView, message.OwnerDepartment))
                .Select(message => new PilotThreadDto(message.Id, message.Kind, message.Subject, message.Status, message.CreatedAt, message.OwnerDepartment)),
        ];
    }
}

public static class PilotEndpoints
{
    public const string Pattern = "/api/flightops/pilots";

    public static IEndpointRouteBuilder MapPilotEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{Pattern}/{{vid:int}}", async (int vid, int? year, Pilots pilots, IClock clock, HttpContext http) =>
                await pilots.ReadAsync(vid, year ?? clock.UtcNow.Year, http.RequestAborted) is { } page ? Results.Ok(page) : Results.NotFound())
            .WithTags("FlightOpsPilots")
            .WithName("FlightOpsPilot")
            .RequireAuthorization(TourPermissions.ViewPilots)
            .Produces<PilotPageDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
