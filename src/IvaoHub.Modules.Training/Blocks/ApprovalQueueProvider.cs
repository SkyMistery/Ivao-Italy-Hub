using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Training.Blocks;

/// <summary>One queue of the staff on a dashboard: how many trainings wait in it for the reader, and the oldest of them.</summary>
/// <param name="Count">How many the reader may move, among the ones read.</param>
/// <param name="Oldest">The oldest requests first, at most <see cref="ApprovalQueueProvider.MaxItems"/>, as the staff's list shows them.</param>
public sealed record TrainingQueueDto(int Count, IReadOnlyList<StaffTrainingRowDto> Oldest);

/// <summary>
/// The requests to accept or refuse, and the trainings to assign — accepted with no trainer yet, or open with a trainer whose data was
/// erased (A12b) —, that wait for the reader.
/// </summary>
public sealed record ApprovalQueueDto(TrainingQueueDto ToApprove, TrainingQueueDto ToAssign);

/// <summary>
/// The block <c>training.approvalQueue</c> (design M3 §4.3): the requests to accept or refuse and the trainings to assign, for whoever
/// is looking — the views «to approve» and «to assign» of the staff's list (<see cref="StaffQueue"/>), the oldest first, each a link to
/// the page of the training and the whole view a link to the list.
/// <para>Always live and with no property, because it is the reader's. A training counts when the one handler says the reader may take
/// the step on it — <c>Training.Approve</c> for a request, <c>Training.Assign</c> for a training to assign —, which is never on a
/// training of their own (§3), and for a head of a FIR only on one of their FIR (A11b): the chief and the assistant chief of a FIR
/// assign the trainings of their FIR and approve none, so they see the trainings to assign of their FIR and nothing to approve. A
/// visitor gets <c>signedIn: false</c>.</para>
/// </summary>
public sealed class ApprovalQueueProvider(
    TrainingDbContext database,
    StaffTrainings staff,
    ICurrentUser currentUser,
    IOptions<DivisionOptions> division,
    IClock clock) : IDataBlockProvider
{
    public const string BlockType = "training.approvalQueue";

    /// <summary>How many of a queue a dashboard shows: the whole of it is the list's.</summary>
    public const int MaxItems = 10;

    /// <summary>How many waiting trainings are read before the permissions narrow them: a dashboard is not the list.</summary>
    private const int Window = 500;

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return BlockAnswers.SignedOut();
        }

        var heldBefore = StaffQueue.HeldBefore(clock.UtcNow, division.Value.ResolveTimeZone());

        return BlockAnswers.SignedIn(new ApprovalQueueDto(
            await QueueAsync(StaffQueue.ToApprove, TrainingPermissions.Approve, heldBefore, cancellationToken),
            await QueueAsync(StaffQueue.ToAssign, TrainingPermissions.Assign, heldBefore, cancellationToken)));
    }

    /// <summary>The trainings of one view the reader may take its step on, the oldest request first.</summary>
    private async Task<TrainingQueueDto> QueueAsync(string view, string permission, DateTime heldBefore, CancellationToken cancellationToken)
    {
        if (!currentUser.HasAny(permission))
        {
            return new TrainingQueueDto(0, []);
        }

        var waiting = await StaffQueue.Narrow(CrudSource.BackOffice<Training>(database).AsNoTracking(), view, heldBefore)!
            .OrderBy(training => training.CreatedAt)
            .ThenBy(training => training.Id)
            .Take(Window)
            .ToListAsync(cancellationToken);

        var theirs = new List<Training>(waiting.Count);
        foreach (var training in waiting)
        {
            if (await staff.MayAsync(training, permission))
            {
                theirs.Add(training);
            }
        }

        return new TrainingQueueDto(theirs.Count, await staff.RowsAsync([.. theirs.Take(MaxItems)], cancellationToken));
    }
}
