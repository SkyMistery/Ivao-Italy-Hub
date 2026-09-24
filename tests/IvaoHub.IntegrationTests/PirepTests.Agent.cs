using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Modules.FlightOps.Agent;
using IvaoHub.Modules.FlightOps.Checks;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Rules;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The agent on the validator's computer through the real host (M2, T19b; note 2026-09-24-il-contratto-dell-agente): a test
/// agent with a token made from <c>/me/tokens</c> reads the queue and a report, sends what its check found, and the validation
/// page shows it as a suggestion — the «done when» of T19 without the browser. And every refusal of the contract.
/// </summary>
public sealed partial class PirepTests
{
    [Fact]
    public async Task AnAgentReadsAReportAndItsFailedCheckIsSuggestedOnThePage()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var error = await CheckedRuleAsync(coordinator, tourId, CheckCatalog.SemicircularLevels, token);
        var takeoff = DateTime.UtcNow.AddDays(-1).AddMinutes(-11);
        takeoff = takeoff.AddTicks(-(takeoff.Ticks % TimeSpan.TicksPerSecond));
        var flown = _flights.Add(PilotVid, "XAA190", Rome, Milan, takeoff, "I", Track(takeoff, (41.8003, 12.2389), (45.4451, 9.27674)));
        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], flown), token));

        await GrantValidateAsync(ValidatorVid, tourId, token);
        try
        {
            using var validator = await SignedInAsync(ValidatorVid, token);
            using var agent = await AgentAsync(validator, token);

            // The queue: the report, with the check it waits for and no agent's result yet — also among the pending ones.
            var queue = await AgentOkAsync(agent, $"{AgentEndpoints.PirepsPattern}?pending=true", token);
            var item = Assert.Single(queue.EnumerateArray(), entry => entry.GetProperty("id").GetInt64() == id);
            Assert.Equal([CheckCatalog.SemicircularLevels], item.GetProperty("agentChecks").EnumerateArray().Select(key => key.GetString()));
            Assert.Equal(JsonValueKind.Null, item.GetProperty("agentRanAt").ValueKind);
            Assert.Equal(PilotVid, item.GetProperty("pilotVid").GetInt32());

            // The report as the agent reads it: the plan, the track, the check asked for, no archive of controllers here.
            var read = await AgentOkAsync(agent, $"{AgentEndpoints.PirepsPattern}/{id}", token);
            var flight = Assert.Single(read.GetProperty("flights").EnumerateArray());
            Assert.Equal("XAA190", flight.GetProperty("callsign").GetString());
            Assert.NotEmpty(flight.GetProperty("plans").EnumerateArray());
            Assert.True(flight.GetProperty("track").GetArrayLength() > 100);
            Assert.Equal(CheckCatalog.SemicircularLevels, Assert.Single(read.GetProperty("checks").EnumerateArray()).GetProperty("key").GetString());
            Assert.Contains(read.GetProperty("airports").EnumerateArray(), airport => airport.GetProperty("icao").GetString() == Milan);
            Assert.False(read.GetProperty("atc").GetProperty("available").GetBoolean());
            Assert.Empty(read.GetProperty("results").EnumerateArray());

            // What the agent found: the level is wrong on a segment.
            var written = await AgentOkAsync(
                await agent.PostAsJsonAsync(
                    $"{AgentEndpoints.PirepsPattern}/{id}/checks",
                    Results("1.4.0", (CheckCatalog.SemicircularLevels, "Failed", ["DCT ELB to SRN, magnetic track 332, FL360 even: wrong"])),
                    token),
                token);
            Assert.Equal([error], written.GetProperty("suggestedErrorIds").EnumerateArray().Select(entry => entry.GetInt64()));

            // The page: the agent's result, whose and which version, and the error it suggests — not marked.
            var page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
            var check = Assert.Single(page.GetProperty("checks").EnumerateArray(), entry => entry.GetProperty("key").GetString() == CheckCatalog.SemicircularLevels);
            Assert.Equal("Agent", check.GetProperty("ranBy").GetString());
            Assert.Equal("Failed", check.GetProperty("outcome").GetString());
            Assert.Equal(ValidatorVid, check.GetProperty("by").GetProperty("vid").GetInt32());
            Assert.Equal("1.4.0", check.GetProperty("agentVersion").GetString());
            Assert.Equal("DCT ELB to SRN, magnetic track 332, FL360 even: wrong", Assert.Single(check.GetProperty("evidence").EnumerateArray()).GetProperty("text").GetString());
            var suggested = page.GetProperty("errors").EnumerateArray().Single(entry => entry.GetProperty("id").GetInt64() == error);
            Assert.True(suggested.GetProperty("suggestedByCheck").GetBoolean());
            Assert.False(suggested.GetProperty("marked").GetBoolean());
            Assert.Equal(1, (await QueueRowAsync(coordinator, tourId, id, token)).GetProperty("failedChecks").GetInt32());

            // No longer pending; sending again replaces the result, and a pass takes the suggestion back.
            Assert.DoesNotContain((await AgentOkAsync(agent, $"{AgentEndpoints.PirepsPattern}?pending=true", token)).EnumerateArray(), entry => entry.GetProperty("id").GetInt64() == id);
            written = await AgentOkAsync(
                await agent.PostAsJsonAsync(
                    $"{AgentEndpoints.PirepsPattern}/{id}/checks",
                    Results("1.4.1", (CheckCatalog.SemicircularLevels, "Passed", ["FL360 on every DCT segment: correct"])),
                    token),
                token);
            Assert.Empty(written.GetProperty("suggestedErrorIds").EnumerateArray());
            var result = Assert.Single(written.GetProperty("results").EnumerateArray());
            Assert.Equal("1.4.1", result.GetProperty("agentVersion").GetString());

            await using (var scope = _host.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
                var row = await database.CheckResults.AsNoTracking().SingleAsync(entry => entry.PirepId == id && entry.RanBy == CheckRanBy.Agent, token);
                Assert.Equal(CheckOutcome.Passed, row.Outcome);
                Assert.Equal(ValidatorVid, row.ByVid);
                Assert.NotNull(row.TokenId);
                Assert.False(await database.PirepErrors.AnyAsync(entry => entry.PirepId == id, token));
            }
        }
        finally
        {
            await CleanTokensAsync(token);
        }
    }

    [Fact]
    public async Task TheAgentsContractRefusesWhatItDoesNotSpeak()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        await CheckedRuleAsync(coordinator, tourId, CheckCatalog.SemicircularLevels, token);
        var (otherTourId, otherLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], _flights.Add(PilotVid, "XAA191", Rome, Milan, DateTime.UtcNow.AddDays(-1))), token));
        var elsewhere = Id(await CreatedAsync(pilot, Reports(otherTourId), Payload(otherLegs[0], _flights.Add(PilotVid, "XAA192", Rome, Milan, DateTime.UtcNow.AddDays(-2))), token));

        await GrantValidateAsync(ValidatorVid, tourId, token);
        try
        {
            // The contract is open to anybody, and says which checks are whose.
            using (var anonymous = _host.CreateClient())
            {
                var contract = await AgentOkAsync(await anonymous.GetAsync(AgentEndpoints.ContractPattern, token), token);
                Assert.Equal(AgentContract.Current, contract.GetProperty("current").GetInt32());
                Assert.Contains(CheckCatalog.SemicircularLevels, contract.GetProperty("agentChecks").EnumerateArray().Select(key => key.GetString()));
                Assert.Contains(CheckCatalog.Speed250, contract.GetProperty("serverChecks").EnumerateArray().Select(key => key.GetString()));
            }

            using var validator = await SignedInAsync(ValidatorVid, token);
            using var agent = await AgentAsync(validator, token);
            var uri = $"{AgentEndpoints.PirepsPattern}/{id}";

            // No version, or one the hub does not speak: 400 with the accepted ones.
            agent.DefaultRequestHeaders.Remove(AgentContract.Header);
            foreach (var version in new[] { null, "2", "one" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (version is not null)
                {
                    request.Headers.Add(AgentContract.Header, version);
                }

                using var refused = await agent.SendAsync(request, token);
                Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
                var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(token);
                Assert.Equal([1], problem.GetProperty("accepted").EnumerateArray().Select(entry => entry.GetInt32()));
            }

            agent.DefaultRequestHeaders.Add(AgentContract.Header, "1");

            // The cookie does not open the agent's endpoints: they are the token's.
            validator.DefaultRequestHeaders.Add(AgentContract.Header, "1");
            using (var withCookie = await validator.GetAsync(uri, token))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, withCookie.StatusCode);
            }

            // A tour the validator is not enabled on: neither read nor written, and not in the queue.
            using (var notEnabled = await agent.GetAsync($"{AgentEndpoints.PirepsPattern}/{elsewhere}", token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, notEnabled.StatusCode);
            }

            Assert.DoesNotContain((await AgentOkAsync(agent, AgentEndpoints.PirepsPattern, token)).EnumerateArray(), entry => entry.GetProperty("id").GetInt64() == elsewhere);

            // What the contract refuses, field by field.
            await AgentRefusedAsync(agent, id, Results("1.0", (CheckCatalog.Speed250, "Failed", [])), "results[0].checkKey", "flightops:errors.agentCheckKeyServer", token);
            await AgentRefusedAsync(agent, id, Results("1.0", ("Bad key", "Failed", [])), "results[0].checkKey", "flightops:errors.agentCheckKey", token);
            await AgentRefusedAsync(agent, id, Results("1.0", ("routeAdherence", "Passed", []), ("routeAdherence", "Passed", [])), "results[1].checkKey", "flightops:errors.agentCheckKeyTwice", token);
            await AgentRefusedAsync(agent, id, Results("1.0", ("routeAdherence", "1", [])), "results[0].outcome", "flightops:errors.agentOutcome", token);
            await AgentRefusedAsync(agent, id, Results("1.0", ("routeAdherence", "Passed", [new string('x', AgentContract.MaxEvidenceCharacters + 1)])), "results[0].evidence", "flightops:errors.agentEvidence", token);
            await AgentRefusedAsync(agent, id, Results(null, ("routeAdherence", "Passed", [])), "agentVersion", "flightops:errors.agentVersion", token);
            await AgentRefusedAsync(agent, id, new AgentChecksWriteDto("1.0", []), "results", "flightops:errors.agentResults", token);

            // A key the catalogue does not know is kept, and suggests nothing: the contract does not know the checks.
            var kept = await AgentOkAsync(
                await agent.PostAsJsonAsync($"{uri}/checks", Results("1.0", ("routeAdherence", "Failed", ["Left the route after ELB."])), token),
                token);
            Assert.Empty(kept.GetProperty("suggestedErrorIds").EnumerateArray());

            // Decided: 409, and the result stays as it was.
            var page = await TakeAsync(coordinator, id, token);
            await StepAsync(coordinator, id, "decide", Decision(PirepStatus.Accepted, [], RowVersion(page)), token);
            using (var decided = await agent.PostAsJsonAsync($"{uri}/checks", Results("1.0", ("routeAdherence", "Passed", [])), token))
            {
                Assert.Equal(HttpStatusCode.Conflict, decided.StatusCode);
                Assert.Equal("agentNotWaiting", (await decided.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("code").GetString());
            }

            // Their own report: the handler says no, even to a validator of the tour.
            var own = Id(await CreatedAsync(validator, Reports(tourId), Payload(legs[0], _flights.Add(ValidatorVid, "XAA193", Rome, Milan, DateTime.UtcNow.AddDays(-1))), token));
            using (var theirOwn = await agent.PostAsJsonAsync($"{AgentEndpoints.PirepsPattern}/{own}/checks", Results("1.0", ("routeAdherence", "Passed", [])), token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, theirOwn.StatusCode);
            }

            Assert.DoesNotContain((await AgentOkAsync(agent, AgentEndpoints.PirepsPattern, token)).EnumerateArray(), entry => entry.GetProperty("id").GetInt64() == own);
        }
        finally
        {
            await CleanTokensAsync(token);
            await using var scope = _host.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await Everything<Pirep>(database).Where(report => report.Vid == ValidatorVid).ExecuteDeleteAsync(token);
        }
    }

    // ---- the test agent ----------------------------------------------------------------------------

    /// <summary>A program with a token of the agent's audience, made from the member's own page, speaking version 1.</summary>
    private async Task<HttpClient> AgentAsync(HttpClient member, CancellationToken cancellationToken)
    {
        using var created = await member.PostAsJsonAsync(
            PersonalTokenEndpoints.Pattern,
            new { name = "fo-test agent", audience = AgentContract.Audience, days = 1 },
            cancellationToken);
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync(cancellationToken));
        var text = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("token").GetString()!;

        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", text);
        client.DefaultRequestHeaders.Add(AgentContract.Header, "1");
        return client;
    }

    private static async Task<JsonElement> AgentOkAsync(HttpClient agent, string uri, CancellationToken cancellationToken) =>
        await AgentOkAsync(await agent.GetAsync(uri, cancellationToken), cancellationToken);

    private static async Task<JsonElement> AgentOkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
    }

    private static async Task AgentRefusedAsync(HttpClient agent, long id, AgentChecksWriteDto body, string field, string key, CancellationToken cancellationToken)
    {
        using var refused = await agent.PostAsJsonAsync($"{AgentEndpoints.PirepsPattern}/{id}/checks", body, cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var errors = (await refused.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var messages), errors.ToString());
        Assert.Contains(key, messages.EnumerateArray().Select(message => message.GetString()));
    }

    private static AgentChecksWriteDto Results(string? version, params (string Key, string Outcome, string[] Evidence)[] results) =>
        new(version, [.. results.Select(result => new AgentCheckWriteDto(result.Key, result.Outcome, result.Evidence))]);

    private async Task CleanTokensAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await database.PersonalTokens.Where(row => row.Vid == ValidatorVid).ExecuteDeleteAsync(cancellationToken);
    }
}
