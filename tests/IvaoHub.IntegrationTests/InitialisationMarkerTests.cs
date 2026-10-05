using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The initialisation marker on the real database (note 2026-09-28-il-marcatore-d-inizializzazione): written only after a
/// complete initialisation, read back by a start that changed nothing, and ignored by a start with another build or
/// another configuration; a mark that cannot be written, even after a deadlock, never fails the start (note
/// 2026-09-29-il-marcatore-che-non-si-scrive).
/// <para>And the lock around it (note 2026-10-05-l-inizializzazione-sotto-blocco, issue #203): of two processes starting
/// together one initialises and the other waits and skips; a start that cannot take the lock initialises without it.</para>
/// </summary>
/// <remarks>The mark is one row shared by every host of the suite: a test here leaves it naming a key of its own, and the
/// next host of any other class does a full initialisation, which is what every start did before the mark.</remarks>
[Collection(MariaDbCollection.Name)]
public sealed class InitialisationMarkerTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task AStartThatChangedNothingSkipsAndAChangedConfigurationDoesEverything()
    {
        // The first host initialises or finds the mark of an earlier one with the same key; either way the mark is its own.
        Assert.StartsWith("initialisation ", Initialisation(_factory), StringComparison.Ordinal);

        await using (var again = new HubWebApplicationFactory(mariaDb.ConnectionString))
        {
            Assert.StartsWith("initialisation skipped (marker of ", Initialisation(again), StringComparison.Ordinal);
            Assert.EndsWith("): migrations, module migrations, position grants, content", Initialisation(again), StringComparison.Ordinal);
        }

        // Another value in the division's options, as an edited division.json would be.
        await using (var edited = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<DivisionOptions>(options =>
                options.Modules[SampleModule.ModuleKey] = new ModuleSettings { BaseDepartment = Department.FOD }))))
        {
            Assert.Equal("initialisation full: the configuration changed", Initialisation(edited));
        }

        await using (var back = new HubWebApplicationFactory(mariaDb.ConnectionString))
        {
            Assert.Equal("initialisation full: the configuration changed", Initialisation(back));
        }
    }

    [Fact]
    public async Task TheMarkIsWrittenOnlyAfterTheInitialisationSucceeded()
    {
        var token = TestContext.Current.CancellationToken;
        var before = NewKey();
        await RunAsync(before, _ => Task.CompletedTask, token);

        // A step that fails half way: the start fails, and the mark still names the start before.
        var after = before with { Build = $"build-{Guid.NewGuid():N}" };
        var steps = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(
            after,
            _ =>
            {
                steps++;
                throw new InvalidOperationException("a step failed");
            },
            token));

        Assert.Equal(1, steps);
        var stored = await ReadAsync(token);
        Assert.Equal(before.Build, stored?.Build);

        // So the next start with the new build does everything again, and only then marks it.
        var outcome = await RunAsync(after, _ => { steps++; return Task.CompletedTask; }, token);
        Assert.False(outcome.Skipped);
        Assert.Equal(["another build (the marker is of 9.9.9+test)"], outcome.Changes);
        Assert.Equal(2, steps);
        Assert.Equal(after.Build, (await ReadAsync(token))?.Build);
    }

    [Fact]
    public async Task AnUnchangedKeySkipsAndAnotherBuildOrConfigurationRunsEverything()
    {
        var token = TestContext.Current.CancellationToken;
        var key = NewKey();
        var runs = 0;
        Task Count(CancellationToken _)
        {
            runs++;
            return Task.CompletedTask;
        }

        Assert.Equal(["no marker"], (await RunAsync(key, Count, token, forget: true)).Changes);
        Assert.Equal(1, runs);

        var skipped = await RunAsync(key, Count, token);
        Assert.True(skipped.Skipped);
        Assert.Equal(key.Stamp, skipped.Marker?.Stamp);
        Assert.Equal(1, runs);

        var otherBuild = key with { Build = $"build-{Guid.NewGuid():N}", Stamp = "9.9.10+test" };
        Assert.False((await RunAsync(otherBuild, Count, token)).Skipped);
        Assert.Equal(2, runs);

        var otherConfiguration = otherBuild with { Configuration = "another division.json" };
        Assert.Equal(["the configuration changed"], (await RunAsync(otherConfiguration, Count, token)).Changes);
        Assert.Equal(3, runs);

        Assert.True((await RunAsync(otherConfiguration, Count, token)).Skipped);
        Assert.Equal(3, runs);
    }

    [Fact]
    public async Task OfTwoProcessesStartingTogetherOneSeedsAndTheOtherWaitsAndSkips()
    {
        // Issue #203: a release seeds a calendar kind the installation does not have, and two processes start in the same
        // second. Both read no mark; without the lock both seeded the kind, and the second stopped on its unique key.
        var token = TestContext.Current.CancellationToken;
        var key = NewKey();
        await ForgetAKindAsync(token);
        await ForgetAsync(token);

        // Each gives the other a moment to arrive inside its own initialisation before it seeds: without the lock the
        // other is there at once and the two seed together; with it the other never arrives, and the moment passes.
        var entered = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task SeedAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                both.SetResult();
            }

            await Task.WhenAny(both.Task, Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));

            await using var scope = _factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ContentSeeder>().SeedAsync(cancellationToken);
        }

        var outcomes = await Task.WhenAll(RunAsync(key, SeedAsync, token), RunAsync(key, SeedAsync, token));

        Assert.Equal(1, entered);
        var seeded = Assert.Single(outcomes, outcome => !outcome.Skipped);
        Assert.Equal(["no marker"], seeded.Changes);
        Assert.True(seeded.Lock?.Held);

        // The other one waited for the lock, found the mark the first had written and did nothing, as a later start would.
        var waited = Assert.Single(outcomes, outcome => outcome.Skipped);
        Assert.True(waited.Lock?.Held);
        Assert.Equal(key.Build, waited.Marker?.Build);
        Assert.Matches(
            @"^initialisation skipped \(marker of 9\.9\.9\+test, .+\): content; waited \d+ ms for the initialisation lock$",
            waited.Describe(["content"]));

        await using var check = _factory.Services.CreateAsyncScope();
        var database = check.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.Equal(1, await database.CalendarKinds.CountAsync(kind => kind.Key == SeededKind, token));
        Assert.Equal(1, await database.DivisionSettings.CountAsync(setting => setting.Key == SeededKindSetting, token));
    }

    [Fact]
    public async Task AStartThatWaitedBehindAFailedInitialisationDoesItItself()
    {
        var token = TestContext.Current.CancellationToken;
        var key = NewKey();
        await ForgetAsync(token);

        // The first holds the lock until the second is waiting for it, then fails: no mark is written, the lock is given back.
        var inside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failing = RunAsync(
            key,
            async cancellationToken =>
            {
                inside.SetResult();
                await WaitForALockWaiterAsync(cancellationToken);
                throw new InvalidOperationException("a step failed");
            },
            token);
        await inside.Task;

        var steps = 0;
        var outcome = await RunAsync(key, _ => { steps++; return Task.CompletedTask; }, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing);

        Assert.False(outcome.Skipped);
        Assert.Equal(1, steps);
        Assert.True(outcome.Lock?.Held);
        Assert.Equal("initialisation full: no marker", outcome.Describe(["content"]));
        Assert.Equal(key.Build, (await ReadAsync(token))?.Build);
    }

    [Fact]
    public async Task AStartThatCannotTakeTheLockInitialisesWithoutItAndSaysSo()
    {
        var token = TestContext.Current.CancellationToken;
        var key = NewKey();
        await ForgetAsync(token);

        // Somebody else holds the lock and never gives it back, as a process that hangs in its initialisation would.
        await using var holder = await OpenOutsideThePoolAsync(token);
        await using (var take = new MySqlCommand("SELECT GET_LOCK(@name, 0)", holder))
        {
            take.Parameters.AddWithValue("@name", InitialisationLock.NameFor(holder.Database));
            Assert.Equal(1L, Convert.ToInt64(await take.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture));
        }

        // The lock is never the thing that stops the site: the wait ends and each start initialises as every start did
        // before the lock. Each waits inside its initialisation for the other: both have read no mark, both insert the row
        // at once, and both write the mark (note 2026-09-29-il-marcatore-che-non-si-scrive).
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = TimeSpan.FromMilliseconds(300);

        var outcomes = await Task.WhenAll(
            RunAsync(key, async _ => { first.SetResult(); await second.Task; }, token, lockWait: wait),
            RunAsync(key, async _ => { second.SetResult(); await first.Task; }, token, lockWait: wait));

        Assert.All(outcomes, outcome =>
        {
            Assert.Equal(["no marker"], outcome.Changes);
            Assert.False(outcome.Lock?.Held);
            Assert.Equal(
                "initialisation full: no marker; without the initialisation lock (not free after 300 ms)",
                outcome.Describe(["content"]));
        });
        Assert.Equal(key.Build, (await ReadAsync(token))?.Build);
        Assert.True((await RunAsync(key, _ => throw new InvalidOperationException("not again"), token)).Skipped);
    }

    [Fact]
    public async Task ALockThatCannotBeAskedForIsNotHeldAndDoesNotThrow()
    {
        // Nobody answers at that address: the start that asks goes on without the lock, with a warning.
        var unreachable = new MySqlConnectionStringBuilder(mariaDb.ConnectionString) { Port = 1, ConnectionTimeout = 2 }.ConnectionString;

        await using var held = await InitialisationLock.TakeAsync(
            unreachable,
            TimeSpan.FromSeconds(1),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            TestContext.Current.CancellationToken);

        Assert.False(held.Held);
        Assert.Equal(InitialisationLockReport.CouldNotBeAsked, held.Report.Refusal);
    }

    [Fact]
    public async Task ADeadlockWhileWritingTheMarkIsTriedAgain()
    {
        // What happened under load (PR #146): the two writers of a start met as a deadlock, which EF reports wrapped, as a
        // transient failure, and the start failed. Here the deadlock is made on purpose, with the marker as its victim.
        var token = TestContext.Current.CancellationToken;
        await RunAsync(NewKey(), _ => Task.CompletedTask, token);
        var key = NewKey();

        // The shape InnoDB reported: the row was just deleted and is not purged yet (an open snapshot keeps it), both
        // transactions hold a shared lock on it and both want to write it.
        await using var snapshot = await OpenOutsideThePoolAsync(token);
        await ExecuteAsync(snapshot, "START TRANSACTION WITH CONSISTENT SNAPSHOT", token);
        await ForgetAsync(token);

        // The other one is made the heavier with rows of its own: MariaDB rolls back the lighter one, the marker.
        await using var other = await OpenOutsideThePoolAsync(token);
        await ExecuteAsync(other, "START TRANSACTION", token);
        for (var row = 0; row < 20; row++)
        {
            await ExecuteAsync(other, $"{InsertSetting}('test.deadlock.{Guid.NewGuid():N}', '{{}}', 0, UTC_TIMESTAMP())", token);
        }

        await ExecuteAsync(other, $"SELECT value_json FROM hub_division_settings WHERE `key` = '{InitialisationMarker.SettingKey}' LOCK IN SHARE MODE", token);
        var deadlocks = await DeadlocksAsync(token);

        // The marker finds no mark and inserts: its duplicate check shares the lock, its insert waits for the other's. Then
        // the other inserts too, and waits for the marker's: one of the two has to go.
        var marking = RunAsync(key, _ => Task.CompletedTask, token);
        await WaitForALockWaitAsync(token);
        await ExecuteAsync(other, $"{InsertSetting}('{InitialisationMarker.SettingKey}', '{{}}', 0, UTC_TIMESTAMP())", token);
        await ExecuteAsync(other, "ROLLBACK", token);
        await ExecuteAsync(snapshot, "ROLLBACK", token);

        var outcome = await marking;
        Assert.False(outcome.Skipped);
        Assert.Equal(deadlocks + 1, await DeadlocksAsync(token));
        Assert.Equal(key.Build, (await ReadAsync(token))?.Build);
    }

    [Fact]
    public async Task AMarkThatCannotBeWrittenDoesNotFailTheStart()
    {
        var token = TestContext.Current.CancellationToken;
        await ForgetAsync(token);

        // Every write of the mark fails, as a database that refuses it would: the start still ends, initialised, unmarked.
        var refusing = new RefuseTheMark();
        await using (var host = new HubWebApplicationFactory(mariaDb.ConnectionString, extraInterceptor: refusing))
        {
            Assert.Equal("initialisation full: no marker", Initialisation(host));
            Assert.Contains(host.Services.GetRequiredService<StartupTimings>().Steps, step => step.Name == "marker not written");
        }

        // Not another writer, so it is not tried again; and the next start does everything, as it did before the mark.
        Assert.Equal(1, refusing.Refused);
        Assert.Null(await ReadAsync(token));
        await using var next = new HubWebApplicationFactory(mariaDb.ConnectionString);
        Assert.Equal("initialisation full: no marker", Initialisation(next));
    }

    private static InitialisationKey NewKey() =>
        new($"build-{Guid.NewGuid():N}", $"configuration-{Guid.NewGuid():N}", "seed", "9.9.9+test");

    private static string Initialisation(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<StartupTimings>().Initialisation ?? "(none)";

    /// <summary>One start's marker step, in a scope of its own as a process of its own would have.</summary>
    private async Task<InitialisationOutcome> RunAsync(
        InitialisationKey key,
        Func<CancellationToken, Task> initialise,
        CancellationToken cancellationToken,
        bool forget = false,
        TimeSpan? lockWait = null)
    {
        if (forget)
        {
            await ForgetAsync(cancellationToken);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var marker = scope.ServiceProvider.GetRequiredService<InitialisationMarker>();
        if (lockWait is { } wait)
        {
            marker.LockWait = wait;
        }

        return await marker.RunAsync(key, initialise, timings: null, cancellationToken);
    }

    /// <summary>A calendar kind of the seed, and the key that remembers it was seeded.</summary>
    private const string SeededKind = "exam";
    private const string SeededKindSetting = ContentSeeder.CalendarKindSettingPrefix + SeededKind;

    /// <summary>The installation as a release before that kind left it: no row, and no key saying it was ever seeded.</summary>
    private async Task ForgetAKindAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.CalendarKinds.RemoveRange(
            await database.CalendarKinds.Where(kind => kind.Key == SeededKind).ToListAsync(cancellationToken));
        database.DivisionSettings.RemoveRange(
            await database.DivisionSettings.Where(setting => setting.Key == SeededKindSetting).ToListAsync(cancellationToken));

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Until a connection waits for a named lock: a start behind the one that is initialising.</summary>
    private async Task WaitForALockWaiterAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE STATE = 'User lock' AND INFO LIKE '%GET_LOCK%' AND ID <> CONNECTION_ID()",
            connection);
        for (var tries = 0; tries < 100; tries++)
        {
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }

        Assert.Fail("No start ever waited for the initialisation lock.");
    }

    private async Task<StoredInitialisation?> ReadAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<InitialisationMarker>().ReadAsync(cancellationToken);
    }

    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().DivisionSettings
            .Where(setting => setting.Key == InitialisationMarker.SettingKey)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private const string InsertSetting = "INSERT INTO hub_division_settings (`key`, value_json, updated_by, updated_at) VALUES ";

    /// <summary>A connection of the test's own, never pooled: one left inside a transaction takes its locks with it.</summary>
    private async Task<MySqlConnection> OpenOutsideThePoolAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(new MySqlConnectionStringBuilder(mariaDb.ConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<long> DeadlocksAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("SHOW GLOBAL STATUS LIKE 'Innodb_deadlocks'", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return long.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Until a transaction waits for a lock: the marker's insert, behind the other transaction's shared lock.</summary>
    /// <remarks>InnoDB refreshes <c>INNODB_TRX</c> only when nobody has read it for 100 ms: asked more often, it keeps
    /// answering what it saw the first time.</remarks>
    private async Task WaitForALockWaitAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.INNODB_TRX WHERE trx_state = 'LOCK WAIT'", connection);
        for (var tries = 0; tries < 40; tries++)
        {
            await Task.Delay(250, cancellationToken);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                return;
            }
        }

        Assert.Fail("The marker never waited for the other transaction's lock.");
    }

    /// <summary>Refuses every save that writes the mark, and counts them.</summary>
    private sealed class RefuseTheMark : SaveChangesInterceptor
    {
        private int _refused;

        public int Refused => _refused;

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Refuse(eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Refuse(eventData);
            return ValueTask.FromResult(result);
        }

        private void Refuse(DbContextEventData eventData)
        {
            if (eventData.Context?.ChangeTracker.Entries<DivisionSetting>().Any(entry =>
                    entry.Entity.Key == InitialisationMarker.SettingKey && entry.State is EntityState.Added or EntityState.Modified) == true)
            {
                Interlocked.Increment(ref _refused);
                throw new DbUpdateException("The database refused the mark.");
            }
        }
    }
}
