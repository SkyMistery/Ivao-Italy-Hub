using System.Collections.Concurrent;
using System.Collections.Specialized;
using IvaoHub.Core.Airspace;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Jobs;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of the scheduled jobs that read no database (note <c>decisions/2026-10-09-i-job-che-recuperano.md</c>): when a
/// job is due, the name of its lock, what the installation may say about the jobs, how the scheduled task's token is
/// compared, in a header or in the address, and masked in the log, and the time zones of the core's own triggers. The runs
/// themselves are proven on MariaDB, in
/// <c>IvaoHub.IntegrationTests.ScheduledJobsTests</c>.
/// </summary>
public sealed class ScheduledJobsTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    /// <summary>Every day at 03:15, read in the zone given: the reference data's schedule.</summary>
    private static JobCron[] Nightly(TimeZoneInfo zone) => [new JobCron("0 15 3 * * ?", zone)];

    /// <summary>An instant of the summer, written in Rome's own time (two hours ahead of UTC).</summary>
    private static DateTime InRome(int day, int hour, int minute, int second = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 7, day, hour, minute, second, DateTimeKind.Unspecified), Rome);

    // ---- when a job is due -------------------------------------------------------------------------------------------

    [Fact]
    public void AnOccurrenceAfterTheLastStartMakesTheJobDueOnce()
    {
        // Ran yesterday at 03:15; today's 03:15 has come: due, however late the check.
        Assert.True(JobSchedule.IsDue(Nightly(Rome), InRome(9, 3, 15), InRome(10, 3, 16)));
        Assert.True(JobSchedule.IsDue(Nightly(Rome), InRome(9, 3, 15), InRome(10, 22, 0)));

        // Three nights lost: still one answer, not three runs.
        Assert.True(JobSchedule.IsDue(Nightly(Rome), InRome(6, 3, 15), InRome(10, 9, 0)));
    }

    [Fact]
    public void NoOccurrenceSinceTheLastStartMeansNotDue()
    {
        // Ran at today's 03:15 (late, at 09:00 even): nothing until tomorrow's.
        Assert.False(JobSchedule.IsDue(Nightly(Rome), InRome(10, 3, 15), InRome(10, 3, 16)));
        Assert.False(JobSchedule.IsDue(Nightly(Rome), InRome(10, 9, 0), InRome(10, 23, 59)));

        // A minute before today's occurrence, yesterday's run still covers it.
        Assert.False(JobSchedule.IsDue(Nightly(Rome), InRome(9, 3, 15), InRome(10, 3, 14)));
    }

    [Fact]
    public void TheScheduleIsReadInItsTriggersTimeZone()
    {
        // Ran the day before at 03:30 UTC, after both. At 02:00 UTC of a summer day it is 04:00 in Rome: 03:15 has come
        // there, and not yet in UTC.
        var lastStart = new DateTime(2026, 7, 9, 3, 30, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 10, 2, 0, 0, DateTimeKind.Utc);

        Assert.True(JobSchedule.IsDue(Nightly(Rome), lastStart, now));
        Assert.False(JobSchedule.IsDue(Nightly(TimeZoneInfo.Utc), lastStart, now));
    }

    [Fact]
    public void AJobThatNeverRanIsDueAndOneWithNoScheduleNeverIs()
    {
        Assert.True(JobSchedule.IsDue(Nightly(Rome), lastStartUtc: null, InRome(10, 12, 0)));
        Assert.False(JobSchedule.IsDue([], lastStartUtc: null, InRome(10, 12, 0)));
        Assert.False(JobSchedule.IsDue([], InRome(1, 0, 0), InRome(10, 12, 0)));
    }

    [Fact]
    public void AnyScheduleOfTheJobMakesItDue()
    {
        JobCron[] two = [new JobCron("0 0 7 * * ?", TimeZoneInfo.Utc), new JobCron("0 0 19 * * ?", TimeZoneInfo.Utc)];
        var morning = new DateTime(2026, 7, 10, 7, 0, 5, DateTimeKind.Utc);

        Assert.False(JobSchedule.IsDue(two, morning, morning.AddHours(11)));
        Assert.True(JobSchedule.IsDue(two, morning, morning.AddHours(12)));
    }

    [Fact]
    public void AnOccurrenceJustAfterARunsStartIsThatRuns()
    {
        // Yesterday's digest, lost, made up by the check of the minute a moment before today's: today's is done with it.
        var occurrence = new DateTime(2026, 7, 10, 7, 0, 0, DateTimeKind.Utc);
        JobCron[] daily = [new JobCron("0 0 7 * * ?", TimeZoneInfo.Utc)];

        Assert.False(JobSchedule.IsDue(daily, occurrence.AddMilliseconds(-100), occurrence.AddMinutes(1)));
        Assert.False(JobSchedule.IsDue(daily, occurrence - JobSchedule.SameOccurrence, occurrence.AddMinutes(1)));

        // Further before it, the run was another one.
        Assert.True(JobSchedule.IsDue(daily, occurrence.AddSeconds(-2), occurrence.AddMinutes(1)));
    }

    [Fact]
    public void TheEveryMinuteQueueIsDueOnceAMinute()
    {
        JobCron[] everyMinute = [new JobCron("0 * * * * ?", TimeZoneInfo.Utc)];
        var run = new DateTime(2026, 7, 10, 7, 0, 0, 30, DateTimeKind.Utc);

        Assert.False(JobSchedule.IsDue(everyMinute, run, run.AddSeconds(45)));
        Assert.True(JobSchedule.IsDue(everyMinute, run, run.AddSeconds(60)));
    }

    // ---- the lock --------------------------------------------------------------------------------------------------------

    [Fact]
    public void AJobsLockIsNamedForItsDatabaseAndItself()
    {
        Assert.Equal("hub-job:itivao_test:flightops-weather", JobLocks.NameFor("itivao_test", "flightops-weather"));

        // Two installations on one server, two jobs of one installation: four names.
        string[] names =
        [
            JobLocks.NameFor("hub_one", "notification-dispatch"),
            JobLocks.NameFor("hub_two", "notification-dispatch"),
            JobLocks.NameFor("hub_one", "ref-data-sync"),
            JobLocks.NameFor("hub_two", "ref-data-sync"),
        ];
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());

        // Never longer than the server takes: a long database and a long job are hashed, under the same prefix.
        var longest = JobLocks.NameFor(new string('d', 64), "flightops-weather-retention");
        Assert.Equal(DatabaseLock.LongestName, longest.Length);
        Assert.StartsWith(JobLocks.Prefix, longest, StringComparison.Ordinal);
        Assert.NotEqual(longest, JobLocks.NameFor(new string('d', 64), "flightops-weather"));
    }

    // ---- what the installation says ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Production", null, true)]
    [InlineData("Development", null, false)]
    [InlineData("E2E", null, false)]
    [InlineData("Development", true, true)]
    [InlineData("Production", false, false)]
    public void TheLostRunsAreMadeUpInProductionUnlessTheInstallationSaysOtherwise(string environment, bool? catchUp, bool on) =>
        Assert.Equal(on, JobCatchUp.IsOn(new JobOptions { CatchUp = catchUp }, new Environment(environment)));

    [Fact]
    public void ATokenSoShortThatItCouldBeGuessedStopsTheStart()
    {
        var validator = new JobOptionsValidator();

        var failed = validator.Validate(null, new JobOptions { Token = "short-token" });
        Assert.True(failed.Failed);
        Assert.Contains(failed.Failures!, failure => failure.Contains("'Jobs:Token'", StringComparison.Ordinal));
        Assert.True(validator.Validate(null, new JobOptions { Token = new string(' ', 40) }).Failed);

        // No token is an installation without the address; a long one is a secret.
        Assert.True(validator.Validate(null, new JobOptions()).Succeeded);
        Assert.True(validator.Validate(null, new JobOptions { Token = string.Empty }).Succeeded);
        Assert.True(validator.Validate(null, new JobOptions { Token = new string('x', JobOptions.ShortestToken) }).Succeeded);
    }

    [Fact]
    public void TheScheduledTasksCallWaitsEightySecondsUnlessTheInstallationSaysOtherwise()
    {
        var validator = new JobOptionsValidator();

        Assert.Equal(TimeSpan.FromSeconds(80), new JobOptions().Wait);
        Assert.Equal(TimeSpan.FromSeconds(45), new JobOptions { WaitSeconds = 45 }.Wait);
        Assert.True(validator.Validate(null, new JobOptions { WaitSeconds = 45 }).Succeeded);

        // Not a wait any proxy would make: the start stops, naming the key.
        foreach (var seconds in new[] { 0, -5, JobOptions.LongestWaitSeconds + 1 })
        {
            var failed = validator.Validate(null, new JobOptions { WaitSeconds = seconds });
            Assert.True(failed.Failed);
            Assert.Contains(failed.Failures!, failure => failure.Contains("'Jobs:WaitSeconds'", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheScheduledTaskCarriesTheTokenAsABearer()
    {
        const string token = "e10j-unit-token-of-the-installation-0123456789";

        Assert.True(JobRunEndpoints.Carries(new StringValues($"Bearer {token}"), token));
        Assert.True(JobRunEndpoints.Carries(new StringValues($"bearer   {token}  "), token));

        Assert.False(JobRunEndpoints.Carries(StringValues.Empty, token));
        Assert.False(JobRunEndpoints.Carries(new StringValues(token), token));
        Assert.False(JobRunEndpoints.Carries(new StringValues($"Basic {token}"), token));
        Assert.False(JobRunEndpoints.Carries(new StringValues($"Bearer {token}x"), token));
        Assert.False(JobRunEndpoints.Carries(new StringValues($"Bearer {token[..^1]}"), token));
        Assert.False(JobRunEndpoints.Carries(new StringValues("Bearer    "), token));
    }

    [Fact]
    public void TheTokenInTheAddressIsComparedAsTheHeadersIs()
    {
        const string token = "e10j-unit-token-of-the-installation-0123456789";

        Assert.True(JobRunEndpoints.IsTheToken(token, token));
        Assert.True(JobRunEndpoints.IsTheToken($" {token} ", token));

        Assert.False(JobRunEndpoints.IsTheToken(null, token));
        Assert.False(JobRunEndpoints.IsTheToken(string.Empty, token));
        Assert.False(JobRunEndpoints.IsTheToken("   ", token));
        Assert.False(JobRunEndpoints.IsTheToken($"{token}x", token));
        Assert.False(JobRunEndpoints.IsTheToken(token[..^1], token));
        Assert.False(JobRunEndpoints.IsTheToken(token.ToUpperInvariant(), token));
    }

    [Theory]
    [InlineData("?token=secret", "?token=***")]
    [InlineData("?a=1&token=secret&b=2", "?a=1&token=***&b=2")]
    [InlineData("?TOKEN=secret", "?TOKEN=***")]
    [InlineData("?%74oken=secret", "?%74oken=***")]
    [InlineData("?token=one&token=two", "?token=***&token=***")]
    [InlineData("token=secret", "token=***")]
    [InlineData("?token=a%3Db&next=1", "?token=***&next=1")]
    [InlineData("?tokens=secret", null)]
    [InlineData("?token", null)]
    [InlineData("?a=1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TheHubsLogWritesTheTokenOfTheAddressMasked(string? queryString, string? logged) =>
        Assert.Equal(logged, JobRunEndpoints.MaskToken(queryString));

    // ---- the guard that cannot decide ------------------------------------------------------------------------------------

    [Fact]
    public async Task ARunGoesAheadWhenItsLockCannotBeAskedFor()
    {
        var token = TestContext.Current.CancellationToken;

        // Nobody answers at that address: neither the lock nor the log can be asked (Carmine on #239, answer 4).
        var unreachable = new DbContextOptionsBuilder<HubDbContext>()
            .UseMySql(
                "Server=127.0.0.1;Port=1;Database=nobody;User ID=nobody;Password=nobody;Connection Timeout=2",
                new MariaDbServerVersion(HubDbContext.ServerVersion))
            .Options;
        var services = new ServiceCollection();
        services.AddScoped(_ => new HubDbContext(unreachable));
        await using var provider = services.BuildServiceProvider();

        var factory = new StdSchedulerFactory(new NameValueCollection
        {
            ["quartz.scheduler.instanceName"] = $"e10j-unit-{Guid.NewGuid():N}",
        });
        var scheduler = await factory.GetScheduler(token);
        await using var locks = new JobLocks();
        scheduler.ListenerManager.AddTriggerListener(
            new ScheduledJobs(
                factory,
                provider.GetRequiredService<IServiceScopeFactory>(),
                locks,
                new SystemClock(),
                new Lifetime(),
                NullLogger<ScheduledJobs>.Instance),
            EverythingMatcher<TriggerKey>.AllTriggers());

        var key = new JobKey($"e10j-unit-probe-{Guid.NewGuid():N}");
        var ran = Signalled.Expect(key.Name);
        await scheduler.ScheduleJob(
            JobBuilder.Create<Signalled>().WithIdentity(key).Build(),
            TriggerBuilder.Create().ForJob(key).WithCronSchedule("0 0 0 1 1 ?", schedule => schedule.InTimeZone(TimeZoneInfo.Utc)).Build(),
            token);
        await scheduler.Start(token);

        try
        {
            await scheduler.TriggerJob(key, token);
            await ran.WaitAsync(TimeSpan.FromSeconds(30), token);
        }
        finally
        {
            await scheduler.Shutdown(waitForJobsToComplete: true, token);
        }
    }

    // ---- the zones of the core's triggers ----------------------------------------------------------------------------------

    [Fact]
    public void TheQueueOfTheMailRunsInUtc()
    {
        var services = new ServiceCollection();
        services.AddHubNotifications();

        var trigger = TriggerOf(services, NotificationServiceCollectionExtensions.TriggerName);
        Assert.Equal(TimeZoneInfo.Utc.Id, trigger.TimeZone.Id);
        Assert.Equal(NotificationDispatchJob.JobName, trigger.JobKey.Name);
    }

    [Fact]
    public void TheOutlinesOfTheFirsAreRefreshedInTheDivisionsNight()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new DivisionOptions
        {
            Code = "XX",
            CountryId = "XX",
            Name = new Dictionary<string, string> { ["en"] = "Example Division" },
            Domain = "example.org",
            Locales = ["en"],
            DefaultLocale = "en",
            Timezone = "Europe/Rome",
        }));
        services.AddAirspace();

        var trigger = TriggerOf(services, AirspaceServiceCollectionExtensions.TriggerName);
        Assert.Equal(Rome.Id, trigger.TimeZone.Id);
        Assert.Equal("0 10 4 ? * SUN", trigger.CronExpressionString);
        Assert.Equal(FirSyncJob.JobName, trigger.JobKey.Name);
    }

    /// <summary>A trigger as the host builds it, by name.</summary>
    private static ICronTrigger TriggerOf(ServiceCollection services, string name)
    {
        using var provider = services.BuildServiceProvider();
        var quartz = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        return Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(quartz.Triggers, trigger => trigger.Key.Name == name));
    }

    /// <summary>A job that says it ran, and does nothing else.</summary>
    internal sealed class Signalled : IJob
    {
        private static readonly ConcurrentDictionary<string, TaskCompletionSource> Runs = new(StringComparer.Ordinal);

        public static Task Expect(string job) =>
            Runs.GetOrAdd(job, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

        public Task Execute(IJobExecutionContext context)
        {
            if (Runs.TryGetValue(context.JobDetail.Key.Name, out var run))
            {
                run.TrySetResult();
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>A host that never stops while the test runs.</summary>
    private sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }

    /// <summary>An environment by name, and nothing else.</summary>
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "IvaoHub.Web";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
