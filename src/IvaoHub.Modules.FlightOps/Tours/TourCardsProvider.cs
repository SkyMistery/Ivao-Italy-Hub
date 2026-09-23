using System.Text.Json.Nodes;
using IvaoHub.Core.Content;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>
/// The block <c>flightops.tourCards</c> (design M2 §8.2): the cards of the tours the public sees, the same ones
/// <c>/tours</c> shows, on a page or on a dashboard. Always live — a list of tours captured on the day a page was
/// published would go stale the moment a tour opens or closes (Carmine, 22 September 2026).
/// <para>It takes two properties: which states to show and how many cards at most. They are the first properties of a
/// module's block, and the reason <c>BlockRegistration</c> grew a namespace for its labels: the property form reads
/// them from the language files, and a module writes its own, not the core's.</para>
/// </summary>
public sealed class TourCardsProvider(PublicTours tours) : IDataBlockProvider
{
    public const string BlockType = "flightops.tourCards";

    /// <summary>Which tours to show, as the property holds them; nothing chosen shows the three states of a card.</summary>
    public const string StatesProperty = "states";

    /// <summary>At most this many cards; nothing written, all of them.</summary>
    public const string LimitProperty = "limit";

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        // Nothing chosen — no property, or a property with nothing ticked, which is what the editor writes — is every
        // state a card can be in.
        //
        // ⚠️ But a property that *does* name states, of which none is a state a card can be in, narrows to nothing: a
        // property that narrows must never widen (the rule `BlockProps.TryDepartment` already follows). Written the
        // other way round first, and the test of the block caught it.
        var states = props?[StatesProperty] is JsonArray chosen && chosen.Count > 0
            ? chosen
                .Select(entry => entry?.GetValue<string>())
                .Select(name => Enum.TryParse<TourStateKind>(name, ignoreCase: true, out var state) ? state : (TourStateKind?)null)
                .OfType<TourStateKind>()
                .Where(PublicTours.CardStates.Contains)
                .ToList()
            : null;

        var cards = await tours.CardsAsync(states, BlockProps.Number(props, LimitProperty), cancellationToken);
        var items = new JsonArray();

        foreach (var card in cards)
        {
            items.Add(new JsonObject
            {
                ["id"] = card.Id,
                ["slug"] = card.Slug,
                ["kind"] = card.Kind.ToString(),
                ["title"] = BlockProps.Translated(card.Title),
                ["summary"] = BlockProps.Translated(card.Summary),
                ["coverMediaId"] = card.CoverMediaId,
                ["state"] = card.State.ToString(),
                ["releaseAt"] = card.ReleaseAt,
                ["closeAt"] = card.CloseAt,
                ["legs"] = card.Legs,
                ["totalNm"] = card.TotalNm,
            });
        }

        return new JsonObject { ["items"] = items };
    }
}
