using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.FlightOps.Shape;

/// <summary>
/// What every row of a tour's shape shares on its way through the CRUD engine: the tour found and its care taken
/// before the permission is asked (<c>CrudOptions.BeforeAuthorize</c>, note 2026-09-21-la-forma-dei-tour), and, on a
/// ready tour, the checks of "ready" with the rows as the write leaves them — as the legs do (T7a).
/// </summary>
public sealed class TourChildren(FlightOpsDbContext database, TourReadiness readiness)
{
    /// <summary>The tour of the row, from the back office's rows.</summary>
    public Task<Tour?> TourAsync(long tourId, CancellationToken cancellationToken) =>
        CrudSource.BackOffice<Tour>(database).AsNoTracking().FirstOrDefaultAsync(tour => tour.Id == tourId, cancellationToken);

    /// <summary>The tour's department and care on the row; a refusal when the tour does not exist.</summary>
    public async Task<(Tour? Tour, IReadOnlyDictionary<string, string[]>? Refusal)> AdoptAsync(
        ITourChild row,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var tour = await TourAsync(row.TourId, cancellationToken);
        if (tour is null)
        {
            return (null, Refusal("tourId", "flightops:errors.tourUnknown"));
        }

        row.OwnerDepartment = tour.OwnerDepartment;
        row.OwnerDepartmentMask = tour.OwnerDepartmentMask;

        return (tour, null);
    }

    /// <summary>
    /// On a ready tour, the problems of "ready" once the change is made; nothing on a draft. A ready tour stays ready
    /// only as long as it could be marked ready (T6a).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> StillReadyAsync(
        Tour tour,
        Func<TourParts, TourParts> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tour);

        if (tour.Status != PublishStatus.Published)
        {
            return null;
        }

        var problems = await readiness.ProblemsAsync(tour, change, cancellationToken);
        return problems.IsEmpty ? null : problems.Errors;
    }

    /// <summary>The same, for a delete, which the engine answers with the first refusal.</summary>
    public async Task ThrowUnlessStillReadyAsync(Tour tour, Func<TourParts, TourParts> change, CancellationToken cancellationToken)
    {
        if (await StillReadyAsync(tour, change, cancellationToken) is { } problems)
        {
            var (field, keys) = problems.First();
            throw new DomainRefusalException(field, keys[0]);
        }
    }

    public static IReadOnlyDictionary<string, string[]> Refusal(string field, string key) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [key] };

    /// <summary>The list without the row, and with it as it will be stored: what a change of one row leaves.</summary>
    public static IReadOnlyList<T> Replacing<T>(IReadOnlyList<T> rows, T row, Func<T, long> id)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(id);

        return [.. rows.Where(other => id(row) == 0 || id(other) != id(row)), row];
    }
}

/// <summary>
/// Hubs, rotations and callsign constraints of a tour (design M2 §1.3, §1.6, §8.3), and the filters and sequence rules of an
/// <c>Open</c> tour (§2.6.1, T7c): four resources of the CRUD engine, each filtered by <c>filter[tourId]</c>, read with
/// <c>Tours.View</c> and written with <c>Tours.Edit</c> on the tour's care — the tab of the tour's editor is a generated
/// list and a generated form. No hand written verb: the one exception of §16.6 stays the legs.
/// </summary>
public static class ShapeEndpoints
{
    public const string HubsPattern = "/api/flightops/hubs";

    public const string RotationsPattern = "/api/flightops/rotations";

    public const string CallsignRulesPattern = "/api/flightops/callsign-rules";

    /// <summary>The filters and sequence rules of an <c>Open</c> tour (T7c).</summary>
    public const string ConstraintsPattern = "/api/flightops/tour-constraints";

    public static IEndpointRouteBuilder MapShapeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new ShapeMapper();

        app.MapCrud<TourHub, HubDto, HubDto, HubWriteDto>(HubsPattern, options =>
        {
            Common(options, "FlightOpsHubs");
            options.DefaultOrder = hub => hub.Sort;
            options.ToList = mapper.ToDto;
            options.ToDetail = mapper.ToDto;
            options.Apply = mapper.Apply;
            options.BeforeAuthorize = (hub, saving) => AdoptAsync(hub, saving);
            options.BeforeSave = SaveHubAsync;
            options.Delete = DeleteHubAsync;
        });

