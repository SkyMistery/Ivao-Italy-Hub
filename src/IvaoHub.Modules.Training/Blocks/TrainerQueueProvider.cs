using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Settings;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training.Blocks;

/// <summary>
/// The block <c>training.trainerQueue</c> (design M3 §4.3, §2.5): the reader's trainings to move, as their trainer — the dates to
/// propose, the trainings whose trainee has let the dates wait longer than <c>responseReminderDays</c>, with how many days, and the
/// reports to write —, each a link to the page of the training where the step is taken. The three parts are
/// <see cref="TrainerQueue"/>'s.
/// <para>Always live and with no property, because it is the reader's. A training counts when the one handler says the reader may
/// conduct it — the trainer on the scope of that training alone (§3.3), never on a training of their own —, the question the page of
/// the training asks before it offers a step. A visitor gets <c>signedIn: false</c>.</para>
/// </summary>
public sealed class TrainerQueueProvider(
    TrainingDbContext database,
    StaffTrainings staff,
    ModuleSettingsStore settingsStore,
    ICurrentUser currentUser,
    IClock clock) : IDataBlockProvider
{
    public const string BlockType = "training.trainerQueue";

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return BlockAnswers.SignedOut();
        }

        var assigned = await TrainerQueue.Theirs(CrudSource.BackOffice<Training>(database).AsNoTracking(), currentUser.Vid)
            .ToListAsync(cancellationToken);

        var theirs = new List<Training>(assigned.Count);
        foreach (var training in assigned)
        {
            if (await staff.MayAsync(training, TrainingPermissions.Conduct))
            {
                theirs.Add(training);
            }
        }

        var ids = theirs.Select(training => training.Id).ToList();
        List<TrainingSlot> slots = ids.Count == 0
            ? []
            : await database.Slots.AsNoTracking().Where(slot => ids.Contains(slot.TrainingId)).ToListAsync(cancellationToken);
        var settings = await settingsStore.GetAsync<TrainingSettings>(TrainingModule.ModuleKey, cancellationToken);
        var parts = TrainerQueue.Split(theirs, slots, clock.UtcNow, settings.ResponseReminderDays);

        // The rows as the staff's list shows them, with the names of the trainees: one query for the block.
        var rows = (await staff.RowsAsync(theirs, cancellationToken)).ToDictionary(row => row.Id);

        return BlockAnswers.SignedIn(new TrainerQueueDto(
            [.. parts.ToPropose.Select(training => rows[training.Id])],
            [.. parts.Waiting.Select(waiting => new WaitingTrainingDto(rows[waiting.Training.Id], waiting.Days))],
            [.. parts.ToReport.Select(training => rows[training.Id])]));
    }
}
