using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Impl.Matchers;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The scheduled jobs once per occurrence whichever process is alive (note
/// <c>decisions/2026-10-09-i-job-che-recuperano.md</c>), through the real host and the real MariaDB: a run lost while no
/// process was alive is made up a few seconds after the start, and a job not due is not; of two processes one runs a job and
/// the other leaves it; a run done for its occurrence is not done again; and the scheduled task's address runs what is due
/// inside its request, with the installation's token and only with it.
/// <para>Two probe jobs of the test's own stand for the hub's, with a schedule that comes once a year: what makes one due is
/// the row of its last run the test writes first. Every other job of the test's hosts is paused before the hub's first
/// check, so nothing of the hub runs here, and no service outside is called. Each host has a scheduler of its own name:
/// Quartz keeps one scheduler per name in a process, and two hosts here are two processes.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ScheduledJobsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The contract as an installation writes it, spelled out rather than read from the code (docs/DEPLOYING.md): the address
    // the scheduled task calls, the keys of the secrets file, and the name of a job's lock in the database.
    private const string Address = "/api/jobs/run";
    private const string CatchUpKey = "Jobs:CatchUp";
    private const string TokenKey = "Jobs:Token";

    private const string Token = "e10j-test-token-of-the-installation-0123456789";

    private readonly Probe _probe = new();
    private readonly List<WebApplicationFactory<Program>> _hosts = [];
    private HubWebApplicationFactory _base = null!;

    public async ValueTask InitializeAsync()
    {
        _base = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        await ForgetProbesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // A probe still inside its gate would hold the stop of its host for ever.
        _probe.Open();

        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }

        await ForgetProbesAsync();
        await _base.DisposeAsync();
    }

    [Fact]
    public async Task ARunLostWhileNoProcessWasAliveIsMadeUpAfterTheStartAndOnlyThatOne()
    {
        var token = TestContext.Current.CancellationToken;

        // A first of January has gone by since the last run of one, and none since the last run of the other.
        await LastRunAsync(Probe.Due, DateTime.UtcNow.AddYears(-2), token);
        await LastRunAsync(Probe.NotDue, DateTime.UtcNow.AddMinutes(-1), token);

        var recorder = new Recorder();
        var host = Host(catchUp: true, token: null, recorder);
        _ = host.Services;

        // The first check comes a few seconds after the start, by itself: nobody asks for it.
        await recorder.CountAsync(Probe.Due, 1).WaitAsync(TimeSpan.FromSeconds(60), token);

        // The one not due was not launched: no launch waiting, no run and no skip of it.
        await Task.Delay(TimeSpan.FromSeconds(1), token);
        var scheduler = await SchedulerOf(host, token);
        Assert.All(
            await scheduler.GetTriggersOfJob(new JobKey(Probe.NotDue), token),
            trigger => Assert.IsAssignableFrom<ICronTrigger>(trigger));
        Assert.Empty(recorder.Of(Probe.NotDue));
        Assert.Equal(0, _probe.Runs(Probe.NotDue));

        // And the one due ran once.
        Assert.Equal(1, _probe.Runs(Probe.Due));
        Assert.Equal(["ran"], recorder.Of(Probe.Due));
    }

    [Fact]
    public async Task OfTwoProcessesOneRunsTheJobAndTheOtherLeavesItToIt()
    {
        var token = TestContext.Current.CancellationToken;
        await LastRunAsync(Probe.Due, DateTime.UtcNow.AddYears(-2), token);

        var first = new Recorder();
        var second = new Recorder();
        var one = await SchedulerOf(Host(catchUp: false, token: null, first), token);
        var other = await SchedulerOf(Host(catchUp: false, token: null, second), token);

        // The first process runs it, and the run stays inside the gate.
        _probe.Close();
        await one.TriggerJob(new JobKey(Probe.Due), token);
        await _probe.StartedAsync(Probe.Due, run: 1).WaitAsync(TimeSpan.FromSeconds(30), token);

        // The second launches it meanwhile: it leaves the run to the first — or, without one runner, it runs it beside.
        await other.TriggerJob(new JobKey(Probe.Due), token);
        await Task.WhenAny(second.CountAsync(Probe.Due, 1), _probe.StartedAsync(Probe.Due, run: 2)).WaitAsync(TimeSpan.FromSeconds(30), token);

        _probe.Open();
        await first.CountAsync(Probe.Due, 1).WaitAsync(TimeSpan.FromSeconds(30), token);

        Assert.Equal(1, _probe.Runs(Probe.Due));
        Assert.Equal(["ran"], first.Of(Probe.Due));
        Assert.Equal(["skipped"], second.Of(Probe.Due));

        // Once it has run, the second process finds the occurrence done by the first: launched there again, with the lock
        // free, it is skipped again. The digest of 07:00 goes once a day, whichever process sends it.
        await LockFreeAsync(Probe.Due, token);
        await other.TriggerJob(new JobKey(Probe.Due), token);
        await second.CountAsync(Probe.Due, 2).WaitAsync(TimeSpan.FromSeconds(30), token);

        Assert.Equal(1, _probe.Runs(Probe.Due));
        Assert.Equal(["skipped", "skipped"], second.Of(Probe.Due));
    }

    [Fact]
    public async Task ARunDoneForItsOccurrenceIsNotDoneAgain()
    {
        var token = TestContext.Current.CancellationToken;
        await LastRunAsync(Probe.Due, DateTime.UtcNow.AddYears(-2), token);

        var recorder = new Recorder();
        var scheduler = await SchedulerOf(Host(catchUp: false, token: null, recorder), token);

        await scheduler.TriggerJob(new JobKey(Probe.Due), token);
        await recorder.CountAsync(Probe.Due, 1).WaitAsync(TimeSpan.FromSeconds(30), token);

        // Launched again before its next occurrence — Quartz at its hour, the check of the minute, the scheduled task.
        await scheduler.TriggerJob(new JobKey(Probe.Due), token);
        await recorder.CountAsync(Probe.Due, 2).WaitAsync(TimeSpan.FromSeconds(30), token);

        Assert.Equal(1, _probe.Runs(Probe.Due));
        Assert.Equal(["ran", "skipped"], recorder.Of(Probe.Due));
    }

    [Fact]
    public async Task TheScheduledTasksAddressRefusesACallWithoutTheToken()
    {
        var token = TestContext.Current.CancellationToken;
        await LastRunAsync(Probe.Due, DateTime.UtcNow.AddYears(-2), token);

        using var client = Host(catchUp: false, token: Token, new Recorder()).CreateClient();

        // Neither the token nor the header of the hub's own client: the guard of /api stops it before the address.
        using (var none = await client.PostAsync(new Uri(Address, UriKind.Relative), content: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, none.StatusCode);
        }

        // Another token: refused, saying how to authenticate.
        using (var wrong = await RunAsync(client, Token + "-not-it", token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.Equal("Bearer", wrong.Headers.WwwAuthenticate.ToString());
        }

        Assert.Equal(0, _probe.Runs(Probe.Due));

        // An installation that has no token has no such address.
        using var without = Host(catchUp: false, token: null, new Recorder()).CreateClient();
        using (var missing = await RunAsync(without, Token, token))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        Assert.Equal(0, _probe.Runs(Probe.Due));
    }

    [Fact]
    public async Task TheScheduledTaskRunsWhatIsDueInsideItsRequest()
    {
        var token = TestContext.Current.CancellationToken;
        await LastRunAsync(Probe.Due, DateTime.UtcNow.AddYears(-2), token);
        await LastRunAsync(Probe.NotDue, DateTime.UtcNow.AddMinutes(-1), token);

        using var client = Host(catchUp: false, token: Token, new Recorder()).CreateClient();

        // The run stays inside the gate: the answer waits for it.
        _probe.Close();
        var call = RunAsync(client, Token, token);
        await _probe.StartedAsync(Probe.Due, run: 1).WaitAsync(TimeSpan.FromSeconds(30), token);
        await Task.Delay(TimeSpan.FromMilliseconds(500), token);
        Assert.False(call.IsCompleted);

        _probe.Open();
        using var response = await call;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jobs = (await response.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("jobs").EnumerateArray().ToList();
        var due = Assert.Single(jobs, job => job.GetProperty("job").GetString() == Probe.Due);
        Assert.Equal("Ran", due.GetProperty("outcome").GetString());
        Assert.DoesNotContain(jobs, job => job.GetProperty("job").GetString() == Probe.NotDue);

        // The run had ended when the answer left: its own row says so.
        await using var scope = _base.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
            .Where(run => run.Job == Probe.Due && run.Message == Probe.Written)
            .ToListAsync(token);
        Assert.NotNull(Assert.Single(rows).FinishedAt);

        Assert.Equal(1, _probe.Runs(Probe.Due));
        Assert.Equal(0, _probe.Runs(Probe.NotDue));
    }

    // ---- the pieces --------------------------------------------------------------------------------------------------

    /// <summary>
    /// A host of the test's own: the two probes and a recorder of what became of their runs, every other job paused, and
    /// the jobs' settings as the test says.
    /// </summary>
    private WebApplicationFactory<Program> Host(bool catchUp, string? token, Recorder recorder)
    {
        var host = _base.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [CatchUpKey] = catchUp ? "true" : "false",
                    [TokenKey] = token,
                }));

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_probe);
                services.AddQuartz(quartz =>
                {
                    quartz.SchedulerName = $"e10j-test-{Guid.NewGuid():N}";

                    foreach (var job in Probe.Jobs)
                    {
                        quartz.AddJob<ProbeJob>(detail => detail.WithIdentity(job));
                        quartz.AddTrigger(trigger => trigger
                            .ForJob(job)
                            .WithIdentity($"{job}-yearly")
                            .WithCronSchedule(Probe.Yearly, schedule => schedule.InTimeZone(TimeZoneInfo.Utc)));
                    }

                    quartz.AddJobListener(recorder, GroupMatcher<JobKey>.AnyGroup());
                });

                // After Quartz's own service, and so before the hub's first check, which waits for the start to end.
                services.AddHostedService<OnlyTheProbes>();
            });
        });

        _hosts.Add(host);
        return host;
    }

    private static async Task<IScheduler> SchedulerOf(WebApplicationFactory<Program> host, CancellationToken cancellationToken) =>
        await host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(cancellationToken);

    private static async Task<HttpResponseMessage> RunAsync(HttpClient client, string bearer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Address, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>The row of a run that ended, written as a job writes it.</summary>
    private async Task LastRunAsync(string job, DateTime startedAt, CancellationToken cancellationToken)
    {
        await using var scope = _base.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        hub.JobsLog.Add(new JobLogEntry
        {
            Job = job,
            StartedAt = startedAt,
            FinishedAt = startedAt.AddSeconds(1),
            Status = "succeeded",
            Message = Probe.Seeded,
        });

        await hub.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Until nobody holds the job's lock: Quartz tells the listeners of a job that it ran before it tells the trigger's,
    /// and the guard gives the lock back in the second.
    /// </summary>
    private async Task LockFreeAsync(string job, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnector.MySqlConnection(mariaDb.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlConnector.MySqlCommand("SELECT IS_FREE_LOCK(@name)", connection);
        command.Parameters.AddWithValue("@name", $"hub-job:{connection.Database}:{job}");

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            Assert.True(DateTime.UtcNow < deadline, $"The lock of {job} was still held after 30 seconds.");
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private async Task ForgetProbesAsync()
    {
        await using var scope = _base.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog
            .Where(run => Probe.Jobs.Contains(run.Job))
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    /// <summary>What the probes did, shared by every host of a test.</summary>
    internal sealed class Probe
    {
        public const string Due = "e10j-probe-due";
        public const string NotDue = "e10j-probe-not-due";
        public static readonly string[] Jobs = [Due, NotDue];

        /// <summary>The first of January at midnight: the schedule never comes while a test runs.</summary>
        public const string Yearly = "0 0 0 1 1 ?";

        /// <summary>The message of the rows a test writes, and of those a probe writes.</summary>
        public const string Seeded = "e10j-test seeded";
        public const string Written = "e10j-test written";

        private readonly ConcurrentDictionary<string, int> _runs = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<(string Job, int Run), TaskCompletionSource> _started = new();
        private TaskCompletionSource _gate = Opened();

        public int Runs(string job) => _runs.GetValueOrDefault(job);

        /// <summary>What a run waits for before it ends: open unless a test closes it.</summary>
        public Task Gate => Volatile.Read(ref _gate).Task;

        public void Close() => Volatile.Write(ref _gate, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        public void Open() => Volatile.Read(ref _gate).TrySetResult();

        /// <summary>Done when the run of that number of a job has started, in any host.</summary>
        public Task StartedAsync(string job, int run) => Signal((job, run)).Task;

        public void Start(string job) => Signal((job, _runs.AddOrUpdate(job, 1, (_, runs) => runs + 1))).TrySetResult();

        private TaskCompletionSource Signal((string Job, int Run) key) =>
            _started.GetOrAdd(key, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        private static TaskCompletionSource Opened()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            gate.SetResult();
            return gate;
        }
    }

    /// <summary>A job of the test, written as the hub's are: its row in the log, the work, the row ended.</summary>
    [DisallowConcurrentExecution]
    internal sealed class ProbeJob(HubDbContext hub, IClock clock, Probe probe) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var job = context.JobDetail.Key.Name;
            var entry = new JobLogEntry { Job = job, StartedAt = clock.UtcNow, Status = "running", Message = Probe.Written };
            hub.JobsLog.Add(entry);
            await hub.SaveChangesAsync(CancellationToken.None);

            probe.Start(job);
            await probe.Gate;

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            await hub.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>What became of every run of a probe in one host: <c>ran</c>, or <c>skipped</c> by the guard.</summary>
    internal sealed class Recorder : IJobListener
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _events = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<(string Job, int Count), TaskCompletionSource> _counts = new();

        public string Name => "e10j-test-recorder";

        public IReadOnlyList<string> Of(string job) => [.. _events.GetValueOrDefault(job) ?? []];

        public Task CountAsync(string job, int count)
        {
            var signal = _counts.GetOrAdd((job, count), _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            if (Of(job).Count >= count)
            {
                signal.TrySetResult();
            }

            return signal.Task;
        }

        public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
            Record(context, "skipped");

        public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default) =>
            Record(context, "ran");

        private Task Record(IJobExecutionContext context, string what)
        {
            var job = context.JobDetail.Key.Name;
            if (!Probe.Jobs.Contains(job))
            {
                return Task.CompletedTask;
            }

            var events = _events.GetOrAdd(job, _ => new ConcurrentQueue<string>());
            events.Enqueue(what);

            foreach (var ((name, count), signal) in _counts)
            {
                if (name == job && events.Count >= count)
                {
                    signal.TrySetResult();
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>Pauses every job of the host but the probes, at the start: nothing of the hub runs in these tests.</summary>
    internal sealed class OnlyTheProbes(ISchedulerFactory schedulers) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var scheduler = await schedulers.GetScheduler(cancellationToken);
            foreach (var key in await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken))
            {
                if (!Probe.Jobs.Contains(key.Name))
                {
                    await scheduler.PauseJob(key, cancellationToken);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
