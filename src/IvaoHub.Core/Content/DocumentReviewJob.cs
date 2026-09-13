using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Core.Content;

/// <summary>
/// Once a day, the operational documents whose review date has passed are pointed out to the
/// department that keeps them (G14, the one improvement over va.ivao.aero the decision note asked
/// for: "the review date does something"). Told once — the row remembers when — and never about
/// a document already retired: there is nothing to review in what is no longer in force.
/// <para>An intent per document, through the one notification service; the recipients are the
/// department's shared inbox and its staff, exactly as a contact message is addressed. Whether
/// each of them wants it is the service's question.</para>
/// <para>The mark goes through the change tracker like every other write — the architecture test
/// allows no bulk update past the interceptor — so it is a line in the audit log and a bump of the
/// row version. ⚠️ The price of that: somebody editing the document at half past three in the
/// morning is told their next save conflicts. Once per document, at night; accepted in G14.</para>
/// <para>It never throws, like the other two jobs: a failure is a row in <c>hub_jobs_log</c>.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class DocumentReviewJob(
    HubDbContext database,
    INotificationService notifications,
    IOptions<DivisionOptions> division,
    IClock clock,
    ILogger<DocumentReviewJob> logger) : IJob
{
    /// <summary>Name under which the runs are recorded.</summary>
    public const string JobName = "document-review";

    /// <summary>Longest failure message kept on the log row; the log file has the rest.</summary>
    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many documents were pointed out on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        database.JobsLog.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        try
        {
            var told = await TellAsync(cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{told} document(s) due for review");
            await database.SaveChangesAsync(cancellationToken);

            return told;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The document review job failed.");

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

    private async Task<int> TellAsync(CancellationToken cancellationToken)
    {
        var today = clock.UtcNow.Date;
        var settings = division.Value;

        // Past the query filter on purpose: the job is nobody, and a draft nobody may see is still
        // a document somebody has to review.
        var due = await CrudSource.BackOffice<ContentEntry>(database)
            .Where(content => content.Kind == ContentKind.Document
                && !content.IsTemplate
                && content.ReviewOn != null
                && content.ReviewOn <= today
                && content.ReviewNotifiedAt == null
                && content.RetiredAt == null)
            .OrderBy(content => content.Id)
            .ToListAsync(cancellationToken);

        var told = 0;

        foreach (var document in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var department = document.OwnerDepartment.ToString();
            var recipients = new List<NotificationRecipient>();

            if (settings.DepartmentMailboxes.TryGetValue(department, out var mailbox) && !string.IsNullOrWhiteSpace(mailbox))
            {
                recipients.Add(NotificationRecipient.Mailbox(mailbox));
            }

            var staff = await database.UserStaffPositions
                .AsNoTracking()
                .Where(position => position.Department == document.OwnerDepartment)
                .Select(position => position.Vid)
                .Distinct()
                .ToListAsync(cancellationToken);

            recipients.AddRange(staff.Select(NotificationRecipient.Member));

            await notifications.QueueAsync(
                new NotificationIntent(
                    NotificationTypes.DocumentReviewDue,
                    recipients,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["department"] = department,
                        // The title in the division's own language: one mail per document, not
                        // one per language, and the address bar is written in that language too.
                        ["title"] = document.Title.Get(settings.DefaultLocale) ?? document.Slug,
                        ["reviewOn"] = document.ReviewOn!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["url"] = $"https://{settings.Domain}/staff/{department.ToLowerInvariant()}/documents/{document.Id}",
                    }),
                cancellationToken);

            // Marked even when nobody was queued — everybody switched off, or no address on file:
            // the reminder was given, and giving it again tomorrow would not reach anybody either.
            document.ReviewNotifiedAt = clock.UtcNow;
            await database.SaveChangesAsync(cancellationToken);

            told++;
        }

        return told;
    }
}
