using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.People;

/// <summary>
/// A pilot's tours as they read them (design M2 §8.2; note 2026-09-23-completamento-validatori-piloti-ban §3.4): the tours they
/// started and still see, how far, and the next leg; the reports to correct; the threads the staff answered; and the summary —
/// legs accepted, minutes flown, tours completed. No count of errors: those are the staff's (§8.7).
/// </summary>
public sealed record MyToursDto(
    IReadOnlyList<StartedTourDto> Tours,
    IReadOnlyList<ReportToFixDto> ToModify,
    IReadOnlyList<AnsweredThreadDto> Answered,
    PilotSummaryDto Summary);

/// <summary>A tour the pilot started, with the measure of <see cref="PilotProgress"/> and the next leg while it is not done.</summary>
public sealed record StartedTourDto(
    long TourId,
    string Slug,
    Localized<string> Title,
    long? ParentTourId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int Done,
    int Target,
    ProgressUnit Unit,
    NextLegDto? Next);

public sealed record NextLegDto(long Id, int Number, string DepartureIcao, string ArrivalIcao);

/// <summary>A report the validator sent back «to modify», with the address of the form that corrects it.</summary>
public sealed record ReportToFixDto(
    long PirepId,
    long TourId,
    string Slug,
    Localized<string> TourTitle,
    string DepartureIcao,
    string ArrivalIcao,
    DateTime TakeoffAt);

/// <summary>A dispute or a clarification about the tours that the staff answered and the pilot has not read yet.</summary>
public sealed record AnsweredThreadDto(long Id, string Kind, string Subject, DateTime UpdatedAt);

/// <summary>
/// Legs accepted, the minutes the tracker recorded from take-off to landing on the accepted reports (Carmine, 23 September
/// 2026), and the tours of the first level completed. Everything the pilot ever flew, hidden tours included: it is their record.
/// </summary>
public sealed record PilotSummaryDto(int LegsAccepted, int MinutesFlown, int ToursCompleted);

