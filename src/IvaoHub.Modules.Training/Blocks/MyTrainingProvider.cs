using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Modules.Training.Requests;

namespace IvaoHub.Modules.Training.Blocks;

/// <summary>
/// The block <c>training.myTraining</c> (design M3 §4.3; note <c>il-training-in-pubblico</c>, «Da portare nel piano»: <c>/me</c> receives
/// this block instead of a page of its own): the reader's own training, ladder by ladder — the open request, the next date, «choose the
/// date», the last report, «ready for…», the waiting, a ban. The answer of the trainee's own page, <c>GET /api/training/mine</c>
/// (<see cref="TrainingRequests.MineAsync"/>), so nothing of it is worked out twice: which rule refuses a request, until when, and
/// what comes next are the server's, and what a line says of them is the browser's.
/// <para>Always live and with no property, because it is the reader's. A visitor gets <c>signedIn: false</c>: the block belongs on
/// <c>/me</c>, and a page that shows it to the public shows an empty one.</para>
/// </summary>
public sealed class MyTrainingProvider(TrainingRequests requests, ICurrentUser currentUser) : IDataBlockProvider
{
    public const string BlockType = "training.myTraining";

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return BlockAnswers.SignedOut();
        }

        // A member the hub has no row of has nothing to show; the page answers them 404, the block with its sentence.
        return await requests.MineAsync(cancellationToken) is { } mine
            ? BlockAnswers.SignedIn(mine)
            : new JsonObject { ["signedIn"] = true };
    }
}
