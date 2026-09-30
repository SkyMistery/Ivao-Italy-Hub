using System.Globalization;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Core.Awards;

/// <summary>
/// Once a day, at the division's <see cref="DivisionOptions.AwardDigestTime"/>, whoever may assign an award hears about the signals
/// that entered the queue since the last mail (M4, E10d, note 2026-09-30-la-mail-a-chi-assegna-gli-award, decided by Carmine): how
/// many, how many wait in all, and one line per reason; nothing on a day with no new signal. The queue is read here,
/// in the core, so a signal is told whichever module wrote it — the tours' and the events' alike — and no module asks for it.
/// <para>Told once: the signal remembers when (<see cref="AwardSignal.NotifiedAt"/>), marked in the same save as the mail's rows,
/// so a run that is late or runs twice tells nothing twice. A signal handled or dismissed before the run is told nothing.</para>
/// <para>Who assigns is the core's answer (<see cref="IPermissionHolders"/>), the one a login computes; whether each of them wants
/// it, and has an address, is the notification service's question.</para>
/// <para>It never throws, like the other jobs: a failure is a row in <c>hub_jobs_log</c>.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class AwardQueueMailJob(
    HubDbContext database,
    IPermissionHolders holders,
    INotificationService notifications,
    IOptions<DivisionOptions> division,
    IClock clock,
    ILogger<AwardQueueMailJob> logger) : IJob
{
    /// <summary>Name under which the runs are recorded.</summary>
    public const string JobName = "award-queue-mail";

    /// <summary>
    /// Every day at that time, in the time zone the trigger is given: the division's own, at its
    /// <see cref="DivisionOptions.AwardDigestTime"/> — a setting of the division, never a schedule written here (Carmine).
    /// </summary>
    public static string CronAt(TimeOnly time) =>
        string.Create(CultureInfo.InvariantCulture, $"0 {time.Minute} {time.Hour} * * ?");

    /// <summary>Longest failure message kept on the log row; the log file has the rest.</summary>
    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many signals were told on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        database.JobsLog.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        try
        {
            var (told, queued) = await TellAsync(cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{told} new signal(s), {queued} mail(s) queued");
            await database.SaveChangesAsync(cancellationToken);

            return told;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The award queue mail job failed.");

            database.ChangeTracker.Clear();
            database.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength
                ? exception.Message
                : exception.Message[..MaxMessageLength];
            await database.SaveChangesAsync(cancellationToken);

            return 0;
        }
    }

    /// <summary>
    /// The words of the signals, one line per reason and proposed award with how many signals share them, in the order the first
    /// of each entered the queue: <c>- Completed the tour "Giro" (Giro award): 3</c>. No VID: the queue shows who, and a mail that
    /// named people would be one more place for an erasure to look. The reason is written in the division's language by the module
    /// (<c>TourCompletion</c>), and the award's name is taken in the same one.
    /// </summary>
    public static string Lines(
        IReadOnlyList<AwardSignal> signals,
        IReadOnlyDictionary<long, Localized<string>> awards,
        string locale)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(awards);

        return string.Join('\n', signals
            .OrderBy(signal => signal.Id)
            .GroupBy(signal => (signal.Reason, signal.AwardId))
            .Select(group =>
            {
                var award = group.Key.AwardId is { } id && awards.TryGetValue(id, out var name)
                    ? name.Resolve(locale, locale)
                    : null;

                return string.IsNullOrWhiteSpace(award)
                    ? string.Create(CultureInfo.InvariantCulture, $"- {group.Key.Reason}: {group.Count()}")
                    : string.Create(CultureInfo.InvariantCulture, $"- {group.Key.Reason} ({award}): {group.Count()}");
            }));
    }

    private async Task<(int Told, int Queued)> TellAsync(CancellationToken cancellationToken)
    {
        // Waiting, and nobody told yet. One handled or dismissed before this run is nobody's business any more.
        var fresh = await database.AwardSignals
            .Where(signal => signal.Status == AwardSignalStatus.Pending && signal.NotifiedAt == null)
            .OrderBy(signal => signal.Id)
            .ToListAsync(cancellationToken);

        if (fresh.Count == 0)
        {
            return (0, 0);
        }

        var settings = division.Value;
        var waiting = await database.AwardSignals.CountAsync(signal => signal.Status == AwardSignalStatus.Pending, cancellationToken);

        long[] awardIds = [.. fresh.Select(signal => signal.AwardId).OfType<long>().Distinct()];
        var awards = awardIds.Length == 0
            ? []
            : await database.Awards
                .AsNoTracking()
                .Where(award => awardIds.Contains(award.Id))
                .ToDictionaryAsync(award => award.Id, award => award.Name, cancellationToken);

        var assigners = await holders.HoldersOfAsync(CorePermissions.AwardsAssign, cancellationToken);

        var now = clock.UtcNow;
        foreach (var signal in fresh)
        {
            signal.NotifiedAt = now;
        }

        // The service saves on this same context, the scope's, which carries the marks: the mail and the marks are one save, or
        // neither is.
        var queued = await notifications.QueueAsync(
            new NotificationIntent(
                NotificationTypes.AwardToAssign,
                [.. assigners.Select(holder => NotificationRecipient.Member(holder.Vid))],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["count"] = fresh.Count.ToString(CultureInfo.InvariantCulture),
                    ["waiting"] = waiting.ToString(CultureInfo.InvariantCulture),
                    ["signals"] = Lines(fresh, awards, settings.DefaultLocale),
                    ["url"] = $"https://{settings.Domain}/staff/awards/queue",
                }),
            cancellationToken);

        // With nobody queued — everybody switched it off, or nobody has an address — the service saved nothing, and the marks go
        // alone: the signals were told, and telling them again tomorrow would reach nobody either.
        await database.SaveChangesAsync(cancellationToken);

        return (fresh.Count, queued);
    }
}
