using System.Globalization;
using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Core.Notifications;

/// <summary>
/// Empties the queue of <c>hub_notifications</c>, one minute at a time. This is where the
/// asynchrony of the design earns its place: a mail that does not go out must not make the save
/// that caused it fail, so the row is written first and sent afterwards (design M1 section 5.2).
/// <para>It is not an event bus. Nothing subscribes, nothing is dispatched by type, and the only
/// thing that ever reads this table is this class.</para>
/// <para>It never throws, exactly like <c>RefDataSyncJob</c>: a failure is a row in
/// <c>hub_jobs_log</c> and an attempt counted on the notification, because a mail server that is
/// down must not take a site with it.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class NotificationDispatchJob(
    HubDbContext database,
    IMailSender mail,
    LocaleCatalog catalog,
    IOptions<SmtpOptions> smtp,
    IClock clock,
    ILogger<NotificationDispatchJob> logger) : IJob
{
    /// <summary>Name under which the runs are recorded.</summary>
    public const string JobName = "notification-dispatch";

    /// <summary>
    /// How many times one notification is tried before it is given up on. Three attempts a minute
    /// apart cover a mail server restarting; a fourth would only make the same mistake again.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>How many are taken per run, so one run cannot hold the connection all day.</summary>
    private const int BatchSize = 50;

    /// <summary>Longest failure message kept on the row; the log file has the rest.</summary>
    private const int MaxErrorLength = 512;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many went out and how many failed on this run.</summary>
    public async Task<(int Sent, int Failed)> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        database.JobsLog.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        if (!smtp.Value.IsConfigured)
        {
            // Nothing is lost: the rows stay pending and go out on the first run after somebody
            // configures a server. Saying so once a minute in a table beats saying it in a stack
            // trace, and beats pretending the queue is empty.
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "skipped";
            entry.Message = "No SMTP server is configured; the queue was left alone.";
            await database.SaveChangesAsync(cancellationToken);
            return (0, 0);
        }

        var pending = await database.Notifications
            .Where(row => row.Status == NotificationStatus.Pending && row.Attempts < MaxAttempts)
            .OrderBy(row => row.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;
        var failed = 0;

        foreach (var notification in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            notification.Attempts++;

            try
            {
                await mail.SendAsync(Compose(notification), cancellationToken);

                notification.Status = NotificationStatus.Sent;
                notification.SentAt = clock.UtcNow;
                notification.LastError = null;
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                notification.LastError = Truncate(exception.Message);

                // Out of attempts is a decision, not an error to keep retrying: the row says
                // failed, holds why, and nothing looks at it again.
                if (notification.Attempts >= MaxAttempts)
                {
                    notification.Status = NotificationStatus.Failed;
                    logger.LogWarning(
                        exception,
                        "Notification {Id} of type {Type} was given up on after {Attempts} attempts.",
                        notification.Id,
                        notification.Type,
                        notification.Attempts);
                }
                else
                {
                    logger.LogInformation(
                        "Notification {Id} failed on attempt {Attempts}; it will be tried again.",
                        notification.Id,
                        notification.Attempts);
                }

                failed++;
            }
        }

        entry.FinishedAt = clock.UtcNow;
        entry.Status = failed == 0 ? "succeeded" : "partial";
        entry.Message = string.Create(CultureInfo.InvariantCulture, $"{sent} sent, {failed} failed");

        await database.SaveChangesAsync(cancellationToken);

        return (sent, failed);
    }

    /// <summary>
    /// The mail as it will be read: the template of its type, in the language stored on the row —
    /// the language of whoever receives it, decided when the intent was queued and not now.
    /// </summary>
    private OutgoingMail Compose(Notification notification)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(notification.DataJson)
            ?? [];

        return MailTemplate.Render(catalog, notification.Locale, notification.Type, notification.Address, data);
    }

    private static string Truncate(string message) =>
        message.Length <= MaxErrorLength ? message : message[..MaxErrorLength];
}
