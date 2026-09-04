using Testcontainers.MariaDb;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// One MariaDB container for the whole test assembly, on the very same minor version as
/// production. Every integration test of M0 runs against a real database, never against a fake
/// provider: the migration chain has to be proven where it will actually run.
/// </summary>
public sealed class MariaDbFixture : IAsyncLifetime
{
    private const string Image = "mariadb:11.4.10";

    /// <summary>What the container gives root. Declared rather than defaulted, so it is readable.</summary>
    private const string RootPassword = "ivaohub-root";

    private readonly MariaDbContainer _container = new MariaDbBuilder(Image)
        .WithDatabase("ivaohub")
        .WithUsername("ivaohub")
        .WithPassword("ivaohub")
        .WithEnvironment("MARIADB_ROOT_PASSWORD", RootPassword)
        .WithCommand(
            "--character-set-server=utf8mb4",
            "--collation-server=utf8mb4_unicode_ci")
        .Build();

    /// <summary>Connection string with the pool cap that the shared production server imposes.</summary>
    public string ConnectionString => _container.GetConnectionString() + ";MaximumPoolSize=15";

    /// <summary>
    /// The same server as root. Exactly one test needs it — the forkability one, which starts a
    /// second installation on a database of its own so that the migration chain runs from zero — and
    /// the application user of a MariaDB container may not create a database.
    /// <para>The image is given this password for root by the builder below, so it is not a guess.</para>
    /// </summary>
    public string RootConnectionString => new MySqlConnector.MySqlConnectionStringBuilder(ConnectionString)
    {
        UserID = "root",
        Password = RootPassword,
    }.ConnectionString;

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

/// <summary>Shares the container between every test class instead of starting one per class.</summary>
[CollectionDefinition(Name)]
public sealed class MariaDbCollection : ICollectionFixture<MariaDbFixture>
{
    public const string Name = "mariadb";
}
