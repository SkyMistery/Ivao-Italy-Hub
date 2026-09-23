using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>
/// Withdraws the reports left «to modify» longer than their tour's report window (design M2 §3.1): the pilot did not
/// correct it, so the leg is theirs to fly again and the session is free. Once a day; the step is written in the report's
/// history as the module's own (VID 0).
/// <para>It runs as the application, outside any request, so the interceptor's guard leaves it alone as it leaves every
/// job; the rows it writes are the ones a pilot's withdrawal writes (<see cref="PirepSubmission.Withdraw"/>).</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class PirepWithdrawalJob(
    FlightOpsDbContext database,
    HubDbContext hub,
    PirepSubmission submission,
    IClock clock,
    ILogger<PirepWithdrawalJob> logger) : IJob
{
    public const string JobName = "flightops-pirep-withdrawal";

    /// <summary>Every day at 03:20 UTC, outside the evening's traffic.</summary>
    public const string Cron = "0 20 3 * * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many reports were withdrawn on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var now = entry.StartedAt;
            var waiting = await CrudSource.BackOffice<Pirep>(database)
                .Include(report => report.Flights)
                .Where(report => report.Status == PirepStatus.ToModify)
                .ToListAsync(cancellationToken);

            var ids = waiting.Select(report => report.Id).ToArray();
            var tourIds = waiting.Select(report => report.TourId).Distinct().ToArray();

            // Since when each one has waited: its last step into «to modify».
            var since = await database.PirepEvents.AsNoTracking()
                .Where(step => ids.Contains(step.PirepId) && step.ToStatus == PirepStatus.ToModify)
                .GroupBy(step => step.PirepId)
                .Select(group => new { PirepId = group.Key, At = group.Max(step => step.At) })
                .ToDictionaryAsync(row => row.PirepId, row => row.At, cancellationToken);
            var windows = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
                .Where(tour => tourIds.Contains(tour.Id))
                .ToDictionaryAsync(tour => tour.Id, tour => tour.ReportWindowDays, cancellationToken);

            var expired = waiting
                .Where(report => (since.TryGetValue(report.Id, out var at) ? at : report.UpdatedAt)
                    .AddDays(windows.GetValueOrDefault(report.TourId)) < now)
                .ToList();

            foreach (var report in expired)
            {
                submission.Withdraw(report, byVid: 0, now, "flightops:events.withdrawnAutomatically");
            }

            await database.SaveChangesAsync(cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{expired.Count} report(s) withdrawn after waiting for a correction");
            await hub.SaveChangesAsync(cancellationToken);

            return expired.Count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The report withdrawal job failed.");

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

/// <summary>Whether a tour and its legs have reports (design M2 §1.2.2, §1.4.1): any report, in any state, is one.</summary>
internal sealed class PirepTourReports(FlightOpsDbContext database) : ITourReports
{
    public Task<bool> AnyAsync(long tourId, CancellationToken cancellationToken = default) =>
        CrudSource.BackOffice<Pirep>(database).AnyAsync(report => report.TourId == tourId, cancellationToken);

    public async Task<IReadOnlySet<long>> LegsWithReportsAsync(long tourId, CancellationToken cancellationToken = default) =>
        (await CrudSource.BackOffice<Pirep>(database)
            .Where(report => report.TourId == tourId && report.LegId != null)
            .Select(report => report.LegId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();
}
