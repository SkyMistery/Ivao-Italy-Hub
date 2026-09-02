using Testcontainers.MariaDb;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Proves that the container used by every later integration test starts on this machine and in
/// CI. F1 turns it into a shared fixture that runs the migration chain.
/// </summary>
public sealed class MariaDbContainerTests
{
    /// <summary>Same minor as production (design M0 section 0.3).</summary>
    private const string MariaDbImage = "mariadb:11.4.10";

    [Fact]
    public async Task ContainerStartsAndAnswersQueries()
    {
        await using var container = new MariaDbBuilder(MariaDbImage)
            .WithDatabase("ivaohub")
            .WithUsername("ivaohub")
            .WithPassword("ivaohub")
            .Build();

        await container.StartAsync(TestContext.Current.CancellationToken);

        var result = await container.ExecScriptAsync("SELECT 1;", TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
    }
}
