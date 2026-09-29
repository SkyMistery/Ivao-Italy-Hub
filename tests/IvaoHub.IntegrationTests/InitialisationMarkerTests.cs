using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The initialisation marker on the real database (note 2026-09-28-il-marcatore-d-inizializzazione): written only after a
/// complete initialisation, read back by a start that changed nothing, and ignored by a start with another build or
/// another configuration; two processes starting together both initialise.
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
    public async Task TwoProcessesStartingTogetherBothInitialiseAndBothWriteTheMark()
    {
        var token = TestContext.Current.CancellationToken;
        var key = NewKey();
        await ForgetAsync(token);

        // Each waits inside its initialisation for the other: both have read no mark, both insert the row at once.
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var outcomes = await Task.WhenAll(
            RunAsync(key, async _ => { first.SetResult(); await second.Task; }, token),
            RunAsync(key, async _ => { second.SetResult(); await first.Task; }, token));

        Assert.All(outcomes, outcome => Assert.Equal(["no marker"], outcome.Changes));
        Assert.Equal(key.Build, (await ReadAsync(token))?.Build);
        Assert.True((await RunAsync(key, _ => throw new InvalidOperationException("not again"), token)).Skipped);
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
        bool forget = false)
    {
        if (forget)
        {
            await ForgetAsync(cancellationToken);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<InitialisationMarker>()
            .RunAsync(key, initialise, timings: null, cancellationToken);
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
}
