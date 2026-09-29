using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Dates;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>
/// The night of the training (design M3 §5.3): it closes the trainings whose trainee chose no date in the time the division gives —
/// only when <c>maxResponseDays</c> is set (§2.5, §12 n.9; A8, <see cref="TrainingDates.CloseUnansweredAsync"/>).
/// <para>It takes no grant back: the trainer conducts the trainings assigned to them with the <c>Training.Conduct</c> of their
/// position, and no grant is written on one training (§3.3, A7b). Until A7b this job also took back the grants the assignment of
/// A7 wrote.</para>
/// <para>It never throws: a failure is a row in <c>hub_jobs_log</c>, as for every job of the hub.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class TrainingExpiryJob(
    HubDbContext hub,
    TrainingDates dates,
    IClock clock,
    ILogger<TrainingExpiryJob> logger) : IJob
{
    public const string JobName = "training-expiry";

    /// <summary>
    /// 04:15 in the division's time zone: after the reference data, the review of the documents and the files that expired, before
    /// anybody is up.
    /// </summary>
    public const string Cron = "0 15 4 * * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many trainings were closed on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var closed = await dates.CloseUnansweredAsync(entry.StartedAt, cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{closed} training(s) with no date chosen in time closed");
            await hub.SaveChangesAsync(cancellationToken);

            return closed;
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
}
