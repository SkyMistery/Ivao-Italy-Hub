using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
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
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.FlightOps.Rules;

/// <summary>
/// Rules and errors in the back office (design M2 §5.1): two resources of the CRUD engine, read with <c>Tours.View</c> and
/// written with <c>Tours.ManageRules</c> — the general rules and the errors on the base department, a tour's rules on its
/// tour's care, and a template's with <c>Tours.ManageTemplates</c> too. <c>filter[tour]</c> says whose rules:
/// <c>general</c>, the default, or a tour's identifier.
/// <para>⚠️ Two hand written verbs, counted as the tour's are: the rules as they hold on a tour (§5.2), which are a
/// composition and not rows, and "copy the rules of another tour" (§1.7), which creates rows from rows.</para>
/// </summary>
public static class RuleEndpoints
{
    public const string RulesPattern = "/api/flightops/rules";

    public const string ErrorsPattern = "/api/flightops/errors";

    /// <summary><c>filter[tour]=general</c> (the default) or <c>filter[tour]={id}</c>.</summary>
    public const string TourFilter = "tour";

    public static IEndpointRouteBuilder MapRuleEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var clock = app.ServiceProvider.GetRequiredService<IClock>();

        app.MapCrud<TourRule, TourRuleListDto, TourRuleDto, TourRuleWriteDto>(RulesPattern, options =>
        {
            options.PermissionArea = TourPermissions.Area;
            options.Name = "FlightOpsRules";
            options.ReadPolicy = TourPermissions.View;
            options.WritePolicy = TourPermissions.ManageRules;
            options.ContextType = typeof(FlightOpsDbContext);
            options.Source = database => CrudSource.BackOffice<TourRule>(database).Include(rule => rule.ErrorLinks);
            options.DefaultOrder = rule => rule.Sort;
            options.Sortable.Add(nameof(TourRule.Code));
            options.Sortable.Add(nameof(TourRule.UpdatedAt));
            options.SearchFields.Add(rule => rule.Title);
            options.SearchFields.Add(rule => rule.Code);
            options.CustomFilters[TourFilter] = (query, raw) =>
                raw == "general" ? query.Where(rule => rule.TourId == null)
                : long.TryParse(raw, out var tourId) ? query.Where(rule => rule.TourId == tourId)
                : null;
            options.DefaultFilters[TourFilter] = "general";

            options.ToList = rule => RuleMapper.ToList(rule, amendsCode: null, rule.ErrorLinks.Count);
            options.ToListPage = RulePageAsync;
            options.ToDetail = RuleMapper.ToDto;
            options.Apply = (payload, rule) => RuleMapper.Apply(payload, rule, clock.UtcNow);

            // A template is changed with its own permission (§1.10), and so are its rules — as its constraints.
            options.ExtraWritePolicy = rule => rule.OnTemplate ? TourPermissions.ManageTemplates : null;
            options.BeforeAuthorize = AdoptAsync;
            options.BeforeSave = SaveRuleAsync;
            options.Delete = DeleteRuleAsync;
        });

        app.MapCrud<TourError, TourErrorListDto, TourErrorDto, TourErrorWriteDto>(ErrorsPattern, options =>
        {
            options.PermissionArea = TourPermissions.Area;
            options.Name = "FlightOpsErrors";
            options.ReadPolicy = TourPermissions.View;
            options.WritePolicy = TourPermissions.ManageRules;
            options.ContextType = typeof(FlightOpsDbContext);
            options.DefaultOrder = error => error.Id;
            options.Sortable.Add(nameof(TourError.Category));
            options.Sortable.Add(nameof(TourError.UpdatedAt));
            options.Filterable.Add(nameof(TourError.Category));
            options.Filterable.Add(nameof(TourError.IsPublic));
            options.SearchFields.Add(error => error.Name);

            options.ToList = error => RuleMapper.ToList(error, rules: 0);
            options.ToListPage = ErrorPageAsync;
            options.ToDetail = RuleMapper.ToDto;
            options.Apply = (payload, error) => RuleMapper.Apply(payload, error, clock.UtcNow);
        });

