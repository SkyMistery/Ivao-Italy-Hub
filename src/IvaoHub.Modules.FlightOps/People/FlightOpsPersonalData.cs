using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Threads;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.People;

/// <summary>
/// The tours' half of erasing a person's data (design M2 §10.0; notes 2026-09-25-la-cancellazione-dei-dati-di-una-persona and
/// 2026-09-25-le-righe-che-restano-con-il-vid). The disciplinary record stays, without the pilot; the rest about them goes.
/// <list type="bullet">
/// <item>A report <b>decided</b> (accepted or rejected) stays: its decision, confirmed errors and history. It loses what the
/// retention takes (<see cref="TourRetentionJob.Empty"/>: notes, the dispute's text, controllers, plans, errors never confirmed)
/// and, since it is the person that goes rather than the tour, what ties it to them: the callsign and the tracker's session of
/// its flights, their tracks, the checks' results, and the words a validator wrote when reopening it.</item>
/// <item>A report still open, sent back, or withdrawn is not part of the record: it goes, with everything under it.</item>
/// <item>Enrolments and the leg issues they reported go.</item>
/// <item>A ban still in force stays as it is, VID and reason (Carmine: a ban without the VID protects nobody); an erasure run
/// again after it ends takes it. A ban already over loses its reason.</item>
/// </list>
/// Nothing here writes a VID: the core writes the pseudonym into every <c>vid</c>/<c>*_vid</c>/<c>*_by</c> column afterwards.
/// </summary>
public sealed class FlightOpsPersonalData(FlightOpsDbContext database, IClock clock) : IPersonalDataEraser
{
    public string ModuleKey => FlightOpsModule.ModuleKey;

    public async Task<IReadOnlyList<ErasureLine>> PreviewAsync(int vid, CancellationToken cancellationToken = default)
    {
        var statuses = await CrudSource.BackOffice<Pirep>(database)
            .Where(report => report.Vid == vid)
            .Select(report => report.Status)
            .ToListAsync(cancellationToken);
        var bans = await Bans(vid).AsNoTracking().ToListAsync(cancellationToken);
        var now = clock.UtcNow;

        return Lines(
            decided: statuses.Count(IsRecord),
            open: statuses.Count(status => !IsRecord(status)),
            enrolments: await database.Enrolments.CountAsync(row => row.Vid == vid, cancellationToken),
            issues: await CrudSource.BackOffice<LegIssue>(database).CountAsync(row => row.CreatedBy == vid, cancellationToken),
            inForce: bans.Count(ban => InForce(ban, now)),
            ended: bans.Count(ban => !InForce(ban, now)));
    }

    public async Task<IReadOnlyList<ErasureLine>> EraseAsync(ErasureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vid = request.Vid;
        var now = clock.UtcNow;

        var reports = await CrudSource.BackOffice<Pirep>(database).AsTracking()
            .Include(report => report.Flights)
            .Include(report => report.Errors)
            .Include(report => report.Events)
            .Where(report => report.Vid == vid)
            .ToListAsync(cancellationToken);

        var (kept, gone) = (reports.Where(report => IsRecord(report.Status)).ToList(), reports.Where(report => !IsRecord(report.Status)).ToList());

        foreach (var report in kept)
        {
            TourRetentionJob.Empty(database, report);

            foreach (var flight in report.Flights)
            {
                flight.Callsign = string.Empty;
                flight.TrackerSessionId = 0;
                flight.ClaimedSessionId = null;
            }

            // The history keeps its steps and their keys; the one step with words of its own is a reopening's reason.
            foreach (var step in report.Events.Where(step => step.Note is { } note && !note.StartsWith($"{FlightOpsModule.ModuleKey}:", StringComparison.Ordinal)))
            {
                step.Note = null;
            }
        }

        // What is under a report the record keeps, but not about it any more: the tracks and the checks' results.
        var keptIds = kept.Select(report => report.Id).ToList();
        var flightIds = kept.SelectMany(report => report.Flights).Select(flight => flight.Id).ToList();
        var tracks = await database.PirepTracks.AsNoTracking()
            .Where(track => flightIds.Contains(track.PirepFlightId))
            .Select(track => track.PirepFlightId)
            .ToListAsync(cancellationToken);
        database.PirepTracks.RemoveRange(tracks.Select(key => new PirepTrack { PirepFlightId = key }));
        database.CheckResults.RemoveRange(
            await database.CheckResults.Where(result => keptIds.Contains(result.PirepId)).ToListAsync(cancellationToken));

        // A report that is not in the record goes whole: its flights, tracks, errors, results and history follow it.
        database.Pireps.RemoveRange(gone);

        var enrolments = await database.Enrolments.Where(row => row.Vid == vid).ToListAsync(cancellationToken);
        database.Enrolments.RemoveRange(enrolments);

        var issues = await CrudSource.BackOffice<LegIssue>(database).AsTracking()
            .Where(row => row.CreatedBy == vid)
            .ToListAsync(cancellationToken);
        database.LegIssues.RemoveRange(issues);

        var bans = await Bans(vid).AsTracking().ToListAsync(cancellationToken);
        foreach (var ban in bans)
        {
            if (InForce(ban, now))
            {
                request.Keep(ban);
            }
            else
            {
                ban.Reason = string.Empty;
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        return Lines(
            decided: kept.Count,
            open: gone.Count,
            enrolments: enrolments.Count,
            issues: issues.Count,
            inForce: bans.Count(ban => InForce(ban, now)),
            ended: bans.Count(ban => !InForce(ban, now)));
    }

    /// <summary>A report the disciplinary record is made of: decided, and not sent back to the pilot.</summary>
    private static bool IsRecord(PirepStatus status) => status is PirepStatus.Accepted or PirepStatus.Rejected;

    /// <summary>Running now, or still to start: either way it protects the tours from the person.</summary>
    private static bool InForce(Ban ban, DateTime now) => ban.EndsAt is null || ban.EndsAt > now;

    private IQueryable<Ban> Bans(int vid) => CrudSource.BackOffice<Ban>(database).Where(ban => ban.Vid == vid);

    private static IReadOnlyList<ErasureLine> Lines(int decided, int open, int enrolments, int issues, int inForce, int ended) =>
    [
        new("flightops:erasure.decided", decided, ErasureOutcome.Anonymised),
        new("flightops:erasure.open", open, ErasureOutcome.Deleted),
        new("flightops:erasure.enrolments", enrolments, ErasureOutcome.Deleted),
        new("flightops:erasure.issues", issues, ErasureOutcome.Deleted),
        new("flightops:erasure.bansInForce", inForce, ErasureOutcome.Kept),
        new("flightops:erasure.bansEnded", ended, ErasureOutcome.Anonymised),
    ];
}
