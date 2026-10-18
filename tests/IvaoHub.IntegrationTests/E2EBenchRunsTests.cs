using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What the end to end bench can ask besides signing in (M4, E10w; note <c>decisions/2026-10-18-il-banco-fa-girare-un-job.md</c>),
/// through the real host and the real MariaDB: <c>POST /e2e/jobs/{name}</c> runs a job now — not due, it runs all the same, and
/// writes its row —, and <c>PUT /e2e/sessions/{vid}</c> gives a person of the bench the template's sessions, which the network's
/// client then answers for them. Neither exists on a normal start, nor on the bench's environment without its flag: the same two
/// locks as <c>/e2e/signin</c>.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class E2EBenchRunsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The contract as a spec of the bench calls it, spelled out rather than read from the code (web/e2e/full/bench.ts).
    private const string Jobs = "/e2e/jobs/";
    private const string Sessions = "/e2e/sessions/";

    private const string Probe = "e10w-probe";
    private const string Written = "e10w-test written";
    private const string Seeded = "e10w-test seeded";

    /// <summary>The bench's people, as <c>web/scripts/e2e-server.mjs</c> configures them.</summary>
    private const int Staff = 999001;
    private const int Pilot = 999002;

    /// <summary>The first of January at midnight: the probe's schedule never comes while a test runs.</summary>
    private const string Yearly = "0 0 0 1 1 ?";

    private readonly List<WebApplicationFactory<Program>> _hosts = [];
    private HubWebApplicationFactory _base = null!;

    public async ValueTask InitializeAsync()
    {
        _base = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        await ForgetProbeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }

        await ForgetProbeAsync();
        await _base.DisposeAsync();
    }

    [Fact]
    public async Task NeitherAddressExistsOnANormalStartNorOnTheBenchWithoutItsFlag()
    {
        var token = TestContext.Current.CancellationToken;

        // A development host, which is what every other test of this project runs against; and the bench's environment
        // with its people configured but the flag off — the second lock.
        foreach (var host in new[] { Host(environment: Environments.Development, enabled: false), Host(environment: HubEnvironments.E2E, enabled: false) })
        {
            using var client = host.CreateClient();

            using var job = await client.PostAsync(new Uri($"{Jobs}{Probe}", UriKind.Relative), content: null, token);
            using var given = await client.PutAsJsonAsync(new Uri($"{Sessions}{Pilot}", UriKind.Relative), new { atUtc = DateTime.UtcNow }, token);
            using var taken = await client.DeleteAsync(new Uri($"{Sessions}{Pilot}", UriKind.Relative), token);

            Assert.Equal(HttpStatusCode.NotFound, job.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, given.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, taken.StatusCode);
            Assert.Equal(0, await RunsAsync(token));
        }
    }

    [Fact]
    public async Task TheBenchRunsAJobNowThoughItIsNotDueAndAnswersItsRow()
    {
        var token = TestContext.Current.CancellationToken;

        // It ran a minute ago, and its schedule comes once a year: the hub's pass would not launch it, and a launch would be
        // skipped by the guard as already done.
        await SeedAsync(token);

        using var client = Host(environment: HubEnvironments.E2E, enabled: true).CreateClient();

        for (var run = 1; run <= 2; run++)
        {
            using var response = await client.PostAsync(new Uri($"{Jobs}{Probe}", UriKind.Relative), content: null, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var answer = await response.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal(Probe, answer.GetProperty("job").GetString());
            Assert.Equal("Ran", answer.GetProperty("outcome").GetString());

            // The answer left when the run had ended, and carries the row the run wrote.
            var row = answer.GetProperty("run");
            Assert.Equal("succeeded", row.GetProperty("status").GetString());
            Assert.Equal(Written, row.GetProperty("message").GetString());
            Assert.NotEqual(JsonValueKind.Null, row.GetProperty("finishedAt").ValueKind);
            Assert.Equal(run, await RunsAsync(token));
        }

        // A job the hub has no job of.
        using var unknown = await client.PostAsync(new Uri($"{Jobs}e10w-no-such-job", UriKind.Relative), content: null, token);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task TheBenchRunsOneOfTheHubsOwnJobsWithItsRow()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = Host(environment: HubEnvironments.E2E, enabled: true).CreateClient();

        // One of the core's, which writes a row at every run: its row, not the probe's.
        using var response = await client.PostAsync(new Uri($"{Jobs}media-expiry", UriKind.Relative), content: null, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("Ran", answer.GetProperty("outcome").GetString());
        var row = answer.GetProperty("run");
        Assert.Equal("succeeded", row.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, row.GetProperty("finishedAt").ValueKind);
    }

    [Fact]
    public async Task APersonOfTheBenchIsGivenTheTemplatesSessionsAndTheyAreTakenBack()
    {
        var token = TestContext.Current.CancellationToken;
        var host = Host(environment: HubEnvironments.E2E, enabled: true);
        using var client = host.CreateClient();
        var at = new DateTime(2026, 10, 18, 14, 3, 20, DateTimeKind.Utc);

        using var given = await client.PutAsJsonAsync(new Uri($"{Sessions}{Pilot}", UriKind.Relative), new { atUtc = at }, token);
        Assert.Equal(HttpStatusCode.OK, given.StatusCode);

        // The answer is what the network's client now reads for them: the tower from the moment asked, then the flight.
        var answer = await given.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(Pilot, answer.GetProperty("vid").GetInt32());
        Assert.Equal(
            [("LIRF_TWR", "Atc", at), ("TST001", "Pilot", at.AddMinutes(50).AddSeconds(8))],
            answer.GetProperty("sessions").EnumerateArray().Select(session => (
                session.GetProperty("callsign").GetString(),
                session.GetProperty("connection").GetString(),
                session.GetProperty("startedAt").GetDateTime().ToUniversalTime())));

        // And a module's question, through the core's source, finds the tower's minutes of a shift on that moment.
        var shift = await SessionsAsync(host, new NetworkSessionQuery(Pilot, at.AddHours(-12), at.AddMinutes(15), Connection: NetworkConnection.Atc), token);
        Assert.Equal([("LIRF_TWR", at, at.AddSeconds(2454))], shift!.Select(session => (session.Callsign, session.StartedAt, session.EndedAt)));

        // Nobody else of the bench has them.
        Assert.Empty((await SessionsAsync(host, new NetworkSessionQuery(Staff, at.AddDays(-1), at.AddDays(1)), token))!);

        using var taken = await client.DeleteAsync(new Uri($"{Sessions}{Pilot}", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.NoContent, taken.StatusCode);
        Assert.Empty((await SessionsAsync(host, new NetworkSessionQuery(Pilot, at.AddDays(-1), at.AddDays(1)), token))!);

        // A VID that is none of the bench's people, and a moment left out.
        using var stranger = await client.PutAsJsonAsync(new Uri($"{Sessions}761020", UriKind.Relative), new { atUtc = at }, token);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        using var noMoment = await client.PutAsJsonAsync(new Uri($"{Sessions}{Pilot}", UriKind.Relative), new { }, token);
        Assert.Equal(HttpStatusCode.BadRequest, noMoment.StatusCode);
    }

    // ---- the pieces --------------------------------------------------------------------------------------------------

    /// <summary>
    /// A host in the environment asked, with the bench's people configured, its flag on or off, the probe with a yearly schedule,
    /// and a scheduler of its own name: Quartz keeps one scheduler per name in a process.
    /// </summary>
    private WebApplicationFactory<Program> Host(string environment, bool enabled)
    {
        var host = _base.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["E2E:Enabled"] = enabled ? "true" : "false",
                ["E2E:Vid"] = Staff.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["E2E:FirstName"] = "Bench",
                ["E2E:LastName"] = "Coordinator",
                ["E2E:Pilot:Vid"] = Pilot.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["E2E:Pilot:FirstName"] = "Bench",
                ["E2E:Pilot:LastName"] = "Pilot",
                ["E2E:Pilot:Email"] = "bench-pilot@bench.test",
            }));

            builder.ConfigureTestServices(services => services.AddQuartz(quartz =>
            {
                quartz.SchedulerName = $"e10w-test-{Guid.NewGuid():N}";
                quartz.AddJob<ProbeJob>(detail => detail.WithIdentity(Probe));
                quartz.AddTrigger(trigger => trigger
                    .ForJob(Probe)
                    .WithIdentity($"{Probe}-yearly")
                    .WithCronSchedule(Yearly, schedule => schedule.InTimeZone(TimeZoneInfo.Utc)));
            }));
        });

        _hosts.Add(host);
        return host;
    }

    private static async Task<IReadOnlyList<NetworkSession>?> SessionsAsync(
        WebApplicationFactory<Program> host,
        NetworkSessionQuery query,
        CancellationToken cancellationToken)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<INetworkSessionSource>().SessionsAsync(query, cancellationToken);
    }

    /// <summary>The probe's rows the probe wrote itself.</summary>
    private async Task<int> RunsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _base.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
            .CountAsync(run => run.Job == Probe && run.Message == Written, cancellationToken);
    }

    /// <summary>A run of the probe a minute ago, ended: its yearly occurrence is done.</summary>
    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _base.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var startedAt = DateTime.UtcNow.AddMinutes(-1);
        hub.JobsLog.Add(new JobLogEntry { Job = Probe, StartedAt = startedAt, FinishedAt = startedAt.AddSeconds(1), Status = "succeeded", Message = Seeded });
        await hub.SaveChangesAsync(cancellationToken);
    }

    private async Task ForgetProbeAsync()
    {
        await using var scope = _base.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog
            .Where(run => run.Job == Probe)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    /// <summary>A job of the test, written as the hub's are: its row in the log, the work, the row ended.</summary>
    [DisallowConcurrentExecution]
    internal sealed class ProbeJob(HubDbContext hub, IClock clock) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var entry = new JobLogEntry { Job = Probe, StartedAt = clock.UtcNow, Status = "running", Message = Written };
            hub.JobsLog.Add(entry);
            await hub.SaveChangesAsync(CancellationToken.None);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            await hub.SaveChangesAsync(CancellationToken.None);
        }
    }
}
