using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.People;

/// <summary>A validator enabled on a tour of the first level, or on every tour when <c>TourId</c> is null.</summary>
public sealed record ValidatorWriteDto(int Vid, long? TourId);

/// <summary>The statistics of the validators in a year (design M2 §8.7), and who is enabled on what.</summary>
/// <param name="Year">The calendar year (UTC) of the decisions counted.</param>
/// <param name="Validators">Everybody enabled by a grant of their own, and everybody who decided a report that year.</param>
/// <param name="Tours">The tours with a decision that year, each with what every validator decided on it.</param>
public sealed record ValidatorsDto(int Year, IReadOnlyList<ValidatorDto> Validators, IReadOnlyList<ValidatorTourDto> Tours);

/// <summary>
/// One validator. <c>AllTours</c> and <c>TourIds</c> are the grants of their own (<c>Tours.Validate</c>, «add a validator»); who
/// validates through their position (FOC, FOAC, FOA) has none and appears for what they decided. <c>Suspended</c>: they left the
/// staff, and their grants wait for them (§7.2).
/// </summary>
public sealed record ValidatorDto(
    MemberDto Member,
    bool AllTours,
    IReadOnlyList<long> TourIds,
    bool Suspended,
    int Accepted,
    int Rejected,
    int ToModify);

public sealed record ValidatorTourDto(long TourId, Localized<string> Title, IReadOnlyList<ValidatorCountDto> Counts);

public sealed record ValidatorCountDto(int Vid, int Accepted, int Rejected, int ToModify);