/// <summary>
/// The one answer to «which tours is this pilot in, and where» (note 2026-09-24-le-pagine-delle-persone §3.1), read by the block
/// <c>flightops.myTours</c> and by the cards of <c>/tours</c> through <c>GET /api/flightops/my-tours</c>. A tour the pilot no
/// longer sees is gone from the list (§1.2.2) — hidden, or a subtour of a container that is — and still counts in the summary.
/// </summary>
public sealed class MyTours(
    FlightOpsDbContext database,
    HubDbContext hub,
    PilotProgress progress,
    IClock clock)
{
    public async Task<MyToursDto> OfAsync(int vid, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var enrolments = await database.Enrolments.AsNoTracking()
            .Where(row => row.Vid == vid)
            .ToListAsync(cancellationToken);
        var reports = await database.Pireps.AsNoTracking()
            .Include(report => report.Flights)
            .Where(report => report.Vid == vid)
            .ToListAsync(cancellationToken);

        // What the pilot may still see: the tours of the public, and a subtour only with its container.
        var tourIds = enrolments.Select(row => row.TourId).Union(reports.Select(report => report.TourId)).ToList();
        var readable = await database.Tours.AsNoTracking()
            .Where(tour => tourIds.Contains(tour.Id))
            .ToDictionaryAsync(tour => tour.Id, cancellationToken);
        var visible = readable.Values
            .Where(tour => TourState.IsPublic(tour, now)
                && (tour.ParentTourId is not { } parentId || (readable.TryGetValue(parentId, out var parent) && TourState.IsPublic(parent, now))))
            .ToDictionary(tour => tour.Id);

        var standings = new List<(Enrolment Enrolment, Tour Tour, PilotStanding Standing)>();
        foreach (var enrolment in enrolments.Where(row => visible.ContainsKey(row.TourId)))
        {
            var tour = visible[enrolment.TourId];
            standings.Add((enrolment, tour, await progress.OfAsync(tour, vid, [.. reports.Where(report => report.TourId == tour.Id)], cancellationToken)));
        }

        // The next leg only while the tour is not done: a completed tour has nothing left to fly, whatever a leg added later says.
        var nextIds = standings.Where(row => row.Enrolment.CompletedAt is null).Select(row => row.Standing.Progress.Next).OfType<long>().ToList();
        var nextLegs = await database.Legs.AsNoTracking()
            .Where(leg => nextIds.Contains(leg.Id))
            .ToDictionaryAsync(leg => leg.Id, cancellationToken);

        return new MyToursDto(
            [
                .. standings
                    .OrderBy(row => row.Enrolment.CompletedAt is null ? 0 : 1)
                    .ThenByDescending(row => row.Enrolment.StartedAt)
                    .Select(row => new StartedTourDto(
                        row.Tour.Id,
                        row.Tour.Slug!,
                        row.Tour.Title,
                        row.Tour.ParentTourId,
                        row.Enrolment.StartedAt,
                        row.Enrolment.CompletedAt,
                        row.Standing.Done,
                        row.Standing.Target,
                        row.Standing.Unit,
                        row.Enrolment.CompletedAt is null && row.Standing.Progress.Next is { } next && nextLegs.TryGetValue(next, out var leg)
                            ? new NextLegDto(leg.Id, leg.Number, leg.DepartureIcao, leg.ArrivalIcao)
                            : null)),
            ],
            [
                .. reports
                    .Where(report => report.Status == PirepStatus.ToModify && visible.ContainsKey(report.TourId))
                    .OrderBy(report => report.TakeoffAt)
                    .Select(report => new ReportToFixDto(
                        report.Id,
                        report.TourId,
                        visible[report.TourId].Slug!,
                        visible[report.TourId].Title,
                        report.DepartureIcao,
                        report.ArrivalIcao,
                        report.TakeoffAt)),
            ],
            await AnsweredAsync(vid, cancellationToken),
            new PilotSummaryDto(
                reports.Count(report => report.Status == PirepStatus.Accepted),
                MinutesFlown(reports),
                await ToursCompletedAsync(enrolments, cancellationToken)));
    }

    /// <summary>From take-off to landing, as the tracker recorded them, on every flight of the accepted reports.</summary>
    private static int MinutesFlown(IEnumerable<Pirep> reports) =>
        (int)reports
            .Where(report => report.Status == PirepStatus.Accepted)
            .SelectMany(report => report.Flights)
            .Where(flight => flight.LandingAt is not null)
            .Sum(flight => (flight.LandingAt!.Value - flight.TakeoffAt).TotalMinutes);

    /// <summary>
    /// The tours of the first level completed, hidden ones included. Only the container is read off a tour the pilot may no
    /// longer see — the back office's source, and nothing of the tour leaves this method but whether it has a parent.
    /// </summary>
    private async Task<int> ToursCompletedAsync(IReadOnlyList<Enrolment> enrolments, CancellationToken cancellationToken)
    {
        var completed = enrolments.Where(row => row.CompletedAt is not null).Select(row => row.TourId).ToList();
        if (completed.Count == 0)
        {
            return 0;
        }

        return await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .CountAsync(tour => completed.Contains(tour.Id) && tour.ParentTourId == null, cancellationToken);
    }

    /// <summary>
    /// The pilot's threads with the tours — disputes their reports opened, clarifications citing the tours — that the staff
    /// answered: the core's filter lets a sender read their own (T14a), as <see cref="Threads.PirepDisputes"/> does.
    /// </summary>
    private async Task<IReadOnlyList<AnsweredThreadDto>> AnsweredAsync(int vid, CancellationToken cancellationToken)
    {
        var cited = hub.ContactReferences.Where(reference => reference.SourceModule == FlightOpsModule.ModuleKey).Select(reference => reference.MessageId);

        return await hub.ContactMessages.AsNoTracking()
            .Where(message => message.CreatedBy == vid
                && message.Status == ContactStatus.Answered
                && ((message.Kind == ContactKinds.Dispute && message.SourceModule == FlightOpsModule.ModuleKey)
                    || (message.Kind == ContactKinds.Clarification && cited.Contains(message.Id))))
            .OrderByDescending(message => message.UpdatedAt)
            .Select(message => new AnsweredThreadDto(message.Id, message.Kind, message.Subject, message.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// The block <c>flightops.myTours</c> (design M2 §8.2): <see cref="MyTours"/> for whoever is looking, always live and without
/// properties. A visitor gets nothing — the block belongs on <c>/me</c>, and a page that shows it to the public shows an empty one.
/// </summary>
public sealed class MyToursProvider(MyTours myTours, ICurrentUser currentUser) : IDataBlockProvider
{
    public const string BlockType = "flightops.myTours";

    /// <summary>The same shape the HTTP answer has: the browser reads the block and the endpoint with one type.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LocalizedJsonConverterFactory(), new JsonStringEnumConverter() },
    };

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return new JsonObject { ["signedIn"] = false };
        }

        var node = JsonSerializer.SerializeToNode(await myTours.OfAsync(currentUser.Vid, cancellationToken), Json)!.AsObject();
        node["signedIn"] = true;
        return node;
    }
}

public static class MyToursEndpoints
{
    public const string Pattern = "/api/flightops/my-tours";

    public static IEndpointRouteBuilder MapMyToursEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Any signed in member: a pilot is not a role. The cards of /tours read it to draw the pilot's progress on top.
        app.MapGet(Pattern, async (MyTours myTours, ICurrentUser user, HttpContext http) =>
                Results.Ok(await myTours.OfAsync(user.Vid, http.RequestAborted)))
            .WithTags("FlightOpsMyTours")
            .WithName("FlightOpsMyTours")
            .RequireAuthorization(HubPolicies.SignedIn)
            .Produces<MyToursDto>();

        return app;
    }
}
