using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Review;

/// <summary>
/// The block <c>flightops.reviewQueue</c> (design M2 §8.2): the reports waiting on the tours whoever is looking may validate,
/// one line per tour — how many, and since when the oldest waits — each a link to the queue of that tour. The daily digest
/// (§4.2.2) on a dashboard, and for the same reason one line per tour and not per report: it stays short with fifty reports
/// waiting (Carmine, 23 September 2026, T13b).
/// <para>Always live and answered for the person asking: a report counts when the one handler says they may validate it —
/// department and scope of the row, never their own — which is the question <c>canTake</c> asks in the queue.</para>
/// </summary>
public sealed class ReviewQueueProvider(
    FlightOpsDbContext database,
    PirepReview reviews,
    ICurrentUser currentUser,
    IClock clock) : IDataBlockProvider
{
    public const string BlockType = "flightops.reviewQueue";

    /// <summary>How many waiting reports are read before the permissions narrow them: a dashboard is not the queue.</summary>
    private const int Window = 500;

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        var items = new JsonArray();
        if (!currentUser.IsAuthenticated || !currentUser.HasAny(TourPermissions.Validate))
        {
            return new JsonObject { ["items"] = items };
        }

        var now = clock.UtcNow;

        // Waiting, as the digest counts it: in the queue, or taken by somebody whose lease has run out (§4.2).
        var waiting = await CrudSource.BackOffice<Pirep>(database).AsNoTracking()
            .Where(report => report.Vid != currentUser.Vid
                && (report.Status == PirepStatus.Queued
                    || (report.Status == PirepStatus.InReview && (report.LeaseUntil == null || report.LeaseUntil <= now))))
            .OrderBy(report => report.QueuedAt)
            .Take(Window)
            .ToListAsync(cancellationToken);

        var theirs = new List<Pirep>(waiting.Count);
        foreach (var report in waiting)
        {
            if (await reviews.MayValidateAsync(report))
            {
                theirs.Add(report);
            }
        }

        var groups = theirs
            .GroupBy(report => report.TourId)
            .Select(group => (TourId: group.Key, Count: group.Count(), Oldest: group.Min(report => report.QueuedAt)))
            .OrderBy(group => group.Oldest)
            .ToList();

        var tourIds = groups.Select(group => group.TourId).ToList();
        var titles = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .Where(tour => tourIds.Contains(tour.Id))
            .ToDictionaryAsync(tour => tour.Id, tour => tour.Title, cancellationToken);

        foreach (var (tourId, count, oldest) in groups)
        {
            items.Add(new JsonObject
            {
                ["tourId"] = tourId,
                ["title"] = BlockProps.Translated(titles.GetValueOrDefault(tourId)),
                ["count"] = count,
                ["oldest"] = BlockProps.Instant(oldest),
            });
        }

        return new JsonObject { ["items"] = items };
    }
}
