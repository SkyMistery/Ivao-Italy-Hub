using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Aircraft;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>
/// Whether a tour has reports, which decides whether it may be deleted or only hidden (design M2 §1.2.2), and which of
/// its legs do, which decides whether a leg is deleted or retired (§1.4.1). The reports arrive with T11: until then the
/// answer is no, and T11 replaces this implementation with the query on its table.
/// </summary>
public interface ITourReports
{
    Task<bool> AnyAsync(long tourId, CancellationToken cancellationToken = default);

    /// <summary>The legs of the tour at least one report points at.</summary>
    Task<IReadOnlySet<long>> LegsWithReportsAsync(long tourId, CancellationToken cancellationToken = default);
}

/// <summary>No tour has reports before the reports exist (T11).</summary>
internal sealed class NoTourReportsYet : ITourReports
{
    public Task<bool> AnyAsync(long tourId, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<IReadOnlySet<long>> LegsWithReportsAsync(long tourId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<long>>(new HashSet<long>());
}

/// <summary>
/// Whether the aircraft a tour or a leg admits exist (design M2 §1.5): every type one the core knows, every group one
/// of the module's. The tour's form and the leg editor refuse the same way.
/// </summary>
public sealed class AllowedAircraftCheck(FlightOpsDbContext database, IAircraftTypeDirectory aircraftTypes)
{
    /// <summary>The i18n key of what is wrong, or null.</summary>
    public async Task<string?> ProblemAsync(AllowedAircraft allowed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(allowed);

        if (allowed.Types.Count > 0 && (await aircraftTypes.UnknownAsync([.. allowed.Types], cancellationToken)).Count > 0)
        {
            return "errors.aircraft.unknownType";
        }

        if (allowed.GroupIds.Count == 0)
        {
            return null;
        }

        var ids = allowed.GroupIds.Distinct().ToArray();
        var known = await database.AircraftGroups.AsNoTracking().CountAsync(group => ids.Contains(group.Id), cancellationToken);

        return known == ids.Length ? null : "flightops:errors.aircraftGroupUnknown";
    }

    /// <summary>Upper case, each once, in order: what is stored, whatever was typed.</summary>
    public static AllowedAircraft Normalize(AllowedAircraft? allowed) => allowed is null
        ? AllowedAircraft.All
        : new AllowedAircraft(
            [.. (allowed.Types ?? []).Select(type => type.Trim().ToUpperInvariant()).Where(type => type.Length > 0).Distinct().Order()],
            [.. (allowed.GroupIds ?? []).Distinct().Order()]);
}

/// <summary>Refusals, one or more i18n keys per field, in the shape the form reads.</summary>
internal sealed class TourProblems
{
    public Dictionary<string, string[]> Errors { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string[]> Localized { get; } = new(StringComparer.Ordinal);

    public bool IsEmpty => Errors.Count == 0;

    public void Add(string field, string key) =>
        Errors[field] = Errors.TryGetValue(field, out var keys) ? [.. keys, key] : [key];

    public void Missing(string field, IReadOnlyList<string> locales)
    {
        Add(field, Core.Localization.LocalizedRules.MissingMessageKey);
        Localized[field] = [.. locales];
    }
}

/// <summary>
/// What a write of a tour may refuse only by looking at other rows (design M2 §1.2): run by the CRUD engine before
/// every save, and by the two copies of a template before theirs, so the three roads into <c>fo_tours</c> answer the
/// same way.
/// </summary>
public sealed class TourSaving(
    FlightOpsDbContext database,
    HubDbContext hub,
    IAircraftTypeDirectory aircraftTypes,
    ModuleSettingsStore settings,
    TourReadiness readiness,
    AllowedAircraftCheck allowedAircraft,
    IClock clock)
{
    public async Task<IReadOnlyDictionary<string, string[]>?> PrepareAsync(
        Tour tour,
        bool isNew,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tour);

        var problems = new TourProblems();
        var now = clock.UtcNow;

        // A new tour takes the division's window unless it says otherwise.
        if (tour.ReportWindowDays == 0)
        {
            tour.ReportWindowDays = (await settings.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken))
                .DefaultReportWindowDays;
        }

        if (!isNew)
        {
            var entry = database.Entry(tour);
            var before = (Tour)entry.OriginalValues.ToObject();

            if (before.IsTemplate != tour.IsTemplate)
            {
                problems.Add("isTemplate", "flightops:errors.templateFixed");
            }

            // The kind is locked the moment anybody outside the staff can see the tour (Carmine, 15 September 2026).
            if (before.Kind != tour.Kind && TourState.IsPublic(before, now))
            {
                problems.Add("kind", "flightops:errors.kindLocked");
            }

            // A ready tour's close moves, but never under a pilot's feet: at least two windows from today (§1.2.2).
            if (before.Status == PublishStatus.Published
                && before.CloseAt != tour.CloseAt
                && tour.CloseAt is { } close
                && close < now.AddDays(2 * tour.ReportWindowDays))
            {
                problems.Add("closeAt", "flightops:errors.closeTooSoon");
            }
        }

        if (tour.Slug is { } slug
            && await CrudSource.BackOffice<Tour>(database)
                .AnyAsync(row => !row.IsTemplate && row.Slug == slug && row.Id != tour.Id, cancellationToken))
        {
            problems.Add("slug", "flightops:errors.slugTaken");
        }

        if (tour.ReferenceAircraftIcao is { } type
            && (await aircraftTypes.UnknownAsync([type], cancellationToken)).Count > 0)
        {
            problems.Add("referenceAircraftIcao", "errors.aircraft.unknownType");
        }

        if (await allowedAircraft.ProblemAsync(tour.AllowedAircraft, cancellationToken) is { } aircraftProblem)
        {
            problems.Add("allowedAircraft", aircraftProblem);
        }

        if (tour.AwardId is { } awardId
            && !await hub.Awards.AsNoTracking().AnyAsync(award => award.Id == awardId, cancellationToken))
        {
            problems.Add("awardId", "flightops:errors.awardUnknown");
        }

        // A ready tour stays ready only as long as it still could be marked ready: an edit does not take it back.
        if (problems.IsEmpty && tour.Status == PublishStatus.Published)
        {
            var ready = await readiness.ProblemsAsync(tour, cancellationToken);
            if (!ready.IsEmpty)
            {
                return ready.Errors;
            }
        }

        if (problems.IsEmpty && !isNew)
        {
            await KeepTheLegsWithTheTourAsync(tour, cancellationToken);
        }

        return problems.IsEmpty ? null : problems.Errors;
    }

    /// <summary>
    /// The legs are in the care of the tour's departments (Carmine, 18 September 2026): when those change, the legs
    /// follow in the same save, so the one handler never reads a leg differently from its tour.
    /// </summary>
    private async Task KeepTheLegsWithTheTourAsync(Tour tour, CancellationToken cancellationToken)
    {
        var legs = await database.Legs
            .Where(leg => leg.TourId == tour.Id
                && (leg.OwnerDepartment != tour.OwnerDepartment || leg.OwnerDepartmentMask != tour.OwnerDepartmentMask))
            .ToListAsync(cancellationToken);

        foreach (var leg in legs)
        {
            leg.OwnerDepartment = tour.OwnerDepartment;
            leg.OwnerDepartmentMask = tour.OwnerDepartmentMask;
        }
    }
}

/// <summary>
/// What a tour needs before it can be marked ready (design M2 §1.2.1), in one place: the action, the list of problems
/// the editor shows before anybody presses it, and every later save of a ready tour or of one of its legs. The checks
/// on the legs are <see cref="TourShape"/> (T7a); the hubs and the subtours are added by T7b.
/// </summary>
public sealed class TourReadiness(
    FlightOpsDbContext database,
    ModuleSettingsStore settings,
    BlockDocumentWalker walker,
    IAirportDirectory airports,
    IOptions<DivisionOptions> division)
{
    internal Task<TourProblems> ProblemsAsync(Tour tour, CancellationToken cancellationToken) =>
        ProblemsAsync(tour, legs: null, cancellationToken);

    /// <summary>
    /// The problems of the tour with these legs — the legs as a write is about to leave them — or, when none are
    /// handed in, with the legs as they are stored.
    /// </summary>
    internal async Task<TourProblems> ProblemsAsync(Tour tour, IReadOnlyList<Leg>? legs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tour);

        var problems = new TourProblems();
        var locales = division.Value.Locales;

        if (tour.IsTemplate)
        {
            problems.Add("status", "flightops:errors.templateNeverReady");
            return problems;
        }

        if (!tour.Title.HasAll(locales))
        {
            problems.Missing("title", tour.Title.MissingLocales(locales));
        }

        if (!tour.Summary.HasAll(locales))
        {
            problems.Missing("summary", tour.Summary.MissingLocales(locales));
        }

        foreach (var missing in walker.MissingLocales(JsonNode.Parse(tour.BriefingJson)))
        {
            problems.Missing($"briefing.{missing.Path}", missing.Locales);
        }

        if (string.IsNullOrWhiteSpace(tour.Slug))
        {
            problems.Add("slug", "errors.required");
        }

        if (tour.ReleaseAt is null)
        {
            problems.Add("releaseAt", "errors.required");
        }

        if (tour.CloseAt is null)
        {
            problems.Add("closeAt", "errors.required");
        }
        else if (tour.ReleaseAt is { } release && tour.CloseAt <= release)
        {
            problems.Add("closeAt", "flightops:errors.closeBeforeRelease");
        }

        // With the division's limit switched off, every tour carries one of its own (§3.7).
        if (tour.DailyLegLimit is null
            && (await settings.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken)).DailyLegLimit is null)
        {
            problems.Add("dailyLegLimit", "flightops:errors.dailyLimitRequired");
        }

