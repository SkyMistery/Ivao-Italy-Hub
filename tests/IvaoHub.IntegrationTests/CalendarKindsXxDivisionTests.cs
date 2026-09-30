using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The words of the calendar in a fork, the fictional division XX started from scratch (M4, E1, note
/// <c>decisions/2026-09-30-i-tipi-degli-eventi-e-l-ed-sul-banco.md</c>; plan §4): the fork is born with every word of the
/// seed — the four of the events among them — each labelled with the English word its key names in the fork's own
/// language file, and in no other language.
/// <para>Its own class, the same start the shared test of the fork makes (<c>ForkabilityXxDivisionTests</c>, the
/// maintainer's, which asserts <c>tour</c> and <c>deadline</c>), on a database of its own. That one reads every word for
/// something of this division; this one reads that each word is the one the seed meant — a key missing from the English
/// file arrives as the key itself, which names no division and passes there.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class CalendarKindsXxDivisionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const string XxDatabase = "ivaohub_xx_kinds";

    /// <summary>The words of the events (M4, E1).</summary>
    private static readonly string[] TheEvents = ["rfe", "rfo", "mse", "online-day"];

    private string _root = null!;
    private string? _previousRoot;
    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        // A root holding what an installation of XX holds: its division file, its one language, the seeds every release ships.
        _root = Path.Combine(Path.GetTempPath(), $"ivaohub-xx-kinds-{Guid.NewGuid():N}");
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
    public async Task TheForkIsBornWithEveryWordOfTheSeedInItsOwnLanguage()
    {
        var token = TestContext.Current.CancellationToken;

        // What the fork's own files say: the words of the seed, the four of the events among them.
        var seeded = SeededKinds();
        Assert.All(TheEvents, key => Assert.Contains(seeded, kind => kind.Key == key));

        // What every chip and every select of the fork reads: the bootstrap, which a visitor gets too.
        using var client = _factory.CreateApiClient();
        var me = JsonDocument.Parse(await client.GetStringAsync(new Uri("/api/me", UriKind.Relative), token)).RootElement;
        var offered = me.GetProperty("calendarKinds").EnumerateArray()
            .ToDictionary(kind => kind.GetProperty("key").GetString() ?? string.Empty, StringComparer.Ordinal);

        foreach (var (key, labelKey, colour) in seeded)
        {
            var word = EnglishWord(labelKey);
            Assert.True(word is not null, $"The label of the calendar kind \"{key}\" names {labelKey}, which the English file lacks.");

            Assert.True(offered.TryGetValue(key, out var kind), $"The fork was born without the calendar kind \"{key}\".");
            Assert.Equal(colour, kind.GetProperty("colour").GetString());

            // One language, the fork's, and the word of its own file: never the key, never another language.
            var label = kind.GetProperty("label");
            Assert.Equal(["en"], label.EnumerateObject().Select(language => language.Name));
            Assert.Equal(word, label.GetProperty("en").GetString());
        }
    }

    /// <summary>Every word of the fork's seed: its key, the translation key of its label, its colour.</summary>
    private List<(string Key, string LabelKey, string Colour)> SeededKinds() =>
    [
        .. Directory.EnumerateFiles(Path.Combine(_root, "seed", "calendar-kinds"), "*.json")
            .SelectMany(file => JsonNode.Parse(File.ReadAllText(file))?["kinds"]?.AsArray() ?? new JsonArray())
            .OfType<JsonNode>()
            .Select(kind => (
                kind["key"]?.GetValue<string>() ?? string.Empty,
                kind["label"]?["$t"]?.GetValue<string>() ?? string.Empty,
                kind["colour"]?.GetValue<string>() ?? string.Empty)),
    ];

    /// <summary>The English word of a translation key, from any file of the fork's one language, or null.</summary>
    private string? EnglishWord(string key)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(_root, "locales", "en"), "*.json"))
        {
            var node = JsonNode.Parse(File.ReadAllText(file));
            foreach (var part in key.Split('.'))
            {
                node = node is JsonObject parent ? parent[part] : null;
            }

            if (node is JsonValue value && value.TryGetValue<string>(out var word))
            {
                return word;
            }
        }

        return null;
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
