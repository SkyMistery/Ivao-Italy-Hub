using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Core.Airspace;

/// <summary>
/// Refreshes the outlines of the flight information regions. Weekly, because borders move about as
/// often as countries do, and like every other snapshot job it never throws: a failed run is a row
/// in <c>hub_jobs_log</c>, and yesterday's outlines are better than none.
/// <para>An installation that never runs it has an empty table, which is a legitimate state: the
/// proposal of contacted ATC then only covers the airports of the flight, and says so.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class FirSyncJob(
    IFirBoundarySource source,
    HubDbContext database,
    IFirLocator locator,
    IClock clock,
    ILogger<FirSyncJob> logger) : IJob
{
    /// <summary>Name under which the runs are recorded.</summary>
    public const string JobName = "fir-boundaries-sync";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many outlines the table holds after the run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        database.JobsLog.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        try
        {
            var incoming = await source.GetAsync(cancellationToken);
            if (incoming.Count == 0)
            {
                entry.FinishedAt = clock.UtcNow;
                entry.Status = "skipped";
                entry.Message = "no boundary came back; the outlines are left alone";
                await database.SaveChangesAsync(cancellationToken);

                logger.LogWarning("No FIR boundary came back; the outlines are left alone.");
                return await database.Firs.CountAsync(cancellationToken);
            }

            var existing = await database.Firs.ToDictionaryAsync(
                fir => fir.Id,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

            foreach (var boundary in incoming)
            {
                if (!existing.TryGetValue(boundary.Id, out var row))
                {
                    row = new FirBoundary { Id = boundary.Id };
                    database.Firs.Add(row);
                }

                row.IsOceanic = boundary.IsOceanic;
                row.Region = boundary.Region;
                row.GeometryJson = boundary.GeometryJson;
                row.MinLatitude = boundary.MinLatitude;
                row.MaxLatitude = boundary.MaxLatitude;
                row.MinLongitude = boundary.MinLongitude;
                row.MaxLongitude = boundary.MaxLongitude;
                row.SyncedAt = clock.UtcNow;
            }

            // What the source no longer lists has been redrawn away; a non empty answer is the
            // whole world, so a row missing from it is gone rather than unmentioned.
            var keep = incoming.Select(boundary => boundary.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var gone = existing.Where(pair => !keep.Contains(pair.Key)).Select(pair => pair.Value).ToArray();
            if (gone.Length > 0)
            {
                database.Firs.RemoveRange(gone);
            }

            await database.SaveChangesAsync(cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(
                CultureInfo.InvariantCulture,
                $"{incoming.Count} FIR outline(s), {gone.Length} withdrawn");

            await database.SaveChangesAsync(cancellationToken);
            locator.Invalidate();

            logger.LogInformation("FIR outlines synchronised: {Message}.", entry.Message);
            return incoming.Count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            database.ChangeTracker.Clear();
            database.JobsLog.Attach(entry);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length > MaxMessageLength
                ? exception.Message[..MaxMessageLength]
                : exception.Message;

            await database.SaveChangesAsync(CancellationToken.None);

            logger.LogWarning(exception, "The FIR outline synchronisation failed; the outlines are unchanged.");
            return 0;
        }
    }
}