        app.MapGet($"{TourEndpoints.Pattern}/{{id:long}}/effective-rules", EffectiveRulesAsync)
            .WithName("FlightOpsTourEffectiveRules")
            .Produces<IReadOnlyList<EffectiveRuleDto>>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.View);

        app.MapPost($"{TourEndpoints.Pattern}/{{id:long}}/copy-rules", CopyRulesAsync)
            .WithName("FlightOpsTourCopyRules")
            .Produces<CopyRulesResultDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(TourPermissions.ManageRules);

        return app;
    }

    /// <summary>A tour's rule takes its tour's care before the permission is asked; a general one keeps the base department.</summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> AdoptAsync(TourRule rule, CrudSaving saving)
    {
        if (rule.TourId is not { } tourId)
        {
            return null;
        }

        var tour = await saving.Services.GetRequiredService<TourChildren>().TourAsync(tourId, saving.CancellationToken);
        if (tour is null)
        {
            return TourChildren.Refusal("tourId", "flightops:errors.tourUnknown");
        }

        if (tour.PurgedAt is not null)
        {
            return TourChildren.Refusal("tourId", TourState.PurgedKey);
        }

        rule.OwnerDepartment = tour.OwnerDepartment;
        rule.OwnerDepartmentMask = tour.OwnerDepartmentMask;
        rule.OnTemplate = tour.IsTemplate;
        return null;
    }

    /// <summary>
    /// What a rule may refuse only by looking at other rows: the rule it amends — a general one, not retired, amended once
    /// per tour —, its code once in its scope, its parameters those of its check, its errors ones that exist; then the links
    /// written to match.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveRuleAsync(TourRule rule, CrudSaving saving)
    {
        var database = (FlightOpsDbContext)saving.Database;
        var amending = rule.AmendsRuleId is not null;

        if (rule.AmendsRuleId is { } amendedId)
        {
            var amended = await database.Rules.AsNoTracking().FirstOrDefaultAsync(row => row.Id == amendedId, saving.CancellationToken);
            if (amended is null || !amended.IsGeneral || amended.RetiredAt is not null)
            {
                return TourChildren.Refusal("amendsRuleId", "flightops:errors.amendedUnknown");
            }

            if (await database.Rules.AnyAsync(
                    row => row.TourId == rule.TourId && row.AmendsRuleId == amendedId && row.RetiredAt == null && row.Id != rule.Id,
                    saving.CancellationToken))
            {
                return TourChildren.Refusal("amendsRuleId", "flightops:errors.alreadyAmended");
            }

            // The check is the rule's: an amendment changes its numbers, never what they are numbers of.
            rule.CheckKey = amended.CheckKey;
        }

        var (parameters, problems) = CheckCatalog.Read(rule.CheckKey, OpenCatalog.Parse(rule.ParametersJson), amending);
        if (OpenParameterCheck.Refusal("parameters", problems) is { } wrong)
        {
            return wrong;
        }

        rule.ParametersJson = parameters.ToJsonString();

        if (rule.RetiredAt is null
            && await database.Rules.AnyAsync(
                row => row.TourId == rule.TourId && row.Code == rule.Code && row.RetiredAt == null && row.Id != rule.Id,
                saving.CancellationToken))
        {
            return TourChildren.Refusal("code", "flightops:errors.ruleCodeTaken");
        }

        var wanted = rule.RequestedErrorIds ?? [];
        if (wanted.Count > 0
            && await database.Errors.CountAsync(error => wanted.Contains(error.Id), saving.CancellationToken) != wanted.Count)
        {
            return TourChildren.Refusal("errorIds", "flightops:errors.errorUnknown");
        }

        rule.ErrorLinks.RemoveAll(link => !wanted.Contains(link.ErrorId));
        rule.ErrorLinks.AddRange(wanted
            .Where(id => rule.ErrorLinks.All(link => link.ErrorId != id))
            .Select(id => new TourRuleError { RuleId = rule.Id, ErrorId = id }));

        return null;
    }

    /// <summary>A general rule some tour amends stays: the amendments go first, or it is retired (§5.2).</summary>
    private static async Task DeleteRuleAsync(TourRule rule, IServiceProvider services, CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FlightOpsDbContext>();

        if (rule.IsGeneral && await database.Rules.AnyAsync(row => row.AmendsRuleId == rule.Id, cancellationToken))
        {
            throw new DomainRefusalException("id", "flightops:errors.ruleHasAmendments");
        }

        database.Rules.Remove(rule);
    }

    /// <summary>Each rule with the code of the rule it amends and its errors, the amended rule's included: one query per page.</summary>
    private static async Task<IReadOnlyList<TourRuleListDto>> RulePageAsync(
        IReadOnlyList<TourRule> rules,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FlightOpsDbContext>();
        var amendedIds = rules.Select(rule => rule.AmendsRuleId).OfType<long>().Distinct().ToArray();

        var amended = await database.Rules.AsNoTracking()
            .Where(rule => amendedIds.Contains(rule.Id))
            .Select(rule => new { rule.Id, rule.Code })
            .ToDictionaryAsync(rule => rule.Id, rule => rule.Code, cancellationToken);
        var amendedErrors = (await database.RuleErrors.AsNoTracking()
            .Where(link => amendedIds.Contains(link.RuleId))
            .ToListAsync(cancellationToken))
            .ToLookup(link => link.RuleId, link => link.ErrorId);

        return
        [
            .. rules.Select(rule => RuleMapper.ToList(
                rule,
                rule.AmendsRuleId is { } id ? amended.GetValueOrDefault(id) : null,
                rule.ErrorLinks.Select(link => link.ErrorId)
                    .Concat(rule.AmendsRuleId is { } of ? amendedErrors[of] : [])
                    .Distinct()
                    .Count())),
        ];
    }

    /// <summary>Each error with how many rules in force name it: one query per page.</summary>
    private static async Task<IReadOnlyList<TourErrorListDto>> ErrorPageAsync(
        IReadOnlyList<TourError> errors,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<FlightOpsDbContext>();
        var ids = errors.Select(error => error.Id).ToArray();

        var counts = await database.RuleErrors.AsNoTracking()
            .Where(link => ids.Contains(link.ErrorId))
            .Join(database.Rules.Where(rule => rule.RetiredAt == null), link => link.RuleId, rule => rule.Id, (link, _) => link.ErrorId)
            .GroupBy(errorId => errorId)
            .Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);

        return [.. errors.Select(error => RuleMapper.ToList(error, counts.GetValueOrDefault(error.Id)))];
    }

    private static async Task<IResult> EffectiveRulesAsync(
        long id,
        FlightOpsDbContext database,
        EffectiveRules rules,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var tour = await CrudSource.BackOffice<Tour>(database).AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, http.RequestAborted);
        if (tour is null)
        {
            return TourEndpoints.NotFound(catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, tour, TourPermissions.View)).Succeeded)
        {
            return TourEndpoints.Forbidden(catalog, currentUser);
        }

        return Results.Ok((await rules.ForTourAsync(tour, http.RequestAborted)).Select(RuleMapper.ToDto).ToList());
    }

    /// <summary>
    /// The rules of another tour added to this one, with their parameters and errors (§1.7): its own rules in force, never
    /// its parent's nor the general ones. What this tour already has is kept — an amendment of the same general rule, a
    /// rule with the same code — and the copy skips it, saying which (Carmine, 22 September 2026).
    /// </summary>
    private static async Task<IResult> CopyRulesAsync(
        long id,
        CopyRulesRequest request,
        FlightOpsDbContext database,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var tours = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .Where(row => row.Id == id || row.Id == request.SourceTourId)
            .ToListAsync(http.RequestAborted);
        var target = tours.FirstOrDefault(row => row.Id == id);
        var source = tours.FirstOrDefault(row => row.Id == request.SourceTourId);

        if (target is null)
        {
            return TourEndpoints.NotFound(catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, target, TourPermissions.ManageRules)).Succeeded
            || (target.IsTemplate && !(await authorization.AuthorizeAsync(http.User, target, TourPermissions.ManageTemplates)).Succeeded))
        {
            return TourEndpoints.Forbidden(catalog, currentUser);
        }

        if (target.PurgedAt is not null)
        {
            return TourEndpoints.Refused("id", TourState.PurgedKey, catalog, currentUser);
        }

        if (source is null || source.Id == target.Id || !(await authorization.AuthorizeAsync(http.User, source, TourPermissions.View)).Succeeded)
        {
            return TourEndpoints.Refused("sourceTourId", "flightops:errors.tourUnknown", catalog, currentUser);
        }

        var existing = await database.Rules.AsNoTracking()
            .Where(rule => rule.TourId == target.Id && rule.RetiredAt == null)
            .ToListAsync(http.RequestAborted);
        var copied = await database.Rules.AsNoTracking().Include(rule => rule.ErrorLinks)
            .Where(rule => rule.TourId == source.Id && rule.RetiredAt == null)
            .OrderBy(rule => rule.Sort).ThenBy(rule => rule.Id)
            .ToListAsync(http.RequestAborted);

        var (added, skipped) = TourCopy.Rules(copied, target, existing);
        database.Rules.AddRange(added);
        await database.SaveChangesAsync(http.RequestAborted);

        return Results.Ok(new CopyRulesResultDto(added.Count, skipped));
    }
}
