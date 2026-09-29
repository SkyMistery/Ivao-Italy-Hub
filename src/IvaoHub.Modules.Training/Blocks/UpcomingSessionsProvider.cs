using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Modules.Training.Public;

namespace IvaoHub.Modules.Training.Blocks;

/// <summary>
/// The block <c>training.upcomingSessions</c> (design M3 §4.1, §4.3; note <c>il-training-in-pubblico</c>): the sessions still to be
/// held, the soonest first, as <c>/training</c> shows them — on a page of the site or on a dashboard. The trainings only: the exams
/// join them with A10c.
/// <para>Always live, because it answers for whoever is looking: the trainee and the trainer only to a signed in reader, which a
/// capture taken when a page is published would hand to whoever opens it (note <c>frozen-e-visibilita</c>); and a list of sessions
/// captured on the day of publication would keep sessions long held and miss the ones dated since. It takes one property, how many
/// at most.</para>
/// </summary>
public sealed class UpcomingSessionsProvider(PublicSessions sessions, ICurrentUser currentUser) : IDataBlockProvider
{
    public const string BlockType = "training.upcomingSessions";

    /// <summary>
    /// At most this many sessions: nothing written, <see cref="DefaultLimit"/>, as the block's schema; zero, as many as
    /// <see cref="PublicSessions.MaxItems"/>.
    /// </summary>
    public const string LimitProperty = "limit";

    /// <summary>
    /// How many sessions a block that writes no limit shows — one saved through the API or a seed, which the editor's default never
    /// reached; the same ten as the schema's default, like the core's blocks.
    /// </summary>
    public const int DefaultLimit = 10;

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken) =>
        new JsonObject
        {
            ["signedIn"] = currentUser.IsAuthenticated,
            ["items"] = BlockAnswers.Of(
                await sessions.UpcomingAsync(BlockProps.Number(props, LimitProperty) ?? DefaultLimit, cancellationToken)),
        };
}
