using IvaoHub.Core.Data;
using IvaoHub.Core.Jobs;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The log of the runs keeps a month (issue #325, item 5), and always the last finished run of every job: the one the
/// guard reads to say when the job is due next.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class JobsLogRetentionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task OldRowsGoAndTheLastFinishedRunOfEveryJobStays()
    {
        var token = TestContext.Current.CancellationToken;
        var often = $"retention-often-{Guid.NewGuid():N}"[..40];
        var yearly = $"retention-yearly-{Guid.NewGuid():N}"[..40];

        long older, last, abandoned, recent, once;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;

            JobLogEntry Row(string job, int daysAgo, bool finished) => new()
            {
                Job = job,
                StartedAt = now.AddDays(-daysAgo),
                FinishedAt = finished ? now.AddDays(-daysAgo).AddMinutes(1) : null,
                Status = finished ? "succeeded" : "running",
            };

            var rows = new[]
            {
                Row(often, 45, finished: false),
                Row(often, 40, finished: true),
                Row(often, 35, finished: true),
                Row(often, 1, finished: false),
                Row(yearly, 400, finished: true),
            };
            database.JobsLog.AddRange(rows);
            await database.SaveChangesAsync(token);
            (abandoned, older, last, recent, once) = (rows[0].Id, rows[1].Id, rows[2].Id, rows[3].Id, rows[4].Id);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<JobsLogRetentionJob>().RunAsync(token) >= 2);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var left = await database.JobsLog.AsNoTracking()
                .Where(run => run.Job == often || run.Job == yearly)
                .Select(run => run.Id)
                .ToListAsync(token);

            // The month's row and the last finished run of each job stay, however old; the rest goes, a run that never
            // ended included.
            Assert.Equal([last, recent, once], left.Order());
            Assert.DoesNotContain(older, left);
            Assert.DoesNotContain(abandoned, left);

            var run = await database.JobsLog.AsNoTracking()
                .Where(entry => entry.Job == JobsLogRetentionJob.JobName)
                .OrderByDescending(entry => entry.Id)
                .FirstAsync(token);
            Assert.Equal("succeeded", run.Status);
        }
    }

    [Fact]
    public async Task TheLastSucceededRunStaysAfterAMonthOfFailures()
    {
        // The release jobs of events and tours start their window at the last run that succeeded: kept, however old, even
        // when every run since has failed.
        var token = TestContext.Current.CancellationToken;
        var failing = $"retention-failing-{Guid.NewGuid():N}"[..40];

        long succeeded, olderSuccess, failed, lastFailed;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;

            JobLogEntry Row(int daysAgo, string status) => new()
            {
                Job = failing,
                StartedAt = now.AddDays(-daysAgo),
                FinishedAt = now.AddDays(-daysAgo).AddMinutes(1),
                Status = status,
            };

            var rows = new[] { Row(60, "succeeded"), Row(50, "succeeded"), Row(45, "failed"), Row(40, "failed") };
            database.JobsLog.AddRange(rows);
            await database.SaveChangesAsync(token);
            (olderSuccess, succeeded, failed, lastFailed) = (rows[0].Id, rows[1].Id, rows[2].Id, rows[3].Id);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<JobsLogRetentionJob>().RunAsync(token);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var left = await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
                .Where(run => run.Job == failing)
                .Select(run => run.Id)
                .ToListAsync(token);

            // The last that ended, for the guard, and the last that succeeded, for the release jobs; nothing else of the month before.
            Assert.Equal([succeeded, lastFailed], left.Order());
            Assert.DoesNotContain(olderSuccess, left);
            Assert.DoesNotContain(failed, left);
        }
    }
}
