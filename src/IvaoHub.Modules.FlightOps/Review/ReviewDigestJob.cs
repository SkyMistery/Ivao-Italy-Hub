using System.Globalization;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Modules.FlightOps.Review;

/// <summary>
/// The daily digest of the queue (design M2 §4.2.2): to everybody who may validate, the reports waiting on the tours
/// <b>they</b> may validate — how many per tour, and since when the oldest waits —, their own reports left out. Nobody with
/// nothing to do is written to, and a member switches it off from their profile (<c>flightops.reviewDigest</c>).
/// <para>Who may validate is the core's answer (<see cref="IPermissionHolders"/>), the same a login computes; which tours is
/// asked of each of them as the handler asks it, department and scope of the report.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class ReviewDigestJob(
    FlightOpsDbContext database,
    HubDbContext hub,
    IPermissionHolders holders,
    INotificationService notifications,
    IOptions<DivisionOptions> division,
    IClock clock,
    ILogger<ReviewDigestJob> logger) : IJob
{
    public const string JobName = "flightops-review-digest";

    /// <summary>Every day at 07:00 UTC, the morning in Europe.</summary>
    public const string Cron = "0 0 7 * * ?";

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
            var now = entry.StartedAt;

            // Waiting: in the queue, or taken by somebody whose lease has run out (§4.2).
            var waiting = await CrudSource.BackOffice<Pirep>(database).AsNoTracking()
                .Where(report => report.Status == PirepStatus.Queued
                    || (report.Status == PirepStatus.InReview && (report.LeaseUntil == null || report.LeaseUntil <= now)))
                .ToListAsync(cancellationToken);

            var sent = 0;
            if (waiting.Count > 0)
            {
                var tourIds = waiting.Select(report => report.TourId).Distinct().ToList();
                var titles = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
                    .Where(tour => tourIds.Contains(tour.Id))
                    .ToDictionaryAsync(tour => tour.Id, tour => tour.Title, cancellationToken);

                var validators = await holders.HoldersOfAsync(TourPermissions.Validate, cancellationToken);
                var vids = validators.Select(holder => holder.Vid).ToList();
                var locales = await hub.Users.AsNoTracking()
                    .Where(user => vids.Contains(user.Vid))
                    .ToDictionaryAsync(user => user.Vid, user => user.Locale, cancellationToken);

                foreach (var holder in validators)
                {
                    var theirs = waiting
                        .Where(report => report.Vid != holder.Vid
                            && ((IOwnedByDepartment)report).OwnerDepartments.Any(department =>
                                holder.Has(TourPermissions.Validate, department, report.ResourceScope)))
                        .GroupBy(report => report.TourId)
                        .Select(group => (TourId: group.Key, Count: group.Count(), Oldest: group.Min(report => report.QueuedAt)))
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
                        ["tours"] = string.Join('\n', theirs.Select(group =>
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"- {Text(titles.GetValueOrDefault(group.TourId), locale)}: {group.Count} ({group.Oldest:yyyy-MM-dd})"))),
                        ["url"] = $"https://{division.Value.Domain}/staff/tours/review",
                    };

                    sent += await notifications.QueueAsync(
                        new NotificationIntent(FlightOpsNotifications.ReviewDigest, [NotificationRecipient.Member(holder.Vid)], data),
                        cancellationToken);
                }
            }

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{waiting.Count} report(s) waiting, {sent} digest(s) queued");
            await hub.SaveChangesAsync(cancellationToken);

            return sent;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The review digest job failed.");

            hub.ChangeTracker.Clear();
            hub.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength ? exception.Message : exception.Message[..MaxMessageLength];
            await hub.SaveChangesAsync(cancellationToken);

            return 0;
        }
    }

    private string Text(Localized<string>? text, string locale) =>
        text?.Get(locale) ?? text?.Get(division.Value.DefaultLocale) ?? string.Empty;
}
