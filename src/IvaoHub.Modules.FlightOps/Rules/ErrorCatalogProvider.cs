using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using IvaoHub.Modules.FlightOps.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Rules;

/// <summary>
/// The block <c>flightops.errorCatalog</c> (design M2 §5.3, §8.2): the errors the division made public, with their category,
/// description, examples and the general rules in force they belong to. Always live — a catalogue captured on the day a page
/// was published would keep an error the flight operations department has since retired. A tour's own rules are not named:
/// the tour may be a draft, and a page is read by anybody.
/// <para>What is public is the row's own flag, <c>is_public</c> (answer 3); the rows are not <c>IVisible</c>, so nothing
/// else filters them and this is the one question asked.</para>
/// </summary>
public sealed class ErrorCatalogProvider(FlightOpsDbContext database) : IDataBlockProvider
{
    public const string BlockType = "flightops.errorCatalog";

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // It takes no property: the catalogue is the division's, whole (the form of a module block's properties would need
        // labels in the core's language files, which a module does not write).
        var query = database.Errors.AsNoTracking().Where(error => error.IsPublic && error.RetiredAt == null);

        // By weight, in the enum's order: the category is stored by name, so the database would sort it alphabetically.
        var errors = (await query.ToListAsync(cancellationToken))
            .OrderBy(error => error.Category)
            .ThenBy(error => error.Id)
            .ToList();
        var ids = errors.Select(error => error.Id).ToArray();

        var rules = (await database.RuleErrors.AsNoTracking()
            .Where(link => ids.Contains(link.ErrorId))
            .Join(
                database.Rules.Where(rule => rule.TourId == null && rule.RetiredAt == null),
                link => link.RuleId,
                rule => rule.Id,
                (link, rule) => new { link.ErrorId, Rule = rule })
            .ToListAsync(cancellationToken))
            .ToLookup(row => row.ErrorId, row => row.Rule);

        var items = new JsonArray();
        foreach (var error in errors)
        {
            var linked = new JsonArray();
            foreach (var rule in rules[error.Id].OrderBy(rule => rule.Sort).ThenBy(rule => rule.Code, StringComparer.Ordinal))
            {
                linked.Add(new JsonObject { ["code"] = rule.Code, ["title"] = BlockProps.Translated(rule.Title) });
            }

            items.Add(new JsonObject
            {
                ["id"] = error.Id,
                ["name"] = BlockProps.Translated(error.Name),
                ["description"] = BlockProps.Translated(error.Description),
                ["examples"] = BlockProps.Translated(error.Examples),
                ["category"] = error.Category.ToString(),
                ["yearlyMax"] = error.YearlyMax,
                ["rules"] = linked,
            });
        }

        return new JsonObject { ["items"] = items };
    }
}
