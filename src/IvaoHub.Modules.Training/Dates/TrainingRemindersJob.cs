using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.Training.Dates;

/// <summary>
/// The reminder of a session (design M3 §5.3, R.7): <c>reminderLeadHours</c> before it — 24 by default —, a mail to the trainee
/// and to the trainer, once. The core queues mails and does not schedule them, so the module looks every quarter of an hour for
/// the sessions about to start that were not reminded yet, and <see cref="Training.RemindedAt"/> makes it once: a date that
/// changes clears it, and the new date is reminded in its turn.
/// <para>The mark goes before the mails: a mail that fails to be queued is a reminder lost, never one sent twice. A training whose
/// date moved while the job looked is left for the next run. Past the filter of the members, as the staff reads: the job is
/// nobody. It never throws: a failure is a row in <c>hub_jobs_log</c>, as for every job of the hub.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class TrainingRemindersJob(
    TrainingDbContext database,
    HubDbContext hub,
    ModuleSettingsStore settingsStore,
    TrainingMail mail,
    IClock clock,
    ILogger<TrainingRemindersJob> logger) : IJob
{
    public const string JobName = "training-reminders";

    /// <summary>
    /// Every quarter of an hour, five minutes past, off the quarter the tours' own job runs on: a reminder leaves at most a quarter
    /// of an hour after its moment. The same in every time zone.
    /// </summary>
    public const string Cron = "0 5/15 * * * ?";

    private const int MaxMessageLength = 2000;

    /// <summary>
    /// The sessions to remind now: dated, starting after <paramref name="now"/> and no later than <paramref name="until"/>, not
    /// reminded yet. A session already started is not reminded any more.
    /// </summary>
    public static IQueryable<Training> Due(IQueryable<Training> trainings, DateTime now, DateTime until)
    {
        ArgumentNullException.ThrowIfNull(trainings);

        return trainings.Where(training => training.State == TrainingState.Scheduled
            && training.RemindedAt == null
            && training.ScheduledStartUtc > now
            && training.ScheduledStartUtc <= until);
    }

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many sessions were reminded on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var now = entry.StartedAt;
            var settings = await settingsStore.GetAsync<TrainingSettings>(TrainingModule.ModuleKey, cancellationToken);
            var until = now.AddHours(settings.ReminderLeadHours);

            var ids = await Due(CrudSource.BackOffice<Training>(database).AsNoTracking(), now, until)
                .Select(training => training.Id)
                .ToListAsync(cancellationToken);

            var reminded = 0;
            foreach (var id in ids)
            {
                var training = await Due(CrudSource.BackOffice<Training>(database), now, until)
                    .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
                if (training is null)
                {
                    continue;
                }

                // The job's bookkeeping alone: not a change of the training, so nothing is audited and nothing moves.
                training.RemindedAt = now;

                try
                {
                    await database.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    database.ChangeTracker.Clear();
                    continue;
                }

                await mail.SessionAsync(TrainingNotifications.Reminder, training, cancellationToken);
                reminded++;
            }

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{reminded} session(s) reminded");
            await hub.SaveChangesAsync(cancellationToken);

            return reminded;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The training reminders job failed.");

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
