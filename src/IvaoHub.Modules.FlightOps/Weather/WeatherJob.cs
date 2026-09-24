using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using IvaoHub.Core.Weather;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.FlightOps.Weather;

/// <summary>
/// Every half hour (confirmed 15 September 2026, design M2 §1.13 point 1): the current METARs and TAFs of the airports of
/// the legs of the tours open or closing, saved without doubles. The core decides how to ask — one file for many METARs,
/// batches for the TAFs, the fallbacks for a METAR nobody else had — and the module only says which airports.
/// </summary>
[DisallowConcurrentExecution]
public sealed class WeatherJob(
    WeatherArchive archive,
    IWeatherSource weather,
    HubDbContext hub,
    IClock clock,
    ILogger<WeatherJob> logger) : IJob
{
    public const string JobName = "flightops-weather";

    /// <summary>At minute 5 and 35: METARs are issued around the hour and the half hour, and reach the sources minutes later.</summary>
    public const string Cron = "0 5/30 * * * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many bulletins were new.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var airports = await archive.WatchedAirportsAsync(entry.StartedAt, cancellationToken);
            var reports = airports.Count == 0 ? [] : await weather.GetCurrentAsync(airports, cancellationToken);
            var saved = await archive.SaveAsync(reports, clock.UtcNow, cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(
                CultureInfo.InvariantCulture,
                $"{airports.Count} airport(s) watched, {reports.Count} bulletin(s) read, {saved} new");
            await hub.SaveChangesAsync(cancellationToken);

            return saved;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The weather job failed.");

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

/// <summary>
/// Once a day (design M2 §1.13, Carmine's rule): deletes the bulletins older than the longest report window of the tours
/// taking reports, unless a report still undecided flew that day at that airport (<see cref="WeatherArchive.DeleteExpiredAsync"/>).
/// </summary>
[DisallowConcurrentExecution]
public sealed class WeatherRetentionJob(
    WeatherArchive archive,
    HubDbContext hub,
    IClock clock,
    ILogger<WeatherRetentionJob> logger) : IJob
{
    public const string JobName = "flightops-weather-retention";

    /// <summary>Every day at 03:50 UTC, after the tracks.</summary>
    public const string Cron = "0 50 3 * * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many bulletins were deleted.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var deleted = await archive.DeleteExpiredAsync(entry.StartedAt, cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{deleted} bulletin(s) deleted");
            await hub.SaveChangesAsync(cancellationToken);

            return deleted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The weather retention job failed.");

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
