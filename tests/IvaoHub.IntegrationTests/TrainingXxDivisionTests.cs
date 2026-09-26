using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The training in a fork, the fictional division XX started from scratch (design M3 §10, plan §4): the fork runs the
/// module like every other, its migrations apply from zero, and its settings start with nothing of this division in them.
/// <para>Its own class, the same start the shared test of the fork makes (<c>ForkabilityXxDivisionTests</c>, which the
/// maintainer's pipeline counts test by test), on a database of its own; the language file of the module is read by that one
/// already, with every English file of a fork.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingXxDivisionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    /// <summary>What must not appear: the division code as a position prefix, one of its FIRs, its name, its host.</summary>
    private static readonly string[] Forbidden = ["IT-", "LIRR", "Italia", "Italy", "it.ivao.aero"];

    private const string XxDatabase = "ivaohub_xx_training";

    private string _root = null!;
    private string? _previousRoot;
    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        // A root holding what an installation of XX holds: its division file, its one language, the seeds every release ships.
        _root = Path.Combine(Path.GetTempPath(), $"ivaohub-xx-training-{Guid.NewGuid():N}");
        var repository = RepositoryRoot();

        Directory.CreateDirectory(Path.Combine(_root, "config"));
        File.Copy(Path.Combine(repository, "config", "division.xx.json"), Path.Combine(_root, "config", "division.json"));
        CopyTree(Path.Combine(repository, "locales", "en"), Path.Combine(_root, "locales", "en"));
        CopyTree(Path.Combine(repository, "seed"), Path.Combine(_root, "seed"));

        // Read by HubPaths first of all; the integration tests run one class at a time, and it is put back below.
        _previousRoot = Environment.GetEnvironmentVariable(HubPaths.RootVariable);
        Environment.SetEnvironmentVariable(HubPaths.RootVariable, _root);

        _factory = new HubWebApplicationFactory(await FreshDatabaseAsync(token));
    }

    public async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable(HubPaths.RootVariable, _previousRoot);
        await _factory.DisposeAsync();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temporary directory that will not go away is not a failed test.
        }
    }

    [Fact]
    public async Task TheForkStartsWithTheTrainingAndSettingsOfItsOwn()
    {
        var token = TestContext.Current.CancellationToken;

        // Among the modules of the bootstrap, like the tours.
        using (var client = _factory.CreateApiClient())
        {
            var me = JsonDocument.Parse(await client.GetStringAsync(new Uri("/api/me", UriKind.Relative), token)).RootElement;
            Assert.Contains(
                me.GetProperty("modules").EnumerateArray(),
                module => module.GetProperty("key").GetString() == TrainingModule.ModuleKey);
        }

        await using var scope = _factory.Services.CreateAsyncScope();

        // Its history applied from zero, with nothing pending.
        var context = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        Assert.Contains(await context.Database.GetAppliedMigrationsAsync(token), migration => migration.EndsWith("_Initial", StringComparison.Ordinal));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(token));

        // Its settings as the fork starts: no threshold, no time limit, no position, no site of an exam — nothing of this
        // division, which only its own training department will write.
        var settings = await scope.ServiceProvider.GetRequiredService<ModuleSettingsStore>()
            .GetAsync<TrainingSettings>(TrainingModule.ModuleKey, token);

        Assert.Empty(settings.MinimumHours);
        Assert.Null(settings.MaxResponseDays);
        Assert.Empty(settings.HiddenPositions);
        Assert.Null(settings.TheoryExamUrl);

        var written = JsonSerializer.Serialize(settings);
        foreach (var forbidden in Forbidden)
        {
            Assert.DoesNotContain(forbidden, written, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>A database of its own and empty, so the whole chain of migrations runs from zero, as on a fork's first start.</summary>
    private async Task<string> FreshDatabaseAsync(CancellationToken cancellationToken)
    {
        await using (var connection = new MySqlConnection(mariaDb.RootConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText =
                $"DROP DATABASE IF EXISTS `{XxDatabase}`; "
                + $"CREATE DATABASE `{XxDatabase}` CHARACTER SET {HubDbContext.CharSet} COLLATE {HubDbContext.Collation}; "
                + $"GRANT ALL ON `{XxDatabase}`.* TO 'ivaohub'@'%';";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new MySqlConnectionStringBuilder(mariaDb.ConnectionString) { Database = XxDatabase }.ConnectionString;
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination, StringComparison.Ordinal), overwrite: true);
        }
    }

    /// <summary>The repository, found from the test binaries: the solution file is the marker.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
