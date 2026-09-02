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

    private readonly MariaDbContainer _container = new MariaDbBuilder(Image)
        .WithDatabase("ivaohub")
        .WithUsername("ivaohub")
        .WithPassword("ivaohub")
        .WithCommand(
            "--character-set-server=utf8mb4",
            "--collation-server=utf8mb4_unicode_ci")
        .Build();

    /// <summary>Connection string with the pool cap that the shared production server imposes.</summary>
    public string ConnectionString => _container.GetConnectionString() + ";MaximumPoolSize=15";

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

/// <summary>Shares the container between every test class instead of starting one per class.</summary>
[CollectionDefinition(Name)]
public sealed class MariaDbCollection : ICollectionFixture<MariaDbFixture>
{
    public const string Name = "mariadb";
}
