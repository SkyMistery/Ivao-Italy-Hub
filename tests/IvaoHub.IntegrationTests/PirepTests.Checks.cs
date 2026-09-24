using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Modules.FlightOps.Checks;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The engine of the checks through the real host (M2, T17): the checks the frozen rules name run at the send and write
/// <c>fo_check_results</c>; the errors of a failed check are suggested on the page and in the queue; the decision keeps
/// whether the validator confirmed them; the job runs them on a report whose checks are missing. And the hole the note of
/// 24 September found: a rejected report keeps its flight.
/// </summary>
public sealed partial class PirepTests
{
    /// <summary>
    /// The shelf's flights file no alternate: a rule of the tour naming <c>alternate</c>, with a dangerous error that check
    /// suggests, fails on every report — the page says why, the queue proposes the rejection, and the validator who accepts
    /// without confirming the error leaves it on the report as a suggestion nobody confirmed.
    /// </summary>
    [Fact]
    public async Task TheChecksRunAtTheSendAndTheirErrorIsSuggestedUntilTheValidatorDecides()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var error = await CheckedRuleAsync(coordinator, tourId, CheckCatalog.Alternate, token);

        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1))), token));

        // The page: the check that failed, its line, the error it suggests — not marked.
        var page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        Assert.NotEqual(JsonValueKind.Null, page.GetProperty("checksRanAt").ValueKind);
        var check = Assert.Single(page.GetProperty("checks").EnumerateArray());
        Assert.Equal(CheckCatalog.Alternate, check.GetProperty("key").GetString());
        Assert.Equal("Failed", check.GetProperty("outcome").GetString());
        Assert.Equal("Server", check.GetProperty("ranBy").GetString());
        Assert.Equal("flightops:evidence.alternateMissing", Assert.Single(check.GetProperty("evidence").EnumerateArray()).GetProperty("key").GetString());
        Assert.Equal([error], check.GetProperty("errorIds").EnumerateArray().Select(item => item.GetInt64()));

        var row = page.GetProperty("errors").EnumerateArray().Single(entry => entry.GetProperty("id").GetInt64() == error);
        Assert.True(row.GetProperty("suggestedByCheck").GetBoolean());
        Assert.False(row.GetProperty("marked").GetBoolean());

        // The queue: one check failed, and a dangerous error suggested proposes the rejection.
        var queued = await QueueRowAsync(coordinator, tourId, id, token);
        Assert.Equal(1, queued.GetProperty("failedChecks").GetInt32());
        Assert.Equal("Rejected", queued.GetProperty("checkSuggestion").GetString());

        // The job runs them again on a report whose checks are gone — as on one sent before the engine existed.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await database.CheckResults.Where(result => result.PirepId == id).ExecuteDeleteAsync(token);
            await Everything<Pirep>(database).Where(report => report.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(report => report.ChecksRanAt, (DateTime?)null), token);

            Assert.True(await scope.ServiceProvider.GetRequiredService<FlightCheckJob>().RunAsync(token) >= 1);
            Assert.Equal(CheckOutcome.Failed, (await database.CheckResults.AsNoTracking().SingleAsync(result => result.PirepId == id, token)).Outcome);
        }

        // Accepted without confirming it: the suggestion stays on the report, unconfirmed, and counts for nothing.
        page = await TakeAsync(coordinator, id, token);
        page = await StepAsync(coordinator, id, "decide", Decision(PirepStatus.Accepted, [], RowVersion(page)), token);
        Assert.Equal("Accepted", page.GetProperty("status").GetString());
        Assert.False(page.GetProperty("thresholdOverridden").GetBoolean());

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            var kept = await database.PirepErrors.AsNoTracking().SingleAsync(entry => entry.PirepId == id, token);
            Assert.Equal(error, kept.ErrorId);
            Assert.True(kept.SuggestedByCheck);
            Assert.False(kept.Confirmed);
        }

        var read = await OkAsync(await pilot.GetAsync($"{PirepEndpoints.Pattern}/{id}", token), token);
        Assert.Empty(read.GetProperty("violatedRules").EnumerateArray());
    }

    /// <summary>
    /// A rejected report holds its flight (design M2 §3.4): the pilot flies the leg again, and cannot send the same flight a
    /// second time (note 2026-09-24-i-controlli-dai-pirep-veri §4 — the old system let a controller find it by eye).
    /// </summary>
    [Fact]
    public async Task TheFlightOfARejectedReportCannotBeReportedAgain()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (dangerous, _, _) = await RuleWithErrorsAsync(coordinator, tourId, yearlyMax: 3, token);
        var flown = _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1));
        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], flown), token));

        var page = await TakeAsync(coordinator, id, token);
        await StepAsync(coordinator, id, "decide", Decision(PirepStatus.Rejected, [dangerous], RowVersion(page)), token);

        var offered = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/sessions?legId={legs[0]}", token), token);
        Assert.DoesNotContain(offered.EnumerateArray(), session => session.GetProperty("id").GetInt64() == flown);
        await RefusedAsync(pilot, Reports(tourId), Payload(legs[0], flown), "sessionIds", "flightops:errors.reportSessionClaimed", token);
    }

    /// <summary>A rule of the tour naming a check, with a dangerous error of the catalogue that check suggests.</summary>
    private async Task<long> CheckedRuleAsync(HttpClient coordinator, long tourId, string check, CancellationToken cancellationToken)
    {
        var error = Id(await CreatedAsync(
            coordinator,
            RuleEndpoints.ErrorsPattern,
            new TourErrorWriteDto(Text($"fo-test {check}"), Text("fo-test"), null, ErrorCategory.Dangerous, null, check, false, false, default),
            cancellationToken));
        _errors.Add(error);

        var code = $"C{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        _rules.Add(Id(await CreatedAsync(
            coordinator,
            RuleEndpoints.RulesPattern,
            new TourRuleWriteDto(tourId, code, Text(code), Text($"The rule {code}."), null, check, new JsonObject(), [error], 0, false, default),
            cancellationToken)));

        return error;
    }
}
