using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The hub, started as a division that is not this one (design M0 section 8, plan section 4).
/// <para>The rule the whole project rests on is that <b>the code does not know it is Italian</b>:
/// no ICAO code, no FIR name, no staff position, no URL and no "IT" anywhere in it. The behaviour
/// of a division comes from <c>config/division.json</c>, its airspace from the IVAO API, and every
/// word of it from the database and from <c>locales/</c>. This test is what makes that a fact
/// rather than an intention: an installation of the fictional division XX is started from scratch,
/// on its own empty database, and nothing it answers may mention Italy.</para>
/// <para>It is also the only test that runs the whole start up sequence against a database that has
/// never been migrated: the chain, the super administrator bootstrap and the seeding of the system
/// templates, all in a division with one language.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ForkabilityXxDivisionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    /// <summary>
    /// What must not appear anywhere: the division code as a staff position prefix, one of its
    /// FIRs, its name in either language, and its host.
    /// </summary>
    private static readonly string[] Forbidden = ["IT-", "LIRR", "Italia", "Italy", "it.ivao.aero"];

    private const string XxDatabase = "ivaohub_xx";

    /// <summary>The one member of the fork this test signs in as.</summary>
    private const int XxStaffVid = 680001;

    private string _root = null!;
    private string? _previousRoot;
    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        // A root of its own, holding exactly what an installation of XX would hold: its division
        // file, the one language it publishes in, and the seed files every release ships.
        _root = Path.Combine(Path.GetTempPath(), $"ivaohub-xx-{Guid.NewGuid():N}");
        var repository = RepositoryRoot();

        Directory.CreateDirectory(Path.Combine(_root, "config"));
        File.Copy(
            Path.Combine(repository, "config", "division.xx.json"),
            Path.Combine(_root, "config", "division.json"));

        CopyTree(Path.Combine(repository, "locales", "en"), Path.Combine(_root, "locales", "en"));
        CopyTree(Path.Combine(repository, "seed"), Path.Combine(_root, "seed"));

        // HubPaths reads this first of all, which is what it is for. The integration tests share
        // one collection and therefore run one at a time, so nothing else is looking at it while
        // this class holds it; it is put back in DisposeAsync whatever happens.
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
    public async Task ForkabilityXxDivision()
    {
        var token = TestContext.Current.CancellationToken;

        // The bootstrap: the division, its one language, its menus, its modules and its registries.
        // It is the payload the whole client is drawn from, so it is the one that would leak.
        var bootstrap = await GetStringAsync("/api/me", token);
        AssertNothingItalian(bootstrap, "/api/me");

        var parsed = JsonDocument.Parse(bootstrap).RootElement;
        var division = parsed.GetProperty("division");

        Assert.Equal("XX", division.GetProperty("code").GetString());
        Assert.Equal("en", division.GetProperty("defaultLocale").GetString());
        Assert.Equal(["en"], division.GetProperty("locales").EnumerateArray().Select(l => l.GetString()));

        // The module list is the build's, not the division's: XX runs the same code and therefore
        // the same modules, which is the point of a fork.
        Assert.NotEmpty(parsed.GetProperty("modules").EnumerateArray());

        AssertNothingItalian(await GetStringAsync("/api/version", token), "/api/version");
        AssertNothingItalian(await GetStringAsync("/health", token), "/health");
        AssertNothingItalian(await GetStringAsync($"{SearchEndpoints.Pattern}?q=division", token), "search");

        // The system templates, seeded from the same files this repository ships: one language, and
        // the language of this division rather than of the one that wrote them.
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var templates = await database.Contents
            .IgnoreQueryFilters()
            .Where(content => content.IsTemplate)
            .ToListAsync(token);

        Assert.NotEmpty(templates);

        foreach (var template in templates)
        {
            Assert.Equal(["en"], template.Title.Select(entry => entry.Key));
            AssertNothingItalian(template.BodyJson, $"template {template.Slug}");
        }
    }

    [Fact]
    public void TheLanguageFilesOfAForkNameNobodyElsesDivision()
    {
        // Whoever forks gets `locales/en/` as it is in this repository. If the English of this
        // division talked about Italy, every fork would inherit it and nothing else would notice.
        foreach (var file in Directory.EnumerateFiles(Path.Combine(_root, "locales", "en"), "*.json"))
        {
            AssertNothingItalian(File.ReadAllText(file), Path.GetFileName(file));
        }
    }

    [Fact]
    public async Task TheMailsOfAForkNameNobodyElsesDivision()
    {
        // Mails are the newest place a sentence of this division can hide, and the words are only
        // half of it: what lands in somebody's inbox is the template of `locales/en/mail.json` with
        // values filled in, and a value is where a domain, a department name or a link would come
        // from. So the mail is not rendered from invented data here — a member of XX writes to a
        // department of XX and the queue is emptied, exactly as it would be on the fork's own
        // server (design M1 §11.2).
        var token = TestContext.Current.CancellationToken;

        await SeedStaffAsync(token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, XxStaffVid, token);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(ContactsEndpoints.Pattern, UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                department = nameof(Department.WD),
                subject = "A question about the site",
                body = "Where do I find the charts?",
            }),
        };

        request.Headers.Add("X-Requested-With", "hub");
        using var submitted = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);

        var sender = new CollectingMailSender();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var job = new NotificationDispatchJob(
                scope.ServiceProvider.GetRequiredService<HubDbContext>(),
                sender,
                scope.ServiceProvider.GetRequiredService<LocaleCatalog>(),
                Options.Create(new SmtpOptions { Host = "localhost", From = "hub@example.org" }),
                scope.ServiceProvider.GetRequiredService<IClock>(),
                NullLogger<NotificationDispatchJob>.Instance);

            await job.RunAsync(token);
        }

        Assert.NotEmpty(sender.Sent);

        foreach (var mail in sender.Sent)
        {
            AssertNothingItalian(mail.To, "the address of a mail");
            AssertNothingItalian(mail.Subject, "the subject of a mail");
            AssertNothingItalian(mail.Text, "the body of a mail");

            // The link back into the site is built from the division's own domain, which is where
            // "it.ivao.aero" would appear if anything still knew this hub had been Italian.
            Assert.Contains("example.org", mail.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("{{", mail.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>A member of the fork's web team, with an address the queue can write to.</summary>
    private async Task SeedStaffAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        database.Users.Add(new HubUser
        {
            Vid = XxStaffVid,
            FirstName = "Example",
            LastName = "Webmaster",
            Email = "webmaster@example.org",
            IsStaff = true,
            SecurityStamp = SuperadminService.NewStamp(),
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        });

        // "XX-WM" and not "IT-WM": the position prefix is the division's own code, which is the
        // whole point of StaffRoleMap taking one.
        var position = StaffRoleMap.Parse("XX-WM", "XX", new HashSet<string>());

        database.UserStaffPositions.Add(new UserStaffPosition
        {
            Vid = XxStaffVid,
            Position = "XX-WM",
            Department = position?.Department,
            Level = position?.Level,
            Fir = position?.Fir,
            SyncedAt = clock.UtcNow,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A mail server that keeps what it was handed, so a test can read it.</summary>
    private sealed class CollectingMailSender : IMailSender
    {
        private readonly List<OutgoingMail> _sent = [];

        public IReadOnlyList<OutgoingMail> Sent => _sent;

        public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default)
        {
            _sent.Add(mail);
            return Task.CompletedTask;
        }
    }

    private static void AssertNothingItalian(string content, string where)
    {
        foreach (var forbidden in Forbidden)
        {
            Assert.DoesNotContain(forbidden, content, StringComparison.OrdinalIgnoreCase);
        }

        Assert.False(string.IsNullOrWhiteSpace(content), $"{where} answered nothing at all");
    }

    private async Task<string> GetStringAsync(string path, CancellationToken cancellationToken)
    {
        using var client = _factory.CreateApiClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// A database of its own, empty: the whole migration chain then runs from zero, which is the
    /// other half of what a fork does on its first start.
    /// </summary>
    private async Task<string> FreshDatabaseAsync(CancellationToken cancellationToken)
    {
        await using (var connection = new MySqlConnection(mariaDb.RootConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText =
                $"DROP DATABASE IF EXISTS `{XxDatabase}`; "
                + $"CREATE DATABASE `{XxDatabase}` "
                + $"CHARACTER SET {HubDbContext.CharSet} COLLATE {HubDbContext.Collation}; "
                // The installation itself connects as the application user, exactly as a real one
                // would: only the creation of the database needs more than that.
                + $"GRANT ALL ON `{XxDatabase}`.* TO 'ivaohub'@'%';";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new MySqlConnectionStringBuilder(mariaDb.ConnectionString)
        {
            Database = XxDatabase,
        }.ConnectionString;
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
