using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Weather;
using IvaoHub.Modules.FlightOps.Checks;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
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
    /// The weather that does not answer at the send is not a report without its checks: they still run on it, and the queue
    /// proposes at once what they found — before, the failed fill let go of the report, the checks saved their results but
    /// not their suggestion, and the queue said nothing until the job came round (the round on the bench, 25 September 2026).
    /// </summary>
    [Fact]
    public async Task TheChecksOfASendSurviveAWeatherThatDoesNotAnswer()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var error = await CheckedRuleAsync(coordinator, tourId, CheckCatalog.Alternate, token);
        _weather.HistoryFails = true;

        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], _flights.Add(PilotVid, "XAA105", Rome, Milan, DateTime.UtcNow.AddDays(-1))), token));
        Assert.NotEmpty(_weather.HistoryAsked);

        var page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        Assert.NotEqual(JsonValueKind.Null, page.GetProperty("checksRanAt").ValueKind);
        Assert.True(page.GetProperty("errors").EnumerateArray()
            .Single(entry => entry.GetProperty("id").GetInt64() == error)
            .GetProperty("suggestedByCheck").GetBoolean());

        var queued = await QueueRowAsync(coordinator, tourId, id, token);
        Assert.Equal(1, queued.GetProperty("failedChecks").GetInt32());
        Assert.Equal("Rejected", queued.GetProperty("checkSuggestion").GetString());
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

    /// <summary>
    /// The checks on the tracks (T18) read what the engine gathers: the airports' positions, the METARs the send kept, the
    /// track. A VFR flight from Rome that flies forty minutes to Milan at 5000 ft and 375 kt lands where it should and stays
    /// low, is too fast below FL100, and arrives in fog.
    /// </summary>
    [Fact]
    public async Task TheChecksOnTheTracksReadThePlacesTheWeatherAndTheTrack()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        foreach (var check in new[] { CheckCatalog.LandingAtArrival, CheckCatalog.Speed250, CheckCatalog.Vmc, CheckCatalog.MaxAltitude })
        {
            await CheckedRuleAsync(coordinator, tourId, check, token);
        }

        var takeoff = DateTime.UtcNow.AddDays(-1).AddMinutes(-11);
        takeoff = takeoff.AddTicks(-(takeoff.Ticks % TimeSpan.TicksPerSecond));
        var flown = _flights.Add(PilotVid, "XAA180", Rome, Milan, takeoff, "V", Track(takeoff, (41.8003, 12.2389), (45.4451, 9.27674)));

        _weather.AddHistory(new WeatherReport(Rome, WeatherReportKind.Metar, takeoff.AddMinutes(-10), $"METAR {Rome} {takeoff:ddHHmm}Z 00000KT 9999 FEW040 20/10 Q1015", WeatherSourceName));
        _weather.AddHistory(new WeatherReport(Milan, WeatherReportKind.Metar, takeoff.AddMinutes(35), $"METAR {Milan} {takeoff:ddHHmm}Z 00000KT 0800 FG VV002 12/12 Q1015", WeatherSourceName));

        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], flown), token));

        var page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        var checks = page.GetProperty("checks").EnumerateArray().ToDictionary(check => check.GetProperty("key").GetString()!);
        string Outcome(string key) => checks[key].GetProperty("outcome").GetString()!;
        IEnumerable<string?> Keys(string key) => checks[key].GetProperty("evidence").EnumerateArray().Select(line => line.GetProperty("key").GetString());

        Assert.Equal("Passed", Outcome(CheckCatalog.LandingAtArrival));
        Assert.Contains("flightops:evidence.landedAt", Keys(CheckCatalog.LandingAtArrival));
        Assert.Equal("Failed", Outcome(CheckCatalog.Speed250));
        Assert.Contains("flightops:evidence.speed250Exceeded", Keys(CheckCatalog.Speed250));
        Assert.Equal("Failed", Outcome(CheckCatalog.Vmc));
        Assert.Equal(["flightops:evidence.vmcMet", "flightops:evidence.vmcNotMet"], Keys(CheckCatalog.Vmc));
        Assert.Equal("Passed", Outcome(CheckCatalog.MaxAltitude));

        var queued = await QueueRowAsync(coordinator, tourId, id, token);
        Assert.Equal(2, queued.GetProperty("failedChecks").GetInt32());
    }

    /// <summary>
    /// A track in the tracker's shape: five minutes standing, a roll, forty minutes at 5000 ft on the great circle — the ground
    /// speed the positions give, so the simulation rate is one —, the landing, five minutes standing.
    /// </summary>
    private static IReadOnlyList<IvaoTrackPointDto> Track(DateTime takeoff, (double Lat, double Lon) from, (double Lat, double Lon) to)
    {
        var points = new List<IvaoTrackPointDto>();
        for (var second = -300; second < -45; second += 15)
        {
            points.Add(new(takeoff.AddSeconds(second), from.Lat, from.Lon, 50, 0, 320, OnGround: true, "Boarding", "2000"));
        }

        points.Add(new(takeoff.AddSeconds(-30), from.Lat, from.Lon, 50, 20, 320, OnGround: true, "Departing", "2000"));
        points.Add(new(takeoff.AddSeconds(-15), from.Lat, from.Lon, 50, 90, 320, OnGround: true, "Departing", "2000"));

        const int Minutes = 40;
        var nm = GreatCircle.DistanceNm(new GeoPoint(from.Lat, from.Lon), new GeoPoint(to.Lat, to.Lon));
        var speed = (int)Math.Round(nm / Minutes * 60);
        for (var second = 0; second < Minutes * 60; second += 15)
        {
            var share = second / (Minutes * 60.0);
            points.Add(new(
                takeoff.AddSeconds(second),
                from.Lat + ((to.Lat - from.Lat) * share),
                from.Lon + ((to.Lon - from.Lon) * share),
                5000,
                speed,
                320,
                OnGround: false,
                "En Route",
                "2000"));
        }

        for (var second = 0; second <= 300; second += 15)
        {
            points.Add(new(takeoff.AddMinutes(Minutes).AddSeconds(second), to.Lat, to.Lon, 400, second == 0 ? 60 : 0, 320, OnGround: true, "On Blocks", "2000"));
        }

        return points;
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
