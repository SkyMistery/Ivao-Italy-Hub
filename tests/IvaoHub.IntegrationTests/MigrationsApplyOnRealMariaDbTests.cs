using IvaoHub.Core.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// There is no shell on the production server: the migration chain runs at start up or it does not
/// run at all. It has to apply on a real MariaDB of the very same minor version, twice in a row,
/// and leave a utf8mb4 database behind (plan section 11.3).
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class MigrationsApplyOnRealMariaDbTests(MariaDbFixture mariaDb)
{
    private HubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HubDbContext>();
        options.UseMySql(mariaDb.ConnectionString, new MariaDbServerVersion(HubDbContext.ServerVersion));
        options.UseSnakeCaseNamingConvention();
        return new HubDbContext(options.Options);
    }

    [Fact]
    public async Task AppliesTheWholeChainFromAnEmptyDatabaseAndIsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateContext();

        await database.Database.MigrateAsync(token);
        var applied = (await database.Database.GetAppliedMigrationsAsync(token)).ToArray();

        Assert.NotEmpty(applied);
        Assert.Empty(await database.Database.GetPendingMigrationsAsync(token));

        // A second start must be a no operation, which is what every restart on Plesk does.
        await database.Database.MigrateAsync(token);
        Assert.Equal(applied, await database.Database.GetAppliedMigrationsAsync(token));
    }

    [Fact]
    public async Task CreatesEveryTableOfTheCore()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateContext();
        await database.Database.MigrateAsync(token);

        var tables = await ReadStringsAsync(
            database,
            "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()",
            token);

        string[] expected =
        [
            "hub_users", "hub_user_staff_positions", "hub_user_grants", "hub_user_tokens",
            "hub_division_settings", "hub_audit_log", "hub_jobs_log",
            "ref_ivao_centers", "ref_ivao_airports",
            "cms_contents", "cms_content_versions", "cms_links", "cms_search_index",
            "cms_calendar_entries", "cms_award_signals",
        ];

        Assert.Equal(expected.Order(), expected.Intersect(tables).Order());
    }

    [Fact]
    public async Task StoresEveryTableAsUtf8Mb4()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateContext();
        await database.Database.MigrateAsync(token);

        var wrong = await ReadStringsAsync(
            database,
            "SELECT TABLE_NAME FROM information_schema.TABLES "
            + "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_COLLATION NOT LIKE 'utf8mb4%'",
            token);

        Assert.Empty(wrong);
    }

    [Fact]
    public async Task IndexesTheSearchProjectionForFullTextAndOneRowPerLanguage()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateContext();
        await database.Database.MigrateAsync(token);

        var fullText = await ReadStringsAsync(
            database,
            "SELECT INDEX_NAME FROM information_schema.STATISTICS "
            + "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cms_search_index' AND INDEX_TYPE = 'FULLTEXT'",
            token);

        var unique = await ReadStringsAsync(
            database,
            "SELECT INDEX_NAME FROM information_schema.STATISTICS "
            + "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cms_search_index' AND NON_UNIQUE = 0 "
            + "AND COLUMN_NAME = 'locale'",
            token);

        Assert.NotEmpty(fullText);
        Assert.NotEmpty(unique);
    }

    private static async Task<List<string>> ReadStringsAsync(HubDbContext database, string sql, CancellationToken token)
    {
        var values = new List<string>();
        var connection = database.Database.GetDbConnection();

        await database.Database.OpenConnectionAsync(token);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                values.Add(reader.GetString(0));
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }

        return values;
    }
}
