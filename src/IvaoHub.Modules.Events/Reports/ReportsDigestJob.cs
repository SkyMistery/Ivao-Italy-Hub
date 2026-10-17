using System.Globalization;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Modules.Events.Reports;

/// <summary>
/// <c>events-digest</c> (design M4 §5.3, §8.3, §8.4, E14b; note 2026-10-17-le-regole-di-award): once a day, to everybody who may validate a
/// report of support, the reports waiting on the events <b>they</b> may validate — how many per event, and since when the oldest waits —, their
/// own left out (<c>EventReports.Edit</c> is denied to the report's member). Nobody with nothing to do is written to, and a member switches it
/// off from their profile (<see cref="EventsNotifications.ReportsToValidate"/>). The digest of the tours' validators (M2) is its pattern.
/// <para>Who may validate is the core's answer (<see cref="IPermissionHolders"/>); which events is asked of each of them as the handler asks it,
/// department and scope of the report.</para>
/// <para><b>«Already sent today» is the run's row</b> (note 2026-10-09-i-job-che-recuperano, E10j): a run that finds a run of its own that
/// succeeded on the same day of the division sends nothing, and says so in its row. Every digest of a run and the run's end are one commit: a
/// run stopped half way queues nobody, and the next sends every digest once.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class ReportsDigestJob(
    EventsDbContext database,
    HubDbContext hub,
    IPermissionHolders holders,
    INotificationService notifications,
    IOptions<DivisionOptions> division,
    IClock clock,
    ILogger<ReportsDigestJob> logger) : IJob
{
    public const string JobName = "events-digest";

    /// <summary>Its trigger, by name: a test reads its zone and its cron.</summary>
    public const string TriggerName = $"{JobName}-daily";

    /// <summary>Every day at 07:15 in the division's zone, the morning of whoever reads it, after the tours' digest of 07:00.</summary>
    public const string Cron = "0 15 7 * * ?";

    /// <summary>What the run's row says when a run of the same day already sent the digest.</summary>
    public const string AlreadySent = "already sent today";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many validators were written to.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            // The core's mail service saves on this same context, the scope's: inside the transaction, so the digests and the run's end are
            // one commit, or none is.
            await using var transaction = await hub.Database.BeginTransactionAsync(cancellationToken);

            var sent = 0;
            var waiting = 0;
            if (await SentTodayAsync(entry, cancellationToken))
            {
                entry.Message = AlreadySent;
            }
            else
            {
                (waiting, sent) = await SendAsync(cancellationToken);
                entry.Message = string.Create(CultureInfo.InvariantCulture, $"{waiting} report(s) waiting, {sent} digest(s) queued");
            }

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            await hub.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return sent;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The digest of the reports of support failed.");

            // The transaction is gone with the try block, rolled back with every digest of the run; the failure is written alone.
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
    /// Whether a run of this job succeeded earlier on the same day of the division as <paramref name="entry"/> — the day the trigger counts in,
    /// so that a digest of 07:15 and a run made up at 23:00 are the same day, and one after midnight is the next.
    /// </summary>
    private async Task<bool> SentTodayAsync(JobLogEntry entry, CancellationToken cancellationToken)
    {
        var zone = division.Value.ResolveTimeZone();
        var local = TimeZoneInfo.ConvertTimeFromUtc(entry.StartedAt, zone);
        var midnight = TimeZoneInfo.ConvertTimeToUtc(local.Date, zone);

        return await hub.JobsLog.AsNoTracking().AnyAsync(
            run => run.Job == JobName
                && run.Id != entry.Id
                && run.Status == "succeeded"
                && run.FinishedAt != null
                && run.StartedAt >= midnight
                && run.StartedAt <= entry.StartedAt,
            cancellationToken);
    }

    /// <summary>The digests of the reports waiting: how many there are, and how many validators were written to.</summary>
    private async Task<(int Waiting, int Sent)> SendAsync(CancellationToken cancellationToken)
    {
        var waiting = await CrudSource.BackOffice<EventReport>(database).AsNoTracking()
            .Where(report => report.State == ReportState.Submitted)
            .ToListAsync(cancellationToken);
        if (waiting.Count == 0)
        {
            return (0, 0);
        }

        var eventIds = waiting.Select(report => report.EventId).Distinct().ToList();
        var titles = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.Title, cancellationToken);

        var validators = await holders.HoldersOfAsync(EventsPermissions.ReportsEdit, cancellationToken);
        var vids = validators.Select(holder => holder.Vid).ToList();
        var locales = await hub.Users.AsNoTracking()
            .Where(user => vids.Contains(user.Vid))
            .ToDictionaryAsync(user => user.Vid, user => user.Locale, cancellationToken);

        var sent = 0;
        foreach (var holder in validators.Where(holder => holder.Vid > 0))
        {
            var theirs = waiting
                .Where(report => report.Vid != holder.Vid
                    && ((IOwnedByDepartment)report).OwnerDepartments.Any(department =>
                        holder.Has(EventsPermissions.ReportsEdit, department, report.ResourceScope)))
                .GroupBy(report => report.EventId)
                .Select(group => (EventId: group.Key, Count: group.Count(), Oldest: group.Min(report => report.SubmittedAt)))
                .OrderBy(group => group.Oldest)
                .ToList();
            if (theirs.Count == 0)
            {
                continue;
            }

            var locale = locales.GetValueOrDefault(holder.Vid) ?? division.Value.DefaultLocale;
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = theirs.Sum(group => group.Count).ToString(CultureInfo.InvariantCulture),
                ["events"] = string.Join('\n', theirs.Select(group =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"- {Text(titles.GetValueOrDefault(group.EventId), locale)}: {group.Count} ({group.Oldest:yyyy-MM-dd}) https://{division.Value.Domain}/staff/events/{group.EventId}?tab=reports"))),
            };

            sent += await notifications.QueueAsync(
                new NotificationIntent(EventsNotifications.ReportsToValidate, [NotificationRecipient.Member(holder.Vid)], data),
                cancellationToken);
        }

        return (waiting.Count, sent);
    }

    private string Text(Localized<string>? text, string locale) =>
        text?.Get(locale) ?? text?.Get(division.Value.DefaultLocale) ?? string.Empty;
}
