using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Modules.Training.Public;

namespace IvaoHub.Modules.Training.Blocks;

/// <summary>
/// The block <c>training.upcomingSessions</c> (design M3 §4.1, §4.3; note <c>il-training-in-pubblico</c>): the sessions still to be
/// held and the exams still to come (A10c), the soonest first, as <c>/training</c> shows them — on a page of the site or on a dashboard.
/// The sessions are <c>items</c>, the exams <c>exams</c>, each at most as many as the property asks: the page draws them in one list
/// and keeps as many of them.
/// <para>Always live, because it answers for whoever is looking: the people only to a signed in reader, which a capture taken when a
/// page is published would hand to whoever opens it (note <c>frozen-e-visibilita</c>); and a list of sessions captured on the day of
/// publication would keep sessions long held and miss the ones dated since. It takes one property, how many at most.</para>
/// </summary>
public sealed class UpcomingSessionsProvider(PublicSessions sessions, ICurrentUser currentUser) : IDataBlockProvider
{
    public const string BlockType = "training.upcomingSessions";

    /// <summary>At most this many sessions; nothing written, or none, as many as <see cref="PublicSessions.MaxItems"/>.</summary>
    public const string LimitProperty = "limit";

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        var limit = BlockProps.Number(props, LimitProperty);

        return new JsonObject
        {
            ["signedIn"] = currentUser.IsAuthenticated,
            ["items"] = BlockAnswers.Of(await sessions.UpcomingAsync(limit, cancellationToken)),
            ["exams"] = BlockAnswers.Of(await sessions.UpcomingExamsAsync(limit, cancellationToken)),
        };
    }
}
