using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Threads;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>
/// The completion of a tour (design M2 §3.11): when an accepted report finishes it for its pilot, the enrolment writes
/// <c>completed_at</c> and points the pilot out for the tour's award, <b>in the save that accepts the report</b> — the signal is
/// a projection of the enrolment (<see cref="Enrolment.AwardSignal"/>), written by the interceptor in the same transaction.
/// Nobody is given an award here: whoever holds <c>Awards.Assign</c> answers from the core's queue.
/// <para>Never taken back: a leg added afterwards, or the decision reopened and turned into a rejection, leaves the completion
/// and the signal where they are. A subtour completed counts for its container, and the container completes — and signals —
/// when <c>required_subtours</c> of them are.</para>
/// </summary>
public sealed class TourCompletion(
    FlightOpsDbContext database,
    PilotProgress progress,
    FlightOpsReferences references,
    IOptions<DivisionOptions> division,
    IClock clock)
{
    /// <summary>
    /// Called with the report as it is about to be saved accepted, before the save: what it completes is written in the same
    /// save. Does nothing for any other outcome, or when the tour was already completed.
    /// </summary>
    public async Task RecordAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        if (pirep.Status != PirepStatus.Accepted)
        {
            return;
        }

        var tour = await CrudSource.BackOffice<Tour>(database).AsNoTracking().FirstAsync(row => row.Id == pirep.TourId, cancellationToken);
        var enrolment = await database.Enrolments.FirstOrDefaultAsync(row => row.TourId == tour.Id && row.Vid == pirep.Vid, cancellationToken);
        if (enrolment is not { CompletedAt: null })
        {
            return;
        }

        // The pilot's reports as they will be once this one is saved: the others from the database, this one as it is now.
        var reports = await database.Pireps.AsNoTracking()
            .Include(report => report.Flights)
            .Where(report => report.TourId == tour.Id && report.Vid == pirep.Vid && report.Id != pirep.Id)
            .ToListAsync(cancellationToken);
        reports.Add(pirep);

        if (!(await progress.OfAsync(tour, pirep.Vid, reports, cancellationToken)).Finished)
        {
            return;
        }

        var now = clock.UtcNow;
        Complete(enrolment, tour, now);

        if (tour.ParentTourId is not { } parentId)
        {
            return;
        }

        var parent = await CrudSource.BackOffice<Tour>(database).AsNoTracking().FirstAsync(row => row.Id == parentId, cancellationToken);
        var container = await database.Enrolments.FirstOrDefaultAsync(row => row.TourId == parentId && row.Vid == pirep.Vid, cancellationToken);
        if (container is { CompletedAt: null }
            && parent.RequiredSubtours is { } required
            && await progress.SubtoursDoneAsync(parentId, pirep.Vid, cancellationToken) >= required)
        {
            Complete(container, parent, now);
        }
    }

    private void Complete(Enrolment enrolment, Tour tour, DateTime now)
    {
        enrolment.CompletedAt = now;
        if (tour.IsSubtour)
        {
            return;
        }

        // The queue shows the reason as it is written: in the division's language, the one the staff shares.
        var locale = division.Value.DefaultLocale;
        var reason = references.Words(
            locale,
            "flightops:awards.tourCompleted",
            ("tour", tour.Title.Resolve(locale, locale) ?? string.Empty));
        enrolment.AwardSignal = new AwardSignalProjection(enrolment.Vid, reason, tour.AwardId);
    }
}
