using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The retention of the tours (design M2 §10, T20a; note 2026-09-25-la-conservazione-dei-tour): what goes, what stays, and
/// which tours are not touched yet.
/// </summary>
public sealed partial class PirepTests
{
    /// <summary>
    /// A tour closed fourteen months ago is archived: its report keeps its decision, its confirmed error — with its name, read
    /// from what is left of the snapshot — and its history, and loses its plans and notes; the tour loses its rules and its
    /// enrolments, leaves the public and refuses every change, and its decision is not reopened. A tour closed a year ago,
    /// one that ran for twenty months and closed eighteen months ago, and one with a report still in the queue are not touched.
    /// </summary>
    [Fact]
    public async Task TheRetentionArchivesAClosedTourAndKeepsTheDisciplinaryRecord()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (old, oldLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (_, warning, code) = await RuleWithErrorsAsync(coordinator, old, yearlyMax: 3, token);
        var decided = Id(await CreatedAsync(
            pilot,
            Reports(old),
            Payload(oldLegs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-2))),
            token));
        var page = await TakeAsync(coordinator, decided, token);
        await StepAsync(
            coordinator,
            decided,
            "decide",
            new ReviewDecisionDto(PirepStatus.Accepted, [warning], NoteToPilot: "fo-test note", StaffNote: "fo-test staff", OverrideReason: null, RowVersion(page)),
            token);

        var (recent, recentLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (_, recentWarning, _) = await RuleWithErrorsAsync(coordinator, recent, yearlyMax: 3, token);
        var kept = Id(await CreatedAsync(
            pilot,
            Reports(recent),
            Payload(recentLegs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-2).AddHours(-4))),
            token));
        page = await TakeAsync(coordinator, kept, token);
        await StepAsync(coordinator, kept, "decide", Decision(PirepStatus.Accepted, [recentWarning], RowVersion(page)), token);

        var (queued, queuedLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var waiting = Id(await CreatedAsync(
            pilot,
            Reports(queued),
            Payload(queuedLegs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-2).AddHours(-8))),
            token));

        var (longTour, _) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);

        var now = DateTime.UtcNow;
        string snapshotBefore;
        int eventsBefore;
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();

            // Back in time: what the application would never let a form write (a ready tour closes ahead of today).
            await MoveAsync(database, old, now.AddMonths(-16), now.AddMonths(-14), token);
            await MoveAsync(database, recent, now.AddMonths(-14), now.AddMonths(-12), token);
            await MoveAsync(database, queued, now.AddMonths(-16), now.AddMonths(-14), token);
            await MoveAsync(database, longTour, now.AddMonths(-38), now.AddMonths(-18), token);

            var report = await Everything<Pirep>(database).AsNoTracking().FirstAsync(row => row.Id == kept, token);
            snapshotBefore = report.RulesSnapshotJson;
            eventsBefore = await database.PirepEvents.CountAsync(row => row.PirepId == decided, token);
            Assert.True(await database.Enrolments.AnyAsync(row => row.TourId == old, token));

            var outcome = await scope.ServiceProvider.GetRequiredService<TourRetentionJob>().RunAsync(token);
            Assert.True(outcome.Archived >= 1, $"{outcome}");
            Assert.True(outcome.Waiting >= 1, $"{outcome}");
        }

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            var tours = await Everything<Tour>(database).AsNoTracking()
                .Where(tour => new[] { old, recent, queued, longTour }.Contains(tour.Id))
                .ToDictionaryAsync(tour => tour.Id, token);

            // The old tour: archived and emptied, its legs still there.
            Assert.NotNull(tours[old].PurgedAt);
            Assert.Equal(Tour.EmptyBriefing, tours[old].BriefingJson);
            Assert.False(await database.Rules.AnyAsync(rule => rule.TourId == old, token));
            Assert.False(await database.Enrolments.AnyAsync(row => row.TourId == old, token));
            Assert.Equal(2, await database.Legs.CountAsync(leg => leg.TourId == old, token));

            // Its report: the record stays, the rest goes.
            var report = await Everything<Pirep>(database).AsNoTracking()
                .Include(row => row.Flights)
                .Include(row => row.Errors)
                .FirstAsync(row => row.Id == decided, token);
            Assert.Equal(PirepStatus.Accepted, report.Status);
            Assert.NotNull(report.DecidedByVid);
            Assert.Null(report.NoteToPilot);
            Assert.Null(report.StaffNote);
            Assert.Equal("[]", report.AtcContactsJson);
            var error = Assert.Single(report.Errors);
            Assert.True(error.Confirmed);
            Assert.Equal(warning, error.ErrorId);
            Assert.Equal(eventsBefore, await database.PirepEvents.CountAsync(row => row.PirepId == decided, token));
            var flight = Assert.Single(report.Flights);
            Assert.Equal("[]", flight.FlightPlansJson);
            Assert.False(await database.PirepTracks.AnyAsync(track => track.PirepFlightId == flight.Id, token));
            Assert.False(await database.CheckResults.AnyAsync(result => result.PirepId == decided, token));

            // The snapshot keeps the rule of the confirmed error, with its code and the error's name, and nothing to read.
            var rule = Assert.Single(PirepSubmission.Snapshot(report));
            Assert.Equal(code, rule.Code);
            Assert.Empty(rule.Text);
            Assert.Empty(rule.Parameters);
            Assert.Equal(warning, Assert.Single(rule.Errors).Id);

            // The others: in their window, running for more than a year, or waiting for a validator.
            Assert.Null(tours[recent].PurgedAt);
            Assert.Null(tours[longTour].PurgedAt);
            Assert.Null(tours[queued].PurgedAt);
            Assert.True(await database.Rules.AnyAsync(row => row.TourId == recent, token));
            Assert.Equal(
                snapshotBefore,
                (await Everything<Pirep>(database).AsNoTracking().FirstAsync(row => row.Id == kept, token)).RulesSnapshotJson);
            Assert.Equal(
                PirepStatus.Queued,
                (await Everything<Pirep>(database).AsNoTracking().FirstAsync(row => row.Id == waiting, token)).Status);
        }

        // The staff reads the record, with the error's name.
        var pilotPage = await OkAsync(await coordinator.GetAsync($"/api/flightops/pilots/{PilotVid}", token), token);
        var named = pilotPage.GetProperty("errors").EnumerateArray().Single(row => row.GetProperty("errorId").GetInt64() == warning);
        Assert.Equal("fo-test warning", named.GetProperty("name").GetProperty("en").GetString());

        // The staff still finds the tour, the public no longer does, and nothing on it changes.
        var detail = await OkAsync(await coordinator.GetAsync($"{TourEndpoints.Pattern}/{old}", token), token);
        Assert.NotEqual(JsonValueKind.Null, detail.GetProperty("purgedAt").ValueKind);
        using var anonymous = _host.CreateClient();
        using (var hidden = await anonymous.GetAsync($"{TourEndpoints.Pattern}/public/{detail.GetProperty("slug").GetString()}", token))
        {
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        await RefusedPostAsync(
            coordinator,
            $"{TourEndpoints.Pattern}/{old}/status",
            new TourStatusRequest(TourStatusAction.Hide),
            "status",
            TourState.PurgedKey,
            token);
        await RefusedPostAsync(coordinator, LegsUri(old), Leg(Milan, Rome), "tourId", TourState.PurgedKey, token);

        page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{decided}", token), token);
        Assert.False(page.GetProperty("actions").GetProperty("canReopen").GetBoolean());
        await RefusedStepAsync(
            coordinator,
            decided,
            "reopen",
            new ReviewReopenDto("fo-test", RowVersion(page)),
            "status",
            "flightops:errors.reviewPurged",
            token);
    }

    private static async Task MoveAsync(FlightOpsDbContext database, long tourId, DateTime releaseAt, DateTime closeAt, CancellationToken cancellationToken) =>
        await Everything<Tour>(database).Where(tour => tour.Id == tourId)
            .ExecuteUpdateAsync(set => set.SetProperty(tour => tour.ReleaseAt, releaseAt).SetProperty(tour => tour.CloseAt, closeAt), cancellationToken);

    private static async Task RefusedPostAsync<T>(
        HttpClient client,
        string uri,
        T body,
        string field,
        string key,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(uri, body, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

        var errors = JsonDocument.Parse(text).RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var keys) && keys.EnumerateArray().Any(item => item.GetString() == key), text);
    }
}
