using System.Globalization;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>
/// Projects again the tours released since its last run (Carmine, 16 September 2026). A ready tour without a preview is
/// the staff's until its release and everybody's after, and nothing writes it at that moment: without this, search and
/// calendar would keep it for the staff for good.
/// <para>It publishes nothing and changes no tour — the state is still read off the dates (design M2 §1.2.1). It only
/// brings the mirror up to date, through the interceptor, with <see cref="ProjectionRefresh"/>: no audit row, no new
/// row version under an editor that is open.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class TourReleaseJob(
    FlightOpsDbContext database,
    HubDbContext hub,
    ProjectionRefresh projections,
    IClock clock,
    ILogger<TourReleaseJob> logger) : IJob
{
    public const string JobName = "flightops-tour-release";

    /// <summary>Every quarter of an hour: a tour appears in search at most fifteen minutes after its release.</summary>
    public const string Cron = "0 0/15 * * * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many tours were projected again on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // Since the start of the last run that succeeded: a release during a run that failed is caught by the next.
        var since = await hub.JobsLog.AsNoTracking()
            .Where(run => run.Job == JobName && run.Status == "succeeded")
            .MaxAsync(run => (DateTime?)run.StartedAt, cancellationToken) ?? DateTime.MinValue;

        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var now = entry.StartedAt;
            var released = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
                .Where(tour => tour.Status == PublishStatus.Published
                    && !tour.IsTemplate
                    && !tour.IsHidden
                    && !tour.ShowPreview
                    && tour.ReleaseAt > since
                    && tour.ReleaseAt <= now)
                .ToListAsync(cancellationToken);

            await projections.RefreshAsync(database, released, cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{released.Count} released tour(s) projected again");
            await hub.SaveChangesAsync(cancellationToken);

            return released.Count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The tour release job failed.");

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