        app.MapCrud<Rotation, RotationListDto, RotationDto, RotationWriteDto>(RotationsPattern, options =>
        {
            Common(options, "FlightOpsRotations");
            options.DefaultOrder = rotation => rotation.Sort;
            options.Filterable.Add(nameof(Rotation.HubId));
            // The page adds the hub and the legs; one row alone is what the engine asks for when it has no page.
            options.ToList = rotation => mapper.ToList(rotation, hubIcao: null, legs: 0);
            options.ToListPage = RotationPageAsync;
            options.ToDetail = mapper.ToDto;
            options.Apply = mapper.Apply;
            options.BeforeAuthorize = (rotation, saving) => AdoptAsync(rotation, saving);
            options.BeforeSave = SaveRotationAsync;
            options.Delete = DeleteRotationAsync;
        });

        app.MapCrud<CallsignRule, CallsignRuleListDto, CallsignRuleDto, CallsignRuleWriteDto>(CallsignRulesPattern, options =>
        {
            Common(options, "FlightOpsCallsignRules");
            options.DefaultOrder = rule => rule.Id;
            options.ToList = rule => mapper.ToList(rule, legNumber: null);
            options.ToListPage = CallsignRulePageAsync;
            options.ToDetail = mapper.ToDto;
            options.Apply = mapper.Apply;

            // A template is changed with its own permission (§1.10), and so are its constraints.
            options.ExtraWritePolicy = rule => rule.OnTemplate ? TourPermissions.ManageTemplates : null;
            options.BeforeAuthorize = async (rule, saving) =>
            {
                var (tour, refusal) = await saving.Services.GetRequiredService<TourChildren>().AdoptAsync(rule, saving.CancellationToken);
                rule.OnTemplate = tour?.IsTemplate == true;
                return refusal;
            };
            options.BeforeSave = SaveCallsignRuleAsync;
        });

        app.MapCrud<TourConstraint, TourConstraintListDto, TourConstraintDto, TourConstraintWriteDto>(ConstraintsPattern, options =>
        {
            Common(options, "FlightOpsTourConstraints");
            options.DefaultOrder = constraint => constraint.Id;
            options.ToList = ConstraintMapper.ToList;
            options.ToDetail = ConstraintMapper.ToDto;
            options.Apply = ConstraintMapper.Apply;

            // A template is changed with its own permission (§1.10), and so are its constraints — as the callsigns'.
            options.ExtraWritePolicy = constraint => constraint.OnTemplate ? TourPermissions.ManageTemplates : null;
            options.BeforeAuthorize = async (constraint, saving) =>
            {
                var (tour, refusal) = await saving.Services.GetRequiredService<TourChildren>().AdoptAsync(constraint, saving.CancellationToken);
                constraint.OnTemplate = tour?.IsTemplate == true;
                return refusal;
            };
            options.BeforeSave = SaveConstraintAsync;
            options.Delete = DeleteConstraintAsync;
        });

