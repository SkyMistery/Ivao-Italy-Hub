using System.Text.Json.Nodes;
using IvaoHub.Core.Content;

namespace IvaoHub.Modules.Events.Public;

/// <summary>
/// The block <c>events.eventList</c> (design M4 §7.3, E4): the events to come and those in progress, the soonest first, as the cards
/// of <c>/events</c> — on the home page, on a page of the site, on a dashboard, and on <c>/events</c> itself, which reads it live as
/// <c>/calendar</c> reads the block of the calendar. Of the kinds the block names, or of every kind; at most as many as it asks.
/// <para>Always live: a list of events captured on the day a page was published would keep events long over — which the site never
/// shows (§2.4) — and miss the ones announced since. So it answers for whoever is looking, through the global query filter: an event
/// for the members only to a signed in reader.</para>
/// </summary>
public sealed class EventListProvider(PublicEvents events) : IDataBlockProvider
{
    public const string BlockType = "events.eventList";

    /// <summary>The kinds to show, as entries of <c>{ kind }</c> like the block of the calendar; none named, every kind.</summary>
    public const string KindsProperty = "kinds";

    /// <summary>
    /// At most this many events: nothing written, <see cref="DefaultLimit"/>, as the block's schema; zero, as many as
    /// <see cref="PublicEvents.MaxItems"/>.
    /// </summary>
    public const string LimitProperty = "limit";

    /// <summary>How many events a block that writes no limit shows: the same ten as the schema's default, like the core's blocks.</summary>
    public const int DefaultLimit = 10;

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        var kinds = BlockProps.Entries(props, KindsProperty, "kind").Distinct(StringComparer.Ordinal).ToArray();
        var limit = BlockProps.Number(props, LimitProperty) ?? DefaultLimit;

        var cards = await events.CardsAsync(kinds, limit <= 0 ? PublicEvents.MaxItems : limit, cancellationToken);
        var items = new JsonArray();

        foreach (var card in cards)
        {
            items.Add(new JsonObject
            {
                ["id"] = card.Id,
                ["slug"] = card.Slug,
                ["kind"] = card.Kind,
                ["title"] = BlockProps.Translated(card.Title),
                ["summary"] = BlockProps.Translated(card.Summary),
                ["bannerMediaId"] = card.BannerMediaId,
                ["state"] = card.State.ToString(),
                ["startsAtUtc"] = BlockProps.Instant(card.StartsAtUtc),
                ["endsAtUtc"] = BlockProps.Instant(card.EndsAtUtc),
                ["wholeDivision"] = card.WholeDivision,
                ["airports"] = new JsonArray(
                [
                    .. card.Airports.Select(airport => (JsonNode?)new JsonObject { ["icao"] = airport.Icao, ["name"] = airport.Name }),
                ]),
            });
        }

        return new JsonObject { ["items"] = items };
    }
}
