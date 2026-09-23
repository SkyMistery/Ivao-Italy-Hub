using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>
/// Deletes the tracks nobody needs any more (Carmine, 23 September 2026, note 2026-09-23-la-validazione §2.3):
/// <c>trackRetentionDays</c> after the decision of a report accepted or rejected and not disputed, or after it was withdrawn.
/// The report, its plans and its errors stay; a report reopened later is judged without the track, and the page says so.
/// <para>Once a day, through the interceptor like every write (no bulk statement), and the report is never written.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class TrackRetentionJob(
    FlightOpsDbContext database,
    HubDbContext hub,
    ModuleSettingsStore settingsStore,
    IClock clock,
    ILogger<TrackRetentionJob> logger) : IJob
{
    public const string JobName = "flightops-track-retention";

    /// <summary>Every day at 03:40 UTC, after the withdrawal job.</summary>
    public const string Cron = "0 40 3 * * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many tracks were deleted.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
            var before = entry.StartedAt.AddDays(-settings.TrackRetentionDays);

            var done = CrudSource.BackOffice<Pirep>(database)
                .Where(report =>
                    ((report.Status == PirepStatus.Accepted || report.Status == PirepStatus.Rejected)
                        && !report.IsDisputed
                        && (report.DisputeDecidedAt ?? report.DecidedAt) < before)
                    || (report.Status == PirepStatus.Withdrawn && report.UpdatedAt < before))
                .Select(report => report.Id);

            // The keys only: the blobs are what is being deleted, and there is no reason to read them first.
            var keys = await database.PirepTracks.AsNoTracking()
                .Where(track => database.PirepFlights
                    .Where(flight => done.Contains(flight.PirepId))
                    .Select(flight => flight.Id)
                    .Contains(track.PirepFlightId))
                .Select(track => track.PirepFlightId)
                .ToListAsync(cancellationToken);

            database.PirepTracks.RemoveRange(keys.Select(key => new PirepTrack { PirepFlightId = key }));
            await database.SaveChangesAsync(cancellationToken);
            var deleted = keys.Count;

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{deleted} track(s) deleted");
            await hub.SaveChangesAsync(cancellationToken);

            return deleted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The track retention job failed.");

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