        return app;
    }

    private static void Common<TEntity, TList, TDetail, TWrite>(CrudOptions<TEntity, TList, TDetail, TWrite> options, string name)
        where TEntity : class, ITourChild
    {
        options.PermissionArea = TourPermissions.Area;
        options.Name = name;
        options.ReadPolicy = TourPermissions.View;
        options.WritePolicy = TourPermissions.Edit;
        options.ContextType = typeof(FlightOpsDbContext);
        options.Filterable.Add(nameof(ITourChild.TourId));
        options.Sortable.Add("UpdatedAt");
    }

    private static async Task<IReadOnlyDictionary<string, string[]>?> AdoptAsync(ITourChild row, CrudSaving saving) =>
        (await saving.Services.GetRequiredService<TourChildren>().AdoptAsync(row, saving.CancellationToken)).Refusal;

    /// <summary>A hub belongs to a hub tour that is not a template (§1.10), is an airport the hub knows, once per tour.</summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveHubAsync(TourHub hub, CrudSaving saving)
    {
        var children = saving.Services.GetRequiredService<TourChildren>();
        var database = (FlightOpsDbContext)saving.Database;

        if (await children.TourAsync(hub.TourId, saving.CancellationToken) is not { } tour)
        {
            return TourChildren.Refusal("tourId", "flightops:errors.tourUnknown");
        }

        if (HubsRefused(tour) is { } refused)
        {
            return refused;
        }

        var found = await saving.Services.GetRequiredService<IAirportDirectory>().FindAsync([hub.Icao], saving.CancellationToken);
        if (!found.ContainsKey(hub.Icao))
        {
            return TourChildren.Refusal("icao", "flightops:errors.airportUnknown");
        }

        if (await database.Hubs.AnyAsync(row => row.TourId == hub.TourId && row.Icao == hub.Icao && row.Id != hub.Id, saving.CancellationToken))
        {
            return TourChildren.Refusal("icao", "flightops:errors.hubTaken");
        }

        return await children.StillReadyAsync(
            tour,
            parts => parts with { Hubs = TourChildren.Replacing(parts.Hubs, hub, row => row.Id) },
            saving.CancellationToken);
    }

    /// <summary>A hub with rotations stays: its rotations go first (note 2026-09-21-la-forma-dei-tour §4).</summary>
    private static async Task DeleteHubAsync(TourHub hub, IServiceProvider services, CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FlightOpsDbContext>();

        if (await database.Rotations.AnyAsync(rotation => rotation.HubId == hub.Id, cancellationToken))
        {
            throw new DomainRefusalException("id", "flightops:errors.hubHasRotations");
        }

        var children = services.GetRequiredService<TourChildren>();
        if (await children.TourAsync(hub.TourId, cancellationToken) is { } tour)
        {
            await children.ThrowUnlessStillReadyAsync(
                tour,
                parts => parts with { Hubs = [.. parts.Hubs.Where(row => row.Id != hub.Id)] },
                cancellationToken);
        }

        database.Hubs.Remove(hub);
    }

    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveRotationAsync(Rotation rotation, CrudSaving saving)
    {
        var children = saving.Services.GetRequiredService<TourChildren>();
        var database = (FlightOpsDbContext)saving.Database;

        if (await children.TourAsync(rotation.TourId, saving.CancellationToken) is not { } tour)
        {
            return TourChildren.Refusal("tourId", "flightops:errors.tourUnknown");
        }

        if (HubsRefused(tour) is { } refused)
        {
            return refused;
        }

        if (!await database.Hubs.AnyAsync(hub => hub.Id == rotation.HubId && hub.TourId == rotation.TourId, saving.CancellationToken))
        {
            return TourChildren.Refusal("hubId", "flightops:errors.hubUnknown");
        }

        return await children.StillReadyAsync(
            tour,
            parts => parts with { Rotations = TourChildren.Replacing(parts.Rotations, rotation, row => row.Id) },
            saving.CancellationToken);
    }

    /// <summary>A rotation with legs stays: they are moved or removed first, and one with reports is retired whole (§1.4.1).</summary>
    private static async Task DeleteRotationAsync(Rotation rotation, IServiceProvider services, CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FlightOpsDbContext>();

        if (await database.Legs.AnyAsync(leg => leg.RotationId == rotation.Id, cancellationToken))
        {
            throw new DomainRefusalException("id", "flightops:errors.rotationHasLegs");
        }

        var children = services.GetRequiredService<TourChildren>();
        if (await children.TourAsync(rotation.TourId, cancellationToken) is { } tour)
        {
            await children.ThrowUnlessStillReadyAsync(
                tour,
                parts => parts with { Rotations = [.. parts.Rotations.Where(row => row.Id != rotation.Id)] },
                cancellationToken);
        }

        database.Rotations.Remove(rotation);
    }

    /// <summary>A constraint of a leg names a leg of the same tour; a template has none (§1.10).</summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveCallsignRuleAsync(CallsignRule rule, CrudSaving saving)
    {
        var database = (FlightOpsDbContext)saving.Database;

        if (rule.LegId is { } legId
            && !await database.Legs.AnyAsync(leg => leg.Id == legId && leg.TourId == rule.TourId, saving.CancellationToken))
        {
            return TourChildren.Refusal("legId", "flightops:errors.legUnknown");
        }

        return null;
    }

    /// <summary>
    /// A constraint belongs to an <c>Open</c> tour (note 2026-09-22-il-tour-open §2.2), once per kind — once per airport for
    /// <see cref="TourConstraintKind.MinFlightsAt"/> —, with the parameters its kind takes, normalized, and what they name
    /// existing.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveConstraintAsync(TourConstraint constraint, CrudSaving saving)
    {
        var children = saving.Services.GetRequiredService<TourChildren>();
        var database = (FlightOpsDbContext)saving.Database;

        if (await children.TourAsync(constraint.TourId, saving.CancellationToken) is not { } tour)
        {
            return TourChildren.Refusal("tourId", "flightops:errors.tourUnknown");
        }

        if (tour.Kind != TourKind.Open)
        {
            return TourChildren.Refusal("tourId", "flightops:errors.kindHasNoConstraints");
        }

        var (parameters, problems) = OpenCatalog.Read(constraint.Kind, OpenCatalog.Parse(constraint.ParametersJson));
        if (OpenParameterCheck.Refusal("parameters", problems) is { } wrong)
        {
            return wrong;
        }

        var fields = OpenCatalog.Fields(constraint.Kind);
        var unknown = await saving.Services.GetRequiredService<OpenParameterCheck>().UnknownAsync(fields, parameters, saving.CancellationToken);
        if (OpenParameterCheck.Refusal("parameters", unknown) is { } missing)
        {
            return missing;
        }

        constraint.ParametersJson = parameters.ToJsonString();

        var others = await database.TourConstraints.AsNoTracking()
            .Where(row => row.TourId == constraint.TourId && row.Kind == constraint.Kind && row.Id != constraint.Id)
            .ToListAsync(saving.CancellationToken);

        if (!OpenCatalog.Repeats(constraint.Kind) && others.Count > 0)
        {
            return TourChildren.Refusal("kind", "flightops:errors.constraintTaken");
        }

        var airport = OpenCatalog.Codes(parameters, "airport");
        if (OpenCatalog.Repeats(constraint.Kind)
            && others.Any(row => OpenCatalog.Codes(OpenCatalog.Parse(row.ParametersJson), "airport").SequenceEqual(airport)))
        {
            return TourChildren.Refusal("parameters.airport", "flightops:errors.constraintTaken");
        }

        return await children.StillReadyAsync(
            tour,
            parts => parts with { Constraints = TourChildren.Replacing(parts.Constraints, constraint, row => row.Id) },
            saving.CancellationToken);
    }

    private static async Task DeleteConstraintAsync(TourConstraint constraint, IServiceProvider services, CancellationToken cancellationToken)
    {
        var children = services.GetRequiredService<TourChildren>();
        if (await children.TourAsync(constraint.TourId, cancellationToken) is { } tour)
        {
            await children.ThrowUnlessStillReadyAsync(
                tour,
                parts => parts with { Constraints = [.. parts.Constraints.Where(row => row.Id != constraint.Id)] },
                cancellationToken);
        }

        services.GetRequiredService<FlightOpsDbContext>().TourConstraints.Remove(constraint);
    }

    private static IReadOnlyDictionary<string, string[]>? HubsRefused(Tour tour) =>
        tour.IsTemplate ? TourChildren.Refusal("tourId", "flightops:errors.templateHasNoHubs")
        : tour.Kind != TourKind.Hub ? TourChildren.Refusal("tourId", "flightops:errors.kindHasNoHubs")
        : null;

    /// <summary>Each rotation with its hub's code and the count of its legs still flown: one query per page, not per row.</summary>
    private static async Task<IReadOnlyList<RotationListDto>> RotationPageAsync(
        IReadOnlyList<Rotation> rotations,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FlightOpsDbContext>();
        var ids = rotations.Select(rotation => rotation.Id).ToArray();
        var hubIds = rotations.Select(rotation => rotation.HubId).Distinct().ToArray();

        var hubs = await database.Hubs.AsNoTracking().Where(hub => hubIds.Contains(hub.Id))
            .ToDictionaryAsync(hub => hub.Id, hub => hub.Icao, cancellationToken);
        var legs = await database.Legs.AsNoTracking()
            .Where(leg => leg.RotationId != null && ids.Contains(leg.RotationId.Value) && leg.RetiredAt == null)
            .GroupBy(leg => leg.RotationId!.Value)
            .Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);

        var mapper = new ShapeMapper();
        return [.. rotations.Select(rotation => mapper.ToList(rotation, hubs.GetValueOrDefault(rotation.HubId), legs.GetValueOrDefault(rotation.Id)))];
    }

    private static async Task<IReadOnlyList<CallsignRuleListDto>> CallsignRulePageAsync(
        IReadOnlyList<CallsignRule> rules,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FlightOpsDbContext>();
        var legIds = rules.Select(rule => rule.LegId).OfType<long>().Distinct().ToArray();

        var numbers = await database.Legs.AsNoTracking().Where(leg => legIds.Contains(leg.Id))
            .ToDictionaryAsync(leg => leg.Id, leg => leg.Number, cancellationToken);

        var mapper = new ShapeMapper();
        return [.. rules.Select(rule => mapper.ToList(rule, rule.LegId is { } id && numbers.TryGetValue(id, out var number) ? number : null))];
    }
}