/// <summary>
/// The validators of the tours (design M2 §7.2, §8.7): the statistics per year and per tour, and «add a validator» and «remove»
/// — the only way a validator is enabled (answer 18). Enabling writes a grant of <c>Tours.Validate</c> scoped to the tour through
/// the core (<see cref="ModuleGrants"/>), on the tour of the first level — a report on a subtour is its container's to validate
/// (<see cref="Pirep.ScopeTourId"/>) — or on every tour, and a grant of <c>Tours.ViewPilots</c> with it, which the design gives
/// every validator (answer 17): the pilot's page and these statistics. Removing the last tour takes that one away as well, only
/// when it was written here (Carmine, 23 September 2026, T15).
/// <para>What is counted is the decision a report has now: a decision reopened and taken again is the second one's, and a report
/// sent back and sent again is nobody's until it is decided again.</para>
/// </summary>
public sealed class Validators(
    FlightOpsDbContext database,
    ModuleGrants grants,
    ModuleRegistry modules,
    PirepReview reviews)
{
    /// <summary>The reason on the grants written here, which is also how «remove» knows the grant of the pilot's page is its own.</summary>
    public const string GrantReason = "flightops: tour validator";

    private const string ScopePrefix = FlightOpsModule.ModuleKey + ":tour:";

    public async Task<ValidatorsDto> ReadAsync(int year, CancellationToken cancellationToken)
    {
        var department = Department();
        var enabled = (await grants.HeldAsync(TourPermissions.Validate, department, cancellationToken))
            .GroupBy(grant => grant.Vid!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);
        var decided = await CrudSource.BackOffice<Pirep>(database).AsNoTracking()
            .Where(report => report.DecidedByVid != null
                && report.DecidedAt >= from && report.DecidedAt < to
                && (report.Status == PirepStatus.Accepted || report.Status == PirepStatus.Rejected || report.Status == PirepStatus.ToModify))
            .GroupBy(report => new { Vid = report.DecidedByVid!.Value, report.ScopeTourId, report.Status })
            .Select(group => new { group.Key.Vid, group.Key.ScopeTourId, group.Key.Status, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var names = await reviews.NamesAsync([.. enabled.Keys.Cast<int?>(), .. decided.Select(row => (int?)row.Vid)], cancellationToken);
        var validators = enabled.Keys.Union(decided.Select(row => row.Vid))
            .Order()
            .Select(vid =>
            {
                var own = enabled.GetValueOrDefault(vid) ?? [];
                var theirs = decided.Where(row => row.Vid == vid).ToList();
                return new ValidatorDto(
                    PirepReview.Member(vid, names)!,
                    own.Any(grant => grant.ResourceScope is null),
                    [.. own.Select(grant => TourOf(grant.ResourceScope)).OfType<long>().Order()],
                    own.Count > 0 && own.All(grant => grant.SuspendedAt is not null),
                    Sum(theirs.Where(row => row.Status == PirepStatus.Accepted).Select(row => row.Count)),
                    Sum(theirs.Where(row => row.Status == PirepStatus.Rejected).Select(row => row.Count)),
                    Sum(theirs.Where(row => row.Status == PirepStatus.ToModify).Select(row => row.Count)));
            })
            .ToList();

        var tourIds = decided.Select(row => row.ScopeTourId).Distinct().ToList();
        var titles = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .Where(tour => tourIds.Contains(tour.Id))
            .Select(tour => new { tour.Id, tour.Title })
            .ToDictionaryAsync(tour => tour.Id, tour => tour.Title, cancellationToken);
        var tours = decided
            .GroupBy(row => row.ScopeTourId)
            .Where(group => titles.ContainsKey(group.Key))
            .OrderBy(group => group.Key)
            .Select(group => new ValidatorTourDto(
                group.Key,
                titles[group.Key],
                [
                    .. group.GroupBy(row => row.Vid).OrderBy(byVid => byVid.Key).Select(byVid => new ValidatorCountDto(
                        byVid.Key,
                        Sum(byVid.Where(row => row.Status == PirepStatus.Accepted).Select(row => row.Count)),
                        Sum(byVid.Where(row => row.Status == PirepStatus.Rejected).Select(row => row.Count)),
                        Sum(byVid.Where(row => row.Status == PirepStatus.ToModify).Select(row => row.Count)))),
                ]))
            .ToList();

        return new ValidatorsDto(year, validators, tours);
    }

    /// <summary>Enables the member on the tour, or on every tour. The refusals are field by field: <c>vid</c>, <c>tourId</c>.</summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> AddAsync(ValidatorWriteDto payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.TourId is { } tourId
            && !await CrudSource.BackOffice<Tour>(database).AnyAsync(
                tour => tour.Id == tourId && !tour.IsTemplate && tour.ParentTourId == null,
                cancellationToken))
        {
            return Refusal("tourId", "flightops:errors.validatorTour");
        }

        var department = Department();
        if (await grants.GiveAsync(payload.Vid, TourPermissions.Validate, department, ScopeOf(payload.TourId), GrantReason, cancellationToken) is { } refused)
        {
            return Refusal("vid", refused);
        }

        var pages = await grants.HeldAsync(TourPermissions.ViewPilots, department, cancellationToken);
        if (!pages.Any(grant => grant.Vid == payload.Vid && grant.ResourceScope is null))
        {
            await grants.GiveAsync(payload.Vid, TourPermissions.ViewPilots, department, null, GrantReason, cancellationToken);
        }

        return null;
    }

    /// <summary>Takes the member off the tour, or off every tour; false when they were not on it.</summary>
    public async Task<bool> RemoveAsync(int vid, long? tourId, CancellationToken cancellationToken)
    {
        var department = Department();
        if (!await grants.TakeAsync(vid, TourPermissions.Validate, department, ScopeOf(tourId), cancellationToken))
        {
            return false;
        }

        var left = await grants.HeldAsync(TourPermissions.Validate, department, cancellationToken);
        var pages = await grants.HeldAsync(TourPermissions.ViewPilots, department, cancellationToken);
        if (!left.Any(grant => grant.Vid == vid)
            && pages.Any(grant => grant.Vid == vid && grant.ResourceScope is null && grant.Reason == GrantReason))
        {
            await grants.TakeAsync(vid, TourPermissions.ViewPilots, department, null, cancellationToken);
        }

        return true;
    }

    private static string? ScopeOf(long? tourId) => tourId is { } id ? Pirep.ScopeOf(id) : null;

    private static long? TourOf(string? scope) =>
        scope is not null && scope.StartsWith(ScopePrefix, StringComparison.Ordinal) && long.TryParse(scope[ScopePrefix.Length..], out var id)
            ? id
            : null;

    private static int Sum(IEnumerable<int> counts) => counts.Sum();

    private static Dictionary<string, string[]> Refusal(string field, string key) => new(StringComparer.Ordinal) { [field] = [key] };

    private Core.Division.Department Department() =>
        modules.BaseDepartmentOf(FlightOpsModule.ModuleKey)
        ?? throw new InvalidOperationException("The tours have no base department: division.json → modules.flightops.baseDepartment.");
}

/// <summary>The endpoints of the validators: read with <c>Tours.ViewPilots</c>, add and remove with <c>Tours.ManageValidators</c>.</summary>
public static class ValidatorEndpoints
{
    public const string Pattern = "/api/flightops/validators";

    public static IEndpointRouteBuilder MapValidatorEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Pattern).WithTags("FlightOpsValidators");

        group.MapGet("/", async (int? year, Validators validators, IClock clock, HttpContext http) =>
                Results.Ok(await validators.ReadAsync(year ?? clock.UtcNow.Year, http.RequestAborted)))
            .RequireAuthorization(TourPermissions.ViewPilots)
            .WithName("FlightOpsValidators")
            .Produces<ValidatorsDto>();

        group.MapPost("/", async (ValidatorWriteDto body, Validators validators, LocaleCatalog catalog, ICurrentUser user, HttpContext http) =>
                await validators.AddAsync(body, http.RequestAborted) is { } problems
                    ? CrudProblems.Validation(problems, new Dictionary<string, string[]>(), catalog, user.Locale)
                    : Results.NoContent())
            .RequireAuthorization(TourPermissions.ManageValidators)
            .WithName("FlightOpsValidatorAdd")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        group.MapDelete("/{vid:int}", async (int vid, long? tourId, Validators validators, HttpContext http) =>
                await validators.RemoveAsync(vid, tourId, http.RequestAborted) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization(TourPermissions.ManageValidators)
            .WithName("FlightOpsValidatorRemove")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
