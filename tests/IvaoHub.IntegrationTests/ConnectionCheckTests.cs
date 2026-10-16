using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Web.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Whether the hub reaches its two databases, asked by a super administrator (note 2026-10-16-la-prova-dei-collegamenti).
/// The archive is a fake of vIPI's in <b>a database of its own</b>, with the view of the contract over it and the grant the
/// hub's user has on the real one, as in <see cref="AtcActivitySourceTests"/>. VIDs 762001–762003; no row of the hub is
/// written by the check.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ConnectionCheckTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const string ArchiveDatabase = "ivaohub_connection_check_archive";

    private const int SuperadminVid = 762001;
    private const int DirectorVid = 762002;
    private const int MemberVid = 762003;

    /// <summary>The newest connection of the archive: the one time the check reads.</summary>
    private static readonly DateTime Newest = new(2026, 10, 15, 21, 30, 0, DateTimeKind.Utc);

    private static readonly Uri Address = new(ConnectionCheckEndpoints.Pattern, UriKind.Relative);

    private HubWebApplicationFactory _factory = null!;
    private string _archive = null!;

    public async ValueTask InitializeAsync()
    {
        _archive = await ArchiveAsync(TestContext.Current.CancellationToken);
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await AsRootAsync($"DROP DATABASE IF EXISTS `{ArchiveDatabase}`", CancellationToken.None);
    }

    [Fact]
    public async Task OnlyASuperAdministratorAsks()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(DirectorVid, position: "IT-DIR", cancellationToken: token);
        await SeedUserAsync(MemberVid, cancellationToken: token);

        using var anonymous = _factory.CreateApiClient();
        anonymous.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        using var nobody = await anonymous.PostAsync(Address, content: null, token);
        Assert.Equal(HttpStatusCode.Unauthorized, nobody.StatusCode);

        using var member = _factory.CreateApiClient();
        member.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(member, MemberVid, token);
        using var refusedMember = await member.PostAsync(Address, content: null, token);
        Assert.Equal(HttpStatusCode.Forbidden, refusedMember.StatusCode);

        // Every global permission of the catalogue, and still not enough: the installation's databases are the system's.
        using var director = _factory.CreateApiClient();
        director.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(director, DirectorVid, token);
        using var refusedDirector = await director.PostAsync(Address, content: null, token);
        Assert.Equal(HttpStatusCode.Forbidden, refusedDirector.StatusCode);
    }

    /// <summary>A division without an archive: the hub's database answers with its version and migrations, the archive is not opened.</summary>
    [Fact]
    public async Task TheHubsDatabaseAnswersAndAnArchiveNotConfiguredIsNotOpened()
    {
        var token = TestContext.Current.CancellationToken;
        await using var host = WithArchive(AtcDataOptions.None, _archive);
        var body = await CheckAsync(host, token);

        var database = body.GetProperty("database");
        Assert.Equal("Ok", database.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, database.GetProperty("error").ValueKind);
        Assert.True(database.GetProperty("milliseconds").GetInt64() >= 0);
        Assert.Contains("MariaDB", database.GetProperty("serverVersion").GetString(), StringComparison.OrdinalIgnoreCase);

        // The core's context first, then every enabled module's; on the bench every one is up to date.
        var migrations = database.GetProperty("migrations").EnumerateArray().ToList();
        Assert.Equal(nameof(HubDbContext), migrations[0].GetProperty("context").GetString());
        Assert.True(migrations.Count > 1);
        Assert.All(migrations, line =>
        {
            Assert.Empty(line.GetProperty("pending").EnumerateArray());
            Assert.False(string.IsNullOrEmpty(line.GetProperty("latest").GetString()));
        });

        var archive = body.GetProperty("archive");
        Assert.Equal("NotConfigured", archive.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, archive.GetProperty("milliseconds").ValueKind);
    }

    /// <summary>The archive read through its view: the start of the newest connection, and nothing of who held it.</summary>
    [Fact]
    public async Task TheArchiveAnswersWithItsNewestConnection()
    {
        var token = TestContext.Current.CancellationToken;
        await using var host = WithArchive(AtcDataOptions.Vipi, _archive);
        var body = await CheckAsync(host, token);

        var archive = body.GetProperty("archive");
        Assert.Equal("Ok", archive.GetProperty("outcome").GetString());
        Assert.Equal(Newest, archive.GetProperty("latestSessionStart").GetDateTime().ToUniversalTime());
        Assert.True(archive.GetProperty("milliseconds").GetInt64() >= 0);

        var raw = body.GetRawText();
        Assert.DoesNotContain("LIRF", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("761", raw, StringComparison.Ordinal);
    }

    /// <summary>
    /// A wrong password is «access denied», and the answer carries nothing of the connection: not the password, not the
    /// user, not the host, not the database, not the server's own message.
    /// </summary>
    [Fact]
    public async Task AWrongPasswordIsAccessDeniedWithoutTheSecret()
    {
        var token = TestContext.Current.CancellationToken;
        const string Password = "not-the-password-4f1c9e";
        var wrong = new MySqlConnectionStringBuilder(_archive) { Password = Password };

        await using var host = WithArchive(AtcDataOptions.Vipi, wrong.ConnectionString);
        var body = await CheckAsync(host, token);

        var archive = body.GetProperty("archive");
        Assert.Equal("Failed", archive.GetProperty("outcome").GetString());
        Assert.Equal("AccessDenied", archive.GetProperty("error").GetString());

        // The hub's own database is unaffected by the archive's failure.
        Assert.Equal("Ok", body.GetProperty("database").GetProperty("outcome").GetString());

        var raw = body.GetRawText();
        Assert.DoesNotContain(Password, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(wrong.UserID, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(wrong.Server, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(ArchiveDatabase, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("denied for", raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A database that is not there, and a view that is not there, each with its own word.</summary>
    [Fact]
    public async Task AMissingDatabaseAndAMissingViewAreToldApart()
    {
        var token = TestContext.Current.CancellationToken;

        // Root may see every database, so the server says «unknown» instead of «denied».
        var nowhere = new MySqlConnectionStringBuilder(mariaDb.RootConnectionString) { Database = "ivaohub_no_such_archive" };
        await using (var host = WithArchive(AtcDataOptions.Vipi, nowhere.ConnectionString))
        {
            var archive = (await CheckAsync(host, token)).GetProperty("archive");
            Assert.Equal("Failed", archive.GetProperty("outcome").GetString());
            Assert.Equal("UnknownDatabase", archive.GetProperty("error").GetString());
        }

        await AsRootAsync($"DROP VIEW `{ArchiveDatabase}`.`{VipiShareDbContext.SessionsView}`", token);
        var rootOnArchive = new MySqlConnectionStringBuilder(mariaDb.RootConnectionString) { Database = ArchiveDatabase };
        await using (var host = WithArchive(AtcDataOptions.Vipi, rootOnArchive.ConnectionString))
        {
            var archive = (await CheckAsync(host, token)).GetProperty("archive");
            Assert.Equal("Failed", archive.GetProperty("outcome").GetString());
            Assert.Equal("MissingView", archive.GetProperty("error").GetString());
        }
    }

    /// <summary>The division names the archive, the secrets do not hold its connection: said, and nothing opened.</summary>
    [Fact]
    public async Task AnArchiveWithoutItsConnectionStringIsSaid()
    {
        var token = TestContext.Current.CancellationToken;
        await using var host = WithArchive(AtcDataOptions.Vipi, archive: null);
        var archive = (await CheckAsync(host, token)).GetProperty("archive");

        Assert.Equal("Failed", archive.GetProperty("outcome").GetString());
        Assert.Equal("NoConnectionString", archive.GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, archive.GetProperty("milliseconds").ValueKind);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private async Task<JsonElement> CheckAsync(WebApplicationFactory<Program> host, CancellationToken cancellationToken)
    {
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: cancellationToken);

        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        using var signIn = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={SuperadminVid}", UriKind.Relative),
            content: null,
            cancellationToken);
        signIn.EnsureSuccessStatusCode();

        using var response = await client.PostAsync(Address, content: null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>The hub with <c>atcData.source</c> set to <paramref name="source"/>, and the archive's connection when there is one.</summary>
    private WebApplicationFactory<Program> WithArchive(string source, string? archive) =>
        _factory.WithWebHostBuilder(builder =>
        {
            if (archive is not null)
            {
                builder.UseSetting($"ConnectionStrings:{AtcServiceCollectionExtensions.ConnectionStringName}", archive);
            }

            builder.ConfigureTestServices(services => services.AddSingleton<IOptions<DivisionOptions>>(provider => Options.Create(
                provider.GetRequiredService<IOptionsFactory<DivisionOptions>>().Create(Options.DefaultName) with
                {
                    AtcData = new AtcDataOptions { Source = source },
                })));
        });

    /// <summary>vIPI's table as far as the view reads it, the view of the contract, the hub's grant, and two connections.</summary>
    private async Task<string> ArchiveAsync(CancellationToken cancellationToken)
    {
        await AsRootAsync(
            $"""
            DROP DATABASE IF EXISTS `{ArchiveDatabase}`;
            CREATE DATABASE `{ArchiveDatabase}` CHARACTER SET {HubDbContext.CharSet} COLLATE {HubDbContext.Collation};
            USE `{ArchiveDatabase}`;
            CREATE TABLE AtcSessions (
                SessionId bigint NOT NULL PRIMARY KEY, UserId int NOT NULL, Callsign varchar(32) NOT NULL, Position varchar(16) NULL,
                Frequency varchar(16) NULL, StartUtc datetime(6) NOT NULL, EndUtc datetime(6) NULL, DurationSeconds int NOT NULL,
                Rating int NULL, IsOutsideDivision tinyint(1) NOT NULL, KEY IX_AtcSessions_StartUtc (StartUtc));
            CREATE OR REPLACE SQL SECURITY DEFINER VIEW v_share_atc_sessions AS
            SELECT SessionId AS session_id, UserId AS vid, Callsign AS callsign, Position AS position, Frequency AS frequency,
                   StartUtc AS start_utc, EndUtc AS end_utc, DurationSeconds AS duration_seconds, Rating AS rating,
                   IsOutsideDivision AS is_outside_division
            FROM AtcSessions;
            GRANT SELECT ON `{ArchiveDatabase}`.v_share_atc_sessions TO 'ivaohub'@'%';
            INSERT INTO AtcSessions VALUES
                (1, 761101, 'LIRF_TWR', 'TWR', NULL, '2026-10-14 18:00:00', '2026-10-14 19:00:00', 3600, 5, 0),
                (2, 761102, 'LIRF_APP', 'APP', NULL, '{Newest:yyyy-MM-dd HH:mm:ss}', NULL, 0, 5, 0);
            """,
            cancellationToken);

        return new MySqlConnectionStringBuilder(mariaDb.ConnectionString) { Database = ArchiveDatabase }.ConnectionString;
    }

    /// <summary>The application user of the container may not create a database, nor give a grant: root does.</summary>
    private async Task AsRootAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SeedUserAsync(
        int vid,
        bool isSuperadmin = false,
        string? position = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = vid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "User";
        user.IsSuperadmin = isSuperadmin;
        user.IsStaff = position is not null;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(
                row => row.Vid == vid && row.Position == position,
                cancellationToken))
        {
            var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = position,
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
