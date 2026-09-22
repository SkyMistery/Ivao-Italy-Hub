using System.Text.Json.Nodes;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Rules;

/// <summary>
/// One rule as it holds on a tour: the row that says it — the general rule, or the tour's that amends it, or the tour's
/// own — the general rule it takes the place of, the parameters with every amendment applied, and the errors of all of
/// them together (an amendment keeps its rule's errors and may add its own).
/// </summary>
public sealed record EffectiveRule(TourRule Rule, TourRule? Amended, JsonObject Parameters, IReadOnlyList<long> ErrorIds)
{
    /// <summary>The check the rule carries the parameters of: an amendment's is its rule's.</summary>
    public string? CheckKey => Amended?.CheckKey ?? Rule.CheckKey;
}

/// <summary>
/// The rules that hold on a tour (design M2 §5.2): the general ones not retired, each replaced by the tour's rule that
/// amends it — with the parameters that one changes over the general's —, then the tour's own; for a subtour, the
/// parent's first and then the subtour's, which may amend a general rule its parent already amended and then wins. One
/// service, read by the tour's page, the report, the checks and the validation.
/// <para>An amendment of a general rule that is retired goes with it: the rule it changed is no longer in the regulation.</para>
/// </summary>
public sealed class EffectiveRules(FlightOpsDbContext database)
{
    public async Task<IReadOnlyList<EffectiveRule>> ForTourAsync(Tour tour, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tour);

        var tourIds = tour.ParentTourId is { } parentId ? new[] { parentId, tour.Id } : [tour.Id];
        var rules = await database.Rules.AsNoTracking()
            .Where(rule => rule.TourId == null || tourIds.Contains(rule.TourId.Value))
            .ToListAsync(cancellationToken);
        var ids = rules.Select(rule => rule.Id).ToArray();
        var links = await database.RuleErrors.AsNoTracking()
            .Where(link => ids.Contains(link.RuleId))
            .ToListAsync(cancellationToken);

        return Compose(
            [.. rules.Where(rule => rule.IsGeneral)],
            [.. tourIds.Select(id => (IReadOnlyList<TourRule>)[.. rules.Where(rule => rule.TourId == id)])],
            links);
    }

    /// <summary>
    /// The composition itself, a pure function: the general rules, the layers of a tour's rules from the outermost — the
    /// parent, then the subtour — and the links between rules and errors.
    /// </summary>
    public static IReadOnlyList<EffectiveRule> Compose(
        IReadOnlyList<TourRule> generals,
        IReadOnlyList<IReadOnlyList<TourRule>> layers,
        IReadOnlyList<TourRuleError> links)
    {
        ArgumentNullException.ThrowIfNull(generals);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(links);

        var errors = links.ToLookup(link => link.RuleId, link => link.ErrorId);
        var effective = InOrder(generals)
            .Select(rule => new EffectiveRule(rule, null, OpenCatalog.Parse(rule.ParametersJson), [.. errors[rule.Id].Order()]))
            .ToList();

        foreach (var layer in layers)
        {
            foreach (var rule in InOrder(layer))
            {
                var own = OpenCatalog.Parse(rule.ParametersJson);

                if (rule.AmendsRuleId is not { } amendedId)
                {
                    effective.Add(new EffectiveRule(rule, null, own, [.. errors[rule.Id].Order()]));
                    continue;
                }

                var at = effective.FindIndex(entry => (entry.Amended ?? entry.Rule).Id == amendedId);
                if (at < 0)
                {
                    continue;
                }

                var before = effective[at];
                effective[at] = new EffectiveRule(
                    rule,
                    before.Amended ?? before.Rule,
                    CheckCatalog.Merge(before.Parameters, own),
                    [.. before.ErrorIds.Concat(errors[rule.Id]).Distinct().Order()]);
            }
        }

        return effective;
    }

    private static IEnumerable<TourRule> InOrder(IEnumerable<TourRule> rules) => rules
        .Where(rule => rule.RetiredAt is null)
        .OrderBy(rule => rule.Sort)
        .ThenBy(rule => rule.Code, StringComparer.Ordinal)
        .ThenBy(rule => rule.Id);
}
