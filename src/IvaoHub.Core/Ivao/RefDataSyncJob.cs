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

    /// <summary>Longest failure message kept in <c>hub_jobs_log</c>; the log file has the rest.</summary>
    private const int MaxMessageLength = 2000;

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
            var aircraft = await SyncAircraftAsync(cancellationToken);

            await database.SaveChangesAsync(cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = Outcome(centers, airports);
            entry.Message = string.Create(
                CultureInfo.InvariantCulture,
                $"{centers} centre(s) for {countryId}, {airports} airport(s) of the world, {aircraft} aircraft type(s)");

            await database.SaveChangesAsync(cancellationToken);

            // The FIRs just changed, so anything holding the old set has to let go of it.
            firs.Invalidate();

            logger.LogInformation("Reference data synchronised: {Message}.", entry.Message);
            return (centers, airports);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordFailureAsync(entry, exception);

            logger.LogWarning(exception, "The reference data synchronisation failed; the snapshot is unchanged.");
            return (0, 0);
        }
    }

    /// <summary>
    /// Which of the two halves actually landed. A run that refreshed the centres and got nothing
    /// back for the airports is not a success: calling it one is how a half stale snapshot goes
    /// unnoticed for weeks.
    /// </summary>
    private static string Outcome(int centers, int airports) => (centers, airports) switch
    {
        (0, 0) => "skipped",
        (0, _) or (_, 0) => "partial",
        _ => "succeeded",
    };

    /// <summary>
    /// Turns the running row into a failed one, and nothing else. Everything the run had staged is
    /// dropped first: without that, saving the failure would carry the half of the snapshot that
    /// had already been tracked into the database along with it, and the row would say "failed"
    /// over data that was in fact written.
    /// </summary>
    private async Task RecordFailureAsync(JobLogEntry entry, Exception exception)
    {
        database.ChangeTracker.Clear();
        database.JobsLog.Attach(entry);

        entry.FinishedAt = clock.UtcNow;
        entry.Status = "failed";
        entry.Message = exception.Message.Length > MaxMessageLength
            ? exception.Message[..MaxMessageLength]
            : exception.Message;

        // The row matters more than the exception: without it a run that never happened and a run
        // that failed look identical from the outside. Not cancellable, for the same reason.
        await database.SaveChangesAsync(CancellationToken.None);
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

        Prune(database.IvaoCenters, existing, incoming.Select(center => center.Id), countryId, "centre");
        return incoming.Count;
    }

    /// <summary>
    /// The airports of the <b>world</b> since T1 of M2: a tour flies anywhere, and a leg needs the
    /// coordinates of both ends (design M2 section 1.12). About 45 000 rows, fourteen megabytes over
    /// the wire, once a day and never while a reader waits.
    /// <para>Runways are not asked for here: that would be one call per airport for data almost
    /// nobody reads. They are fetched for the airports a tour or a report touches, by
    /// <see cref="IRunwayDirectory"/>.</para>
    /// </summary>
    private async Task<int> SyncAirportsAsync(string countryId, CancellationToken cancellationToken)
    {
        var incoming = await ivao.GetAirportsAsync(countryId: null, includeRunways: false, cancellationToken);
        if (incoming.Count == 0)
        {
            logger.LogWarning("IVAO returned no airport at all; the snapshot is left alone.");
            return 0;
        }

        var existing = await database.IvaoAirports.ToDictionaryAsync(
            airport => airport.Icao,
            StringComparer.OrdinalIgnoreCase,
            cancellationToken);

        // Forty five thousand rows: with change detection on, every Add re-scans everything tracked
        // so far, and the loop goes quadratic. It is switched off for the loop only, and SaveChanges
        // still goes through the interceptor exactly as before.
        var detecting = database.ChangeTracker.AutoDetectChangesEnabled;
        database.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
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
                row.Iata = airport.Iata;
                row.Latitude = airport.Latitude;
                row.Longitude = airport.Longitude;
                row.ElevationFeet = airport.ElevationFeet;

                // Only when the answer carries them: the world call does not ask for runways, and
                // overwriting what a call that did ask for had stored would lose it for nothing.
                if (airport.RunwaysJson is not null)
                {
                    row.RunwaysJson = airport.RunwaysJson;
                }

                row.RawJson = airport.RawJson;
                row.SyncedAt = clock.UtcNow;
            }

        }
        finally
        {
            database.ChangeTracker.AutoDetectChangesEnabled = detecting;
            database.ChangeTracker.DetectChanges();
        }

        Prune(database.IvaoAirports, existing, incoming.Select(airport => airport.Icao), countryId, "airport");
        WarnIfNotOurAirspace(
            incoming.Where(airport => airport.CountryId == countryId).Select(airport => airport.Icao),
            countryId);

        return incoming.Count;
    }

    /// <summary>
    /// The aircraft types, and the two vocabularies a flight plan is written with. They change
    /// rarely and they are small (2626 types, 36 equipment letters, 17 transponder letters), so they
    /// ride along with the daily run rather than having a job of their own.
    /// </summary>
    private async Task<int> SyncAircraftAsync(CancellationToken cancellationToken)
    {
        var incoming = await ivao.GetAircraftTypesAsync(cancellationToken);
        if (incoming.Count == 0)
        {
            logger.LogWarning("IVAO returned no aircraft type; that table is left alone.");
            return 0;
        }

        var existing = await database.IvaoAircraftTypes.ToDictionaryAsync(
            type => type.IcaoCode,
            StringComparer.OrdinalIgnoreCase,
            cancellationToken);

        foreach (var type in incoming)
        {
            if (!existing.TryGetValue(type.IcaoCode, out var row))
            {
                row = new IvaoAircraftType { IcaoCode = type.IcaoCode };
                database.IvaoAircraftTypes.Add(row);
            }

            row.IataCode = type.IataCode;
            row.Model = type.Model;
            row.Manufacturer = type.Manufacturer;
            row.Description = type.Description;
            row.WakeTurbulence = type.WakeTurbulence;
            row.NumberOfEngines = type.NumberOfEngines;
            row.Military = type.Military;
            row.RawJson = type.RawJson;
            row.SyncedAt = clock.UtcNow;
        }

        // A type IVAO stops listing is not pruned: a PIREP of three years ago may name it, and the
        // disciplinary record has to stay readable (design M2 section 10.1).
        var (equipments, transponders) = await ivao.GetFlightPlanVocabulariesAsync(cancellationToken);
        await UpsertVocabularyAsync(equipments, transponders, cancellationToken);

        return incoming.Count;
    }

    private async Task UpsertVocabularyAsync(
        IReadOnlyList<IvaoAircraftEquipment> equipments,
        IReadOnlyList<IvaoTransponderType> transponders,
        CancellationToken cancellationToken)
    {
        if (equipments.Count > 0)
        {
            var known = await database.IvaoAircraftEquipments.ToDictionaryAsync(
                equipment => equipment.Id,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

            foreach (var equipment in equipments)
            {
                if (!known.TryGetValue(equipment.Id, out var row))
                {
                    row = new IvaoAircraftEquipment { Id = equipment.Id };
                    database.IvaoAircraftEquipments.Add(row);
                }

                row.Name = equipment.Name;
                row.Order = equipment.Order;
                row.IsActive = equipment.IsActive;
                row.SyncedAt = clock.UtcNow;
            }
        }

        if (transponders.Count == 0)
        {
            return;
        }

        var existing = await database.IvaoTransponderTypes.ToDictionaryAsync(
            transponder => transponder.Id,
            StringComparer.OrdinalIgnoreCase,
            cancellationToken);

        foreach (var transponder in transponders)
        {
            if (!existing.TryGetValue(transponder.Id, out var row))
            {
                row = new IvaoTransponderType { Id = transponder.Id };
                database.IvaoTransponderTypes.Add(row);
            }

            row.Name = transponder.Name;
            row.Kind = transponder.Kind;
            row.Order = transponder.Order;
            row.SyncedAt = clock.UtcNow;
        }
    }

    /// <summary>
    /// The safety net the <c>icaoPrefixes</c> of the division are there for. They decide nothing —
    /// the airspace comes from IVAO — but a snapshot in which <b>no</b> airport starts with any of
    /// them is not this division's airspace, and the likeliest cause is a <c>countryId</c> that
    /// belongs to somebody else. Better a line in the log now than a site full of foreign airports.
    /// </summary>
    private void WarnIfNotOurAirspace(IEnumerable<string> icaos, string countryId)
    {
        var prefixes = division.Value.IcaoPrefixes;
        if (prefixes.Length == 0)
        {
            return;
        }

        var codes = icaos.ToArray();
        if (codes.Any(icao => prefixes.Any(prefix => icao.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
        {
            return;
        }

        logger.LogWarning(
            "Not one of the {Count} airport(s) IVAO returned for {Country} starts with any of the "
            + "icaoPrefixes of the division ({Prefixes}). Check 'countryId' in division.json.",
            codes.Length,
            countryId,
            string.Join(", ", prefixes));
    }

    /// <summary>
    /// Drops what IVAO no longer lists. The endpoints answer with the whole set for a country, so
    /// a row that is missing from a non empty answer has been decommissioned — and a FIR that no
    /// longer exists must stop making <c>LIRR-CH</c> look like a staff position. An empty answer
    /// never reaches here: the callers return early, so a bad afternoon at IVAO can never be read
    /// as "the division has no airspace".
    /// </summary>
    private void Prune<TEntity>(
        DbSet<TEntity> set,
        Dictionary<string, TEntity> existing,
        IEnumerable<string> incomingKeys,
        string countryId,
        string what)
        where TEntity : class
    {
        var keep = incomingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var gone = existing.Where(pair => !keep.Contains(pair.Key)).ToArray();

        if (gone.Length == 0)
        {
            return;
        }

        set.RemoveRange(gone.Select(pair => pair.Value));
        logger.LogInformation(
            "IVAO no longer lists {Count} {What}(s) for {Country}: {Gone}.",
            gone.Length,
            what,
            countryId,
            string.Join(", ", gone.Select(pair => pair.Key)));
    }
}
