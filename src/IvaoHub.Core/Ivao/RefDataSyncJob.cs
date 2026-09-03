using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// Refreshes the snapshot of the FIRs and airports of the division. They are read from IVAO, never
/// configured, and kept locally so the hub keeps working when the API does not (plan section 4.1).
/// <para>It never throws. A failed run is a row in <c>hub_jobs_log</c> and a warning, because the
/// site going down for a synchronisation that could have waited until tomorrow would be worse than
/// the data being a day old.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class RefDataSyncJob(
    IIvaoApiClient ivao,
    HubDbContext database,
    IFirDirectory firs,
    IOptions<DivisionOptions> division,
    IClock clock,
    ILogger<RefDataSyncJob> logger) : IJob
{
    /// <summary>Name under which the runs are recorded.</summary>
    public const string JobName = "ref-data-sync";

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many centres and airports were written; zeroes when the run failed.</summary>
    public async Task<(int Centers, int Airports)> RunAsync(CancellationToken cancellationToken = default)
    {
        var started = clock.UtcNow;
        var countryId = division.Value.CountryId;

        var entry = new JobLogEntry { Job = JobName, StartedAt = started, Status = "running" };
        database.JobsLog.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        try
        {
            var centers = await SyncCentersAsync(countryId, cancellationToken);
            var airports = await SyncAirportsAsync(countryId, cancellationToken);

            await database.SaveChangesAsync(cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = centers == 0 && airports == 0 ? "skipped" : "succeeded";
            entry.Message = string.Create(
                CultureInfo.InvariantCulture,
                $"{centers} centre(s), {airports} airport(s) for {countryId}");

            await database.SaveChangesAsync(cancellationToken);

            // The FIRs just changed, so anything holding the old set has to let go of it.
            firs.Invalidate();

            logger.LogInformation("Reference data synchronised: {Message}.", entry.Message);
            return (centers, airports);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message;

            // The row matters more than the exception: without it a run that never happened and a
            // run that failed look identical from the outside.
            await database.SaveChangesAsync(CancellationToken.None);

            logger.LogWarning(exception, "The reference data synchronisation failed; the snapshot is unchanged.");
            return (0, 0);
        }
    }

    private async Task<int> SyncCentersAsync(string countryId, CancellationToken cancellationToken)
    {
        var incoming = await ivao.GetCentersAsync(countryId, cancellationToken);
        if (incoming.Count == 0)
        {
            // Nothing came back. Keeping yesterday's airspace beats emptying the table.
            logger.LogWarning("IVAO returned no centre for {Country}; the snapshot is left alone.", countryId);
            return 0;
        }

        var existing = await database.IvaoCenters.ToDictionaryAsync(
            center => center.Id,
            StringComparer.OrdinalIgnoreCase,
            cancellationToken);

        foreach (var center in incoming)
        {
            if (!existing.TryGetValue(center.Id, out var row))
            {
                row = new IvaoCenter { Id = center.Id };
                database.IvaoCenters.Add(row);
            }

            row.Name = center.Name;
            row.CountryId = center.CountryId;
            row.RawJson = center.RawJson;
            row.SyncedAt = clock.UtcNow;
        }

        return incoming.Count;
    }

    private async Task<int> SyncAirportsAsync(string countryId, CancellationToken cancellationToken)
    {
        var incoming = await ivao.GetAirportsAsync(countryId, includeRunways: true, cancellationToken);
        if (incoming.Count == 0)
        {
            logger.LogWarning("IVAO returned no airport for {Country}; the snapshot is left alone.", countryId);
            return 0;
        }

        var existing = await database.IvaoAirports.ToDictionaryAsync(
            airport => airport.Icao,
            StringComparer.OrdinalIgnoreCase,
            cancellationToken);

        foreach (var airport in incoming)
        {
            if (!existing.TryGetValue(airport.Icao, out var row))
            {
                row = new IvaoAirport { Icao = airport.Icao };
                database.IvaoAirports.Add(row);
            }

            row.Name = airport.Name;
            row.CountryId = airport.CountryId;
            row.CenterId = airport.CenterId;
            row.RunwaysJson = airport.RunwaysJson;
            row.RawJson = airport.RawJson;
            row.SyncedAt = clock.UtcNow;
        }

        return incoming.Count;
    }
}
