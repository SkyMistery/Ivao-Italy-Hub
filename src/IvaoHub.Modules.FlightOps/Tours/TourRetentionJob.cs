using System.Globalization;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>
/// The retention of the tours (design M2 §10, road B of §10.1; note 2026-09-25-la-conservazione-dei-tour): once a month, a
/// ready tour closed more than <c>retentionMonths</c> ago — <c>retentionMonthsLong</c> when it ran for more than a year — loses
/// what weighs, and is archived. The tour and its legs stay, and so does the disciplinary record: every report with its
/// decision, its confirmed errors, its history, and the bans.
/// <para>What goes, of every report: tracks, check results, errors suggested and not confirmed, the revisions of the plans,
/// controllers and exemptions, the notes, and of the rules it froze all but those with a confirmed error, without their texts
/// and parameters (<see cref="PirepSubmission.ArchivedSnapshot"/>). Of the tour: the briefing, its own rules, hubs and
/// rotations, constraints, callsign constraints and enrolments. Its pictures are the core's: their uses ended a month after
/// the close (§1.14).</para>
/// <para>A tour waits, and is counted as waiting, while one of its reports is still to be decided or disputed, or one of its
/// completions still waits for its award: the job never takes from a validator what they are reading, nor makes a forgotten
/// award final. One save per tour, through the interceptor like every write (no bulk statement).</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class TourRetentionJob(
    FlightOpsDbContext database,
    HubDbContext hub,
    ModuleSettingsStore settingsStore,
    IClock clock,
    ILogger<TourRetentionJob> logger) : IJob
{
    public const string JobName = "flightops-tour-retention";

    /// <summary>The first day of every month at 04:20 on the server's clock (Quartz reads local time), after the core's media job.</summary>
    public const string Cron = "0 20 4 1 * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>What the run did: tours archived, reports emptied, tours still waiting.</summary>
    public sealed record Outcome(int Archived, int Reports, int Waiting);

    public async Task<Outcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
            var now = entry.StartedAt;

            // The shorter period is the first filter; the longer one is asked of each tour below.
            var shortBefore = now.AddMonths(-settings.RetentionMonths);
            var due = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
                .Where(tour => !tour.IsTemplate
                    && tour.Status == PublishStatus.Published
                    && tour.PurgedAt == null
                    && tour.CloseAt != null
                    && tour.CloseAt < shortBefore)
                .Select(tour => new { tour.Id, tour.ReleaseAt, tour.CloseAt })
                .ToListAsync(cancellationToken);

            var archived = 0;
            var reports = 0;
            var waiting = 0;

            foreach (var tour in due)
            {
                if (tour.CloseAt!.Value.AddMonths(RetentionMonths(tour.ReleaseAt, tour.CloseAt.Value, settings)) > now)
                {
                    continue;
                }

                if (await WaitsAsync(tour.Id, cancellationToken))
                {
                    waiting++;
                    continue;
                }

                reports += await ArchiveAsync(tour.Id, now, cancellationToken);
                archived++;
            }

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(
                CultureInfo.InvariantCulture,
                $"{archived} tour(s) archived, {reports} report(s) emptied, {waiting} tour(s) waiting");
            await hub.SaveChangesAsync(cancellationToken);

            return new Outcome(archived, reports, waiting);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The tour retention job failed.");

            database.ChangeTracker.Clear();
            hub.ChangeTracker.Clear();
            hub.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength ? exception.Message : exception.Message[..MaxMessageLength];
            await hub.SaveChangesAsync(cancellationToken);

            return new Outcome(0, 0, 0);
        }
    }

    /// <summary>
    /// 13 months, or 25 for a tour that ran for more than a year: its close more than twelve months after its release (Carmine,
    /// 25 September 2026). A tour from November to February is not one.
    /// </summary>
    public static int RetentionMonths(DateTime? releaseAt, DateTime closeAt, FlightOpsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return releaseAt is { } release && closeAt > release.AddMonths(12) ? settings.RetentionMonthsLong : settings.RetentionMonths;
    }

    /// <summary>A report still to be decided, or disputed, or a completion whose award nobody has handled yet.</summary>
    private async Task<bool> WaitsAsync(long tourId, CancellationToken cancellationToken)
    {
        var open = await CrudSource.BackOffice<Pirep>(database).AnyAsync(
            report => report.TourId == tourId
                && (report.Status == PirepStatus.Queued
                    || report.Status == PirepStatus.InReview
                    || report.Status == PirepStatus.ToModify
                    || report.DisputeStatus == DisputeStatus.Open),
            cancellationToken);
        if (open)
        {
            return true;
        }

        var enrolments = await database.Enrolments.AsNoTracking()
            .Where(enrolment => enrolment.TourId == tourId && enrolment.CompletedAt != null)
            .Select(enrolment => enrolment.Id)
            .ToListAsync(cancellationToken);
        var sources = enrolments.Select(Enrolment.ReferenceOf).ToList();

        return sources.Count > 0
            && await hub.AwardSignals.AsNoTracking().AnyAsync(
                signal => signal.SourceModule == FlightOpsModule.ModuleKey
                    && sources.Contains(signal.SourceId)
                    && signal.Status == AwardSignalStatus.Pending,
                cancellationToken);
    }

    /// <summary>Empties one tour and its reports in one save; returns how many reports it emptied.</summary>
    private async Task<int> ArchiveAsync(long tourId, DateTime now, CancellationToken cancellationToken)
    {
        var tour = await CrudSource.BackOffice<Tour>(database).FirstAsync(row => row.Id == tourId, cancellationToken);
        tour.BriefingJson = Tour.EmptyBriefing;
        tour.PurgedAt = now;

        var pireps = await CrudSource.BackOffice<Pirep>(database)
            .Include(report => report.Flights)
            .Include(report => report.Errors)
            .Where(report => report.TourId == tourId)
            .ToListAsync(cancellationToken);

        foreach (var pirep in pireps)
        {
            Empty(pirep);
        }

        var pirepIds = pireps.Select(report => report.Id).ToList();
        var flightIds = pireps.SelectMany(report => report.Flights).Select(flight => flight.Id).ToList();

        // The keys only, as the track job does: the blobs are what is being deleted.
        var tracks = await database.PirepTracks.AsNoTracking()
            .Where(track => flightIds.Contains(track.PirepFlightId))
            .Select(track => track.PirepFlightId)
            .ToListAsync(cancellationToken);
        database.PirepTracks.RemoveRange(tracks.Select(key => new PirepTrack { PirepFlightId = key }));

        database.CheckResults.RemoveRange(
            await database.CheckResults.Where(result => pirepIds.Contains(result.PirepId)).ToListAsync(cancellationToken));

        // The rows of the tour's shape are audited: loaded whole, so what the audit keeps of a deleted row is the row.
        database.Rules.RemoveRange(
            await database.Rules.Include(rule => rule.ErrorLinks).Where(rule => rule.TourId == tourId).ToListAsync(cancellationToken));
        database.CallsignRules.RemoveRange(
            await database.CallsignRules.Where(rule => rule.TourId == tourId).ToListAsync(cancellationToken));
        database.TourConstraints.RemoveRange(
            await database.TourConstraints.Where(constraint => constraint.TourId == tourId).ToListAsync(cancellationToken));

        // A leg keeps its place in the tour and loses its rotation, written here so the audit sees it.
        foreach (var leg in await database.Legs.Where(leg => leg.TourId == tourId && leg.RotationId != null).ToListAsync(cancellationToken))
        {
            leg.RotationId = null;
        }

        database.Rotations.RemoveRange(
            await database.Rotations.Where(rotation => rotation.TourId == tourId).ToListAsync(cancellationToken));
        database.Hubs.RemoveRange(await database.Hubs.Where(row => row.TourId == tourId).ToListAsync(cancellationToken));
        database.Enrolments.RemoveRange(
            await database.Enrolments.Where(enrolment => enrolment.TourId == tourId).ToListAsync(cancellationToken));

        await database.SaveChangesAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return pireps.Count;
    }

    /// <summary>What a report loses; the decision, the confirmed errors and the history stay.</summary>
    private void Empty(Pirep pirep)
    {
        pirep.RulesSnapshotJson = PirepSubmission.ArchivedSnapshot(pirep);
        pirep.AtcContactsJson = "[]";
        pirep.AtcExemptionsJson = "[]";
        pirep.PilotRemarks = null;
        pirep.DiversionNote = null;
        pirep.NoteToPilot = null;
        pirep.StaffNote = null;
        pirep.OverrideReason = null;
        pirep.DisputeText = null;

        foreach (var flight in pirep.Flights)
        {
            flight.FlightPlansJson = "[]";
            flight.PlanAtTakeoffRevision = null;
        }

        database.PirepErrors.RemoveRange(pirep.Errors.Where(error => !error.Confirmed));
    }
}
