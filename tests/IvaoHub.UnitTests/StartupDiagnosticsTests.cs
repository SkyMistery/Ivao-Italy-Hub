using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// <c>startup.txt</c> is the first thing read after an upload, and never the reason a start fails (issue #325, item 4):
/// a full disk, a permission, a file held open by an FTP client only lose the report.
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class StartupDiagnosticsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ivaohub-startup-{Guid.NewGuid():N}");

    public StartupDiagnosticsTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "config"));
        File.WriteAllText(Path.Combine(_root, "config", "division.json"), "{}");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task AReportThatCannotBeWrittenDoesNotStopTheStart()
    {
        var paths = HubPaths.Resolve(_root);

        // A file where the folder should be: the folder cannot be made, the report cannot be written.
        await File.WriteAllTextAsync(paths.Diagnostics, "in the way", TestContext.Current.CancellationToken);

        await StartupDiagnostics.WriteAsync(
            paths,
            new BuildInfo("0.15.0", "0123456789abcdef0123456789abcdef01234567", DateTime.UnixEpoch, ".NET"),
            "Production",
            "XX",
            appliedMigrations: [],
            enabledModules: [],
            DateTime.UnixEpoch,
            "example.org",
            preview: false,
            TestContext.Current.CancellationToken);

        Assert.True(File.Exists(paths.Diagnostics));
    }
}
