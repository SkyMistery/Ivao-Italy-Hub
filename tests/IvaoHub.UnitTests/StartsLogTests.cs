using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// <c>diagnostics/starts.txt</c> (notes 2026-09-28-i-job-quando-passenger-spegne-l-hub and 2026-09-28-l-avvio-a-freddo):
/// what each line says, and the verdict on the process before, read by its pid as vIPI learnt to.
/// </summary>
public sealed class StartsLogTests : IDisposable
{
    private static readonly BuildInfo Build = new("0.2.4", "0123456789abcdef0123456789abcdef01234567", DateTime.UnixEpoch, ".NET 10.0.0");
    private static readonly DateTime Now = new(2026, 9, 28, 21, 14, 7, DateTimeKind.Utc);
    private static readonly ProcessMemory Memory = new(180L << 20, 210L << 20, 45L << 20);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ivaohub-starts-{Guid.NewGuid():N}");
    private readonly HubPaths _paths;

    public StartsLogTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "config"));
        File.WriteAllText(Path.Combine(_root, "config", "division.json"), "{}");
        _paths = HubPaths.Resolve(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void TheStartLineSaysTheBuildHowLongItTookWhereTheTimeWentAndTheMemory()
    {
        var line = StartsLog.StartLine(
            Now,
            4242,
            Build,
            TimeSpan.FromMilliseconds(1754),
            "none, first start in this file",
            Memory,
            [new StartupStep("runtime", TimeSpan.FromMilliseconds(25)), new StartupStep("models", TimeSpan.FromMilliseconds(861.4))]);

        Assert.Equal(
            "2026-09-28 21:14:07Z  START   pid 4242     0.2.4+0123456  ready in 1.75 s  previous: none, first start in this file  " +
            "memory 180 MB (peak 210 MB, managed heap 45 MB)  steps ms: runtime 25, models 861",
            line);
    }

    [Fact]
    public void TheStartLineSaysRightAfterTheTimeWhetherTheInitialisationWasSkipped()
    {
        var line = StartsLog.StartLine(
            Now,
            4242,
            Build,
            TimeSpan.FromMilliseconds(1204),
            "none, first start in this file",
            Memory,
            [new StartupStep("models", TimeSpan.FromMilliseconds(700)), new StartupStep("marker", TimeSpan.FromMilliseconds(41))],
            "initialisation skipped (marker of 0.2.4+0123456, 2026-09-28 20:00:00Z): migrations, content");

        Assert.Equal(
            "2026-09-28 21:14:07Z  START   pid 4242     0.2.4+0123456  ready in 1.20 s  " +
            "initialisation skipped (marker of 0.2.4+0123456, 2026-09-28 20:00:00Z): migrations, content  " +
            "previous: none, first start in this file  memory 180 MB (peak 210 MB, managed heap 45 MB)  steps ms: models 700, marker 41",
            line);
    }

    [Fact]
    public void TheStopLineSaysHowLongItLivedWhatItAnsweredAndTheMemory()
    {
        Assert.Equal(
            "2026-09-28 21:14:07Z  STOP    pid 4242     lived 00:00:53  requests 6  first answer at 2.10 s  " +
            "memory 180 MB (peak 210 MB, managed heap 45 MB)",
            StartsLog.StopLine(Now, 4242, TimeSpan.FromSeconds(53.4), 6, TimeSpan.FromMilliseconds(2104), Memory));

        Assert.Contains(
            "requests 0  no request answered",
            StartsLog.StopLine(Now, 4242, TimeSpan.FromDays(1.5), 0, null, Memory),
            StringComparison.Ordinal);
        Assert.Contains("lived 1d 12:00:00", StartsLog.StopLine(Now, 4242, TimeSpan.FromDays(1.5), 0, null, Memory), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrderlyStopIsReadForThePidOfTheLastStart()
    {
        string[] lines =
        [
            "# a comment",
            StartLine(Now.AddMinutes(-5), 100),
            StartsLog.SignalLine(Now.AddMinutes(-2), 100, "SIGTERM"),
            StartsLog.StopLine(Now.AddMinutes(-2), 100, TimeSpan.FromMinutes(3), 4, null, Memory),
        ];

        Assert.Equal("pid 100 stopped in order 00:02:00 ago", StartsLog.Previous(lines, Now, 200, _ => false));
    }

    [Fact]
    public void AStartWithoutItsStopIsAProcessThatDiedBadlyUnlessItIsStillRunning()
    {
        // Two processes at once: the last line is the other one's stop, and the process of the last start is alive.
        string[] lines =
        [
            StartLine(Now.AddMinutes(-10), 100),
            StartLine(Now.AddMinutes(-5), 101),
            StartsLog.StopLine(Now.AddMinutes(-1), 100, TimeSpan.FromMinutes(9), 4, null, Memory),
        ];

        Assert.Equal("pid 101 is still running: two processes at once", StartsLog.Previous(lines, Now, 200, pid => pid == 101));

        var died = StartsLog.Previous(lines, Now, 200, _ => false);
        Assert.StartsWith("!! pid 101 did NOT stop in order, it had started 00:05:00 ago", died, StringComparison.Ordinal);
        Assert.DoesNotContain("from outside", died, StringComparison.Ordinal);

        // The same pid as ours: that process is gone and the system gave the number again.
        Assert.StartsWith("!! pid 101", StartsLog.Previous(lines, Now, 101, _ => true), StringComparison.Ordinal);
    }

    [Fact]
    public void ASignalWithoutAStopIsAProcessStoppedFromOutside()
    {
        string[] lines = [StartLine(Now.AddMinutes(-1), 100), StartsLog.SignalLine(Now.AddSeconds(-40), 100, "SIGTERM")];

        Assert.EndsWith(
            "; it had received SIGTERM 00:00:40 ago and was stopped from outside before it could close",
            StartsLog.Previous(lines, Now, 200, _ => false),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyFileHasNoPreviousProcess()
    {
        Assert.Equal("none, first start in this file", StartsLog.Previous([], Now, 200, _ => true));
        Assert.Equal("none, first start in this file", StartsLog.Previous(["# only the header", "garbage"], Now, 200, _ => true));
    }

    [Fact]
    public void TheFileStartsWithItsExplanationAndIsAppendedTo()
    {
        StartsLog.AppendStart(_paths, lines =>
        {
            Assert.Empty(lines);
            return StartLine(Now, 100);
        });
        StartsLog.Append(_paths, StartsLog.StopLine(Now.AddSeconds(30), 100, TimeSpan.FromSeconds(31), 2, null, Memory));
        StartsLog.AppendStart(_paths, lines => StartsLog.Previous(lines, Now.AddSeconds(40), 101, _ => false));

        var written = File.ReadAllLines(Path.Combine(_root, "diagnostics", StartsLog.FileName));

        Assert.StartsWith("# starts.txt", written[0], StringComparison.Ordinal);
        Assert.Equal("pid 100 stopped in order 00:00:10 ago", written[^1]);
        Assert.Contains("  STOP    pid 100 ", written[^2], StringComparison.Ordinal);
        Assert.Single(written, line => line.StartsWith("# starts.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void AFullFileKeepsItsNewestLinesAndItsExplanation()
    {
        var file = Path.Combine(_root, "diagnostics", StartsLog.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllLines(file, Enumerable.Range(0, StartsLog.MostLines + 1).Select(n => StartLine(Now.AddSeconds(n), n)));

        StartsLog.AppendStart(_paths, _ => StartLine(Now.AddHours(1), 99_999));

        var written = File.ReadAllLines(file);
        var events = written.Where(line => !line.StartsWith('#')).ToArray();

        Assert.StartsWith("# starts.txt", written[0], StringComparison.Ordinal);
        Assert.Contains(written, line => line.StartsWith($"# {StartsLog.MostLines + 1 - StartsLog.KeptLines} older lines removed", StringComparison.Ordinal));
        Assert.Equal(StartsLog.KeptLines + 1, events.Length);
        Assert.Contains($"pid {StartsLog.MostLines} ", events[^2], StringComparison.Ordinal);
        Assert.Contains("pid 99999 ", events[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatCannotBeWrittenLosesTheLineAndNothingElse()
    {
        // diagnostics/ is taken by a file: the folder cannot be created.
        File.WriteAllText(_paths.Diagnostics, "in the way");

        StartsLog.AppendStart(_paths, _ => "a start");
        StartsLog.Append(_paths, "a stop");

        Assert.Equal("in the way", File.ReadAllText(_paths.Diagnostics));
    }

    [Fact]
    public void TheTimingsCountEachStepFromTheEndOfThePreviousOne()
    {
        var timings = new StartupTimings(TimeSpan.FromMilliseconds(30));
        Thread.Sleep(20);
        timings.Step("first");
        timings.Step("second");

        var steps = timings.Steps;
        Assert.Equal(["runtime", "first", "second"], steps.Select(step => step.Name));
        Assert.Equal(TimeSpan.FromMilliseconds(30), steps[0].Duration);
        Assert.True(steps[1].Duration >= TimeSpan.FromMilliseconds(20));
        Assert.True(timings.Elapsed >= steps.Aggregate(TimeSpan.Zero, (sum, step) => sum + step.Duration));

        // A clock that went backwards is not a negative first step.
        Assert.Equal(TimeSpan.Zero, new StartupTimings(TimeSpan.FromSeconds(-1)).Steps[0].Duration);
    }

    private static string StartLine(DateTime at, int pid) =>
        StartsLog.StartLine(at, pid, Build, TimeSpan.FromSeconds(2), "none, first start in this file", Memory, []);
}
