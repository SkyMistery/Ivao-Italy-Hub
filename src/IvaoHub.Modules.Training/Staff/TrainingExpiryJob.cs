using System.Globalization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>
/// The night of the training (design M3 §5.3), its first half (A7): takes back the grants the assignment wrote on trainings that
/// are over — reported, closed, not attended —, because a grant with a scope travels in its holder's cookie and must not pile up
/// (§3.3). Its holder is asked to sign in again, at night rather than when the report is just published: the cost decided with
/// the grant (§12 n.1). The closing of the trainings nobody dated in time is its second half, with the dates (A8).
/// <para>A grant is also taken back when its training has another trainer, or none: the assignment writes the new grant before
/// the training and takes the old one after it, and a write that stopped half way leaves one behind. Not in the hour after it
/// was written, which may be an assignment still under way.</para>
/// <para>Past the filter of the members, as the staff reads: the job is nobody. It never throws: a failure is a row in
/// <c>hub_jobs_log</c>, as for every job of the hub.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class TrainingExpiryJob(
    TrainingDbContext database,
    HubDbContext hub,
    ModuleGrants grants,
    IClock clock,
    ILogger<TrainingExpiryJob> logger) : IJob
{
    public const string JobName = "training-expiry";

    /// <summary>
    /// 04:15 in the division's time zone: after the reference data, the review of the documents and the files that expired, before
    /// anybody is up.
    /// </summary>
    public const string Cron = "0 15 4 * * ?";

    /// <summary>How long a grant of a training that has another trainer is left alone: an assignment may still be writing it.</summary>
    private static readonly TimeSpan InFlight = TimeSpan.FromHours(1);

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many grants were taken back on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var taken = await TakeBackAsync(entry.StartedAt, cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{taken} grant(s) of trainings over or reassigned taken back");
            await hub.SaveChangesAsync(cancellationToken);

            return taken;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The training expiry job failed.");

            hub.ChangeTracker.Clear();
            hub.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength ? exception.Message : exception.Message[..MaxMessageLength];
            await hub.SaveChangesAsync(cancellationToken);

            return 0;
        }
    }

    /// <summary>
    /// Every grant of <c>Training.Conduct</c> on one training, on any department — a division may move the module's —, taken back
    /// unless its training is open and its holder is still the trainer.
    /// </summary>
    private async Task<int> TakeBackAsync(DateTime now, CancellationToken cancellationToken)
    {
        var held = new List<UserGrant>();
        foreach (var department in Enum.GetValues<Department>())
        {
            held.AddRange((await grants.HeldAsync(TrainingPermissions.Conduct, department, cancellationToken))
                .Where(grant => Training.IdOf(grant.ResourceScope) is not null));
        }

        if (held.Count == 0)
        {
            return 0;
        }

        var ids = held.Select(grant => Training.IdOf(grant.ResourceScope)!.Value).Distinct().ToList();
        var trainings = await CrudSource.BackOffice<Training>(database).AsNoTracking()
            .Where(training => ids.Contains(training.Id))
            .Select(training => new { training.Id, training.State, training.TrainerVid })
            .ToDictionaryAsync(training => training.Id, cancellationToken);

        var taken = 0;
        foreach (var grant in held)
        {
            var training = trainings.GetValueOrDefault(Training.IdOf(grant.ResourceScope)!.Value);
            var over = training is null || !Training.IsOpen(training.State);
            var reassigned = !over && training!.TrainerVid != grant.Vid && grant.CreatedAt <= now - InFlight;

            if ((over || reassigned)
                && await grants.TakeAsync(grant.Vid!.Value, grant.Value, grant.Department!.Value, grant.ResourceScope, cancellationToken))
            {
                taken++;
            }
        }

        return taken;
    }
}