        // The reference aircraft is optional, but a tour that names one has to be able to compute with it (§1.5).
        if (tour.ReferenceAircraftIcao is { } type
            && !await database.Set<AircraftProfile>().AsNoTracking().AnyAsync(profile => profile.IcaoType == type, cancellationToken))
        {
            problems.Add("referenceAircraftIcao", "flightops:errors.referenceWithoutProfile");
        }

        legs ??= await database.Legs.AsNoTracking().Where(leg => leg.TourId == tour.Id).ToListAsync(cancellationToken);
        var known = await airports.FindAsync(
            [.. legs.Where(leg => !leg.IsRetired).SelectMany(leg => new[] { leg.DepartureIcao, leg.ArrivalIcao })],
            cancellationToken);

        foreach (var problem in TourShape.Problems(tour, legs, known.Keys.ToHashSet(StringComparer.Ordinal)))
        {
            problems.Add(problem.Field, problem.Key);
        }

        return problems;
    }
}

/// <summary>
/// What a template carries into a tour and a tour into a template (design M2 §1.10): the settings, the briefing and the
/// pictures — never the address, the dates, the award, the state, the legs or the hubs. The rules and the callsign
/// constraints are added to the copy by the phases that create them (T7b, T9).
/// </summary>
public static class TourCopy
{
    public static Tour Settings(Tour source, BlockDocumentWalker walker)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(walker);

        var briefing = JsonNode.Parse(source.BriefingJson) ?? JsonNode.Parse(Tour.EmptyBriefing)!;
        TemplateCopy.Reidentify(walker, briefing);

        return new Tour
        {
            Kind = source.Kind,
            Progression = source.Progression,
            ReportWindowDays = source.ReportWindowDays,
            DailyLegLimit = source.DailyLegLimit,
            RequiresProcedures = source.RequiresProcedures,
            AllowedAircraftJson = source.AllowedAircraftJson,
            ReferenceAircraftIcao = source.ReferenceAircraftIcao,
            HubRotationOrder = source.HubRotationOrder,
            MinPilotRating = source.MinPilotRating,
            RequiredNm = source.RequiredNm,
            RequiredSubtours = source.RequiredSubtours,
            OpenGoal = source.OpenGoal,
            OpenGoalJson = source.OpenGoalJson,
            BriefingJson = briefing.ToJsonString(),
            CoverMediaId = source.CoverMediaId,
            BannerMediaId = source.BannerMediaId,
            OwnerDepartment = source.OwnerDepartment,
            OwnerDepartmentMask = source.OwnerDepartmentMask,
        };
    }
}
