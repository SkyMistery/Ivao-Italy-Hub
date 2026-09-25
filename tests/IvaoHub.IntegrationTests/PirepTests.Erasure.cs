using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Division;
using IvaoHub.Core.Privacy;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Erasing a pilot's data in the tours (design M2 §10.0, T20b; notes 2026-09-25-la-cancellazione-dei-dati-di-una-persona and
/// 2026-09-25-le-righe-che-restano-con-il-vid): the disciplinary record stays without the pilot and counts the same, the rest
/// about them goes, and a ban still in force keeps its VID.
/// </summary>
public sealed partial class PirepTests
{
    private const int ErasedPilotVid = 780099;

    [Fact]
    public async Task AnErasedPilotLeavesTheRecordWithoutTheirNameAndABanInForce()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(ErasedPilotVid, staffPosition: null, rating: 4, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(ErasedPilotVid, token);

        // A decided report with a confirmed error, reopened once with words of the validator's own, and decided again.
        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (_, warning, _) = await RuleWithErrorsAsync(coordinator, tourId, yearlyMax: 3, token);
        var decided = Id(await CreatedAsync(
            pilot,
            Reports(tourId),
            Payload(legs[0], _flights.Add(ErasedPilotVid, "XAE100", Rome, Milan, DateTime.UtcNow.AddDays(-2))),
            token));
        var page = await TakeAsync(coordinator, decided, token);
        page = await StepAsync(
            coordinator,
            decided,
            "decide",
            new ReviewDecisionDto(PirepStatus.Accepted, [warning], NoteToPilot: "fo-test-erasure note", StaffNote: "fo-test-erasure staff", OverrideReason: null, RowVersion(page)),
            token);
        page = await StepAsync(coordinator, decided, "reopen", new ReviewReopenDto("fo-test-erasure the pilot told me why", RowVersion(page)), token);
        await StepAsync(
            coordinator,
            decided,
            "decide",
            new ReviewDecisionDto(PirepStatus.Rejected, [warning], NoteToPilot: null, StaffNote: null, OverrideReason: "fo-test-erasure against the suggestion", RowVersion(page)),
            token);

        // A report still in the queue, on a tour of its own (a rejection holds the next legs), and a problem reported on a leg.
        var (otherTour, otherLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var queued = Id(await CreatedAsync(
            pilot,
            Reports(otherTour),
            Payload(otherLegs[0], _flights.Add(ErasedPilotVid, "XAE200", Rome, Milan, DateTime.UtcNow.AddDays(-1))),
            token));
        using (var reported = await pilot.PostAsJsonAsync(
            $"/api/flightops/tours/{tourId}/legs/{legs[1]}/issues",
            new LegIssueReportDto("fo-test-erasure the airport is closed"),
            token))
        {
            Assert.True(reported.IsSuccessStatusCode, $"The issue answered {(int)reported.StatusCode}.");
        }

        // Two bans: one for good, one over a month ago.
        long inForce;
        long ended;
        int decidedBefore;
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            var now = DateTime.UtcNow;
            var bans = new[]
            {
                new Ban { Vid = ErasedPilotVid, StartsAt = now.AddDays(-1), Reason = "fo-test-erasure in force", OwnerDepartment = Department.FOD },
                new Ban { Vid = ErasedPilotVid, StartsAt = now.AddMonths(-3), EndsAt = now.AddMonths(-1), Reason = "fo-test-erasure over", OwnerDepartment = Department.FOD },
            };
            database.Bans.AddRange(bans);
            await database.SaveChangesAsync(token);
            (inForce, ended) = (bans[0].Id, bans[1].Id);

            decidedBefore = await Everything<Pirep>(database).CountAsync(
                row => row.TourId == tourId && (row.Status == PirepStatus.Accepted || row.Status == PirepStatus.Rejected),
                token);
        }

        try
        {
            using var superadmin = await SignedInAsync(SuperadminPilotVid, token);
            using var response = await superadmin.PostAsync(new Uri($"{ErasureEndpoints.Pattern}/{ErasedPilotVid}", UriKind.Relative), null, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(token);
            var pseudonym = result.GetProperty("pseudonym").GetInt32();

            await using var scope = _host.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();

            // The record: the same reports decided, under the pseudonym, with the confirmed error and the history's steps.
            Assert.Equal(decidedBefore, await Everything<Pirep>(database).CountAsync(
                row => row.TourId == tourId && (row.Status == PirepStatus.Accepted || row.Status == PirepStatus.Rejected),
                token));
            var report = await Everything<Pirep>(database).AsNoTracking()
                .Include(row => row.Flights)
                .Include(row => row.Errors)
                .Include(row => row.Events)
                .SingleAsync(row => row.Id == decided, token);
            Assert.Equal((pseudonym, PirepStatus.Rejected, (int?)CoordinatorVid), (report.Vid, report.Status, report.DecidedByVid));
            Assert.Equal(warning, Assert.Single(report.Errors, error => error.Confirmed).ErrorId);
            Assert.Null(report.NoteToPilot);
            Assert.Null(report.StaffNote);
            Assert.Null(report.OverrideReason);
            var flight = Assert.Single(report.Flights);
            Assert.Equal((string.Empty, 0L, (long?)null, "[]"), (flight.Callsign, flight.TrackerSessionId, flight.ClaimedSessionId, flight.FlightPlansJson));
            Assert.False(await database.PirepTracks.AnyAsync(track => track.PirepFlightId == flight.Id, token));
            Assert.False(await database.CheckResults.AnyAsync(row => row.PirepId == decided, token));
            Assert.NotEmpty(report.Events);
            Assert.All(report.Events, step => Assert.True(step.Note is null || step.Note.StartsWith("flightops:", StringComparison.Ordinal), step.Note));
            Assert.All(report.Events.Where(step => step.ByVid != CoordinatorVid), step => Assert.NotEqual(ErasedPilotVid, step.ByVid));

            // The rest about them: gone.
            Assert.False(await Everything<Pirep>(database).AnyAsync(row => row.Id == queued || row.Vid == ErasedPilotVid, token));
            Assert.False(await database.Enrolments.AnyAsync(row => row.Vid == ErasedPilotVid || row.Vid == pseudonym, token));
            Assert.False(await Everything<LegIssue>(database).AnyAsync(row => row.CreatedBy == ErasedPilotVid || row.CreatedBy == pseudonym, token));

            // The bans: the one in force as it was, the one over without the pilot and without its reason.
            var bansAfter = await Everything<Ban>(database).AsNoTracking()
                .Where(ban => ban.Id == inForce || ban.Id == ended)
                .ToDictionaryAsync(ban => ban.Id, token);
            Assert.Equal((ErasedPilotVid, "fo-test-erasure in force"), (bansAfter[inForce].Vid, bansAfter[inForce].Reason));
            Assert.Equal((pseudonym, string.Empty), (bansAfter[ended].Vid, bansAfter[ended].Reason));

            // The staff still read the record: the validation page answers, with a pilot nobody can name.
            var review = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{decided}", token), token);
            Assert.Equal(pseudonym, review.GetProperty("pilot").GetProperty("vid").GetInt32());
        }
        finally
        {
            await using var scope = _host.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await database.Bans.Where(ban => ban.Id == inForce || ban.Id == ended).ExecuteDeleteAsync(token);
        }
    }
}
