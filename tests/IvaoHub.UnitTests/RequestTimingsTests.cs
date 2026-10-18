using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// <c>diagnostics/requests-YYYYMMDD.txt</c> and its summaries (note 2026-10-18-i-tempi-delle-richieste): what a line says
/// and reads back, what a request that reached no endpoint is written as, the summary of a day, and the housekeeping that
/// writes it and removes the old days.
/// </summary>
public sealed class RequestTimingsTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 18, 9, 28, 0, 473, DateTimeKind.Utc);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ivaohub-requests-{Guid.NewGuid():N}");
    private readonly HubPaths _paths;

    public RequestTimingsTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "config"));
        File.WriteAllText(Path.Combine(_root, "config", "division.json"), "{}");
        _paths = HubPaths.Resolve(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ALineIsTabSeparatedInTheOrderOfTheColumnsAndReadsBack()
    {
        var line = Line(Now, number: 1, route: "/api/content/public/{kind}/{slug}", totalMs: 408.4, firstByteMs: 400);

        var text = line.Format();

        Assert.Equal(
            "2026-10-18 09:28:00.473Z\t2491177\t1\t6420\tGET\t200\t/api/content/public/{kind}/{slug}\t408\t400\t3\t45\t1\t30\t0\t0\t120\t800\t2048",
            text);
        Assert.Equal(RequestTimingsLine.Columns.Count, text.Split('\t').Length);
        Assert.Equal(line with { Total = TimeSpan.FromMilliseconds(408), FirstByte = TimeSpan.FromMilliseconds(400) }, RequestTimingsLine.Parse(text));
    }

    [Fact]
    public void AResponseThatNeverStartedHasADashForItsFirstByte()
    {
        var text = Line(Now, firstByteMs: null).Format();

        Assert.Equal("-", text.Split('\t')[8]);
        Assert.Null(RequestTimingsLine.Parse(text)!.FirstByte);
    }

    [Theory]
    [InlineData("# a comment")]
    [InlineData("")]
    [InlineData("2026-10-18 09:28:00.473Z\tnot ours")]
    public void ACommentOrALineThatIsNotOursReadsAsNothing(string text) => Assert.Null(RequestTimingsLine.Parse(text));

    [Theory]
    [InlineData("/favicon.ico", "(no endpoint) /favicon.ico")]
    [InlineData("/assets/index-Bx81.js", "(no endpoint) /assets")]
    [InlineData("/wp-login.php", "(no endpoint) /wp-login.php")]
    [InlineData("/704798", "(no endpoint)")]
    [InlineData("/user704798/profile", "(no endpoint)")]
    [InlineData("/caf%C3%A9", "(no endpoint)")]
    [InlineData("/", "(no endpoint)")]
    [InlineData(null, "(no endpoint)")]
    public void ARequestThatReachedNoEndpointKeepsOnlyAPlainFirstWordOfItsPath(string? path, string expected) =>
        Assert.Equal(expected, RequestTimingsLine.NoEndpoint(path));

    [Fact]
    public void APercentileIsTheNearestRank()
    {
        double[] ordered = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];

        Assert.Equal(50, RequestTimingsSummary.Percentile(ordered, 50));
        Assert.Equal(90, RequestTimingsSummary.Percentile(ordered, 90));
        Assert.Equal(100, RequestTimingsSummary.Percentile(ordered, 99));
        Assert.Equal(10, RequestTimingsSummary.Percentile([10], 99));
        Assert.Equal(0, RequestTimingsSummary.Percentile([], 50));
    }

    [Fact]
    public void TheSummaryCountsTheDaySplitsColdFromWarmGroupsByRouteAndShowsTheSlowest()
    {
        List<RequestTimingsLine> lines =
        [
            Line(Now, pid: 1, number: 1, route: "/api/me", totalMs: 2000),
            Line(Now, pid: 1, number: 2, route: "/api/me", totalMs: 300),
            Line(Now, pid: 2, number: 1, route: "/api/me", totalMs: 1800),
            .. Enumerable.Range(3, 12).Select(number => Line(Now, pid: 2, number: number, route: "/api/blocks/data/{type}", totalMs: 20 + number)),
        ];

        var summary = RequestTimingsSummary.Build(new DateOnly(2026, 10, 18), lines);

        Assert.Contains("requests 15  processes 2", summary, StringComparison.Ordinal);
        Assert.Matches(@"\ncold\s+2\s+1800\s+2000\s+2000\s+2000", summary);
        Assert.Matches(@"\nearly\s+9\s", summary);
        Assert.Matches(@"\nwarm\s+4\s", summary);
        Assert.Contains("cold: the request began 6420 ms after the process was created", summary, StringComparison.Ordinal);

        // Most requested first: twelve to the blocks, three to /api/me.
        Assert.True(
            summary.IndexOf("GET /api/blocks/data/{type}", StringComparison.Ordinal) < summary.IndexOf("GET /api/me ", StringComparison.Ordinal));

        var slowest = summary[summary.IndexOf("the 10 slowest", StringComparison.Ordinal)..]
            .Split('\n')
            .Select(RequestTimingsLine.Parse)
            .OfType<RequestTimingsLine>()
            .ToList();
        Assert.Equal(10, slowest.Count);
        Assert.Equal(2000, slowest[0].Total.TotalMilliseconds);
        Assert.Equal(1800, slowest[1].Total.TotalMilliseconds);
    }

    [Fact]
    public void AnEmptyDayHasASummaryThatSaysSo()
    {
        var summary = RequestTimingsSummary.Build(new DateOnly(2026, 10, 18), []);

        Assert.Contains("requests 0  processes 0", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void HousekeepingSummarisesTheDaysThatAreOverAndNeverToday()
    {
        var log = Log();
        Directory.CreateDirectory(log.Folder);
        var yesterday = new DateOnly(2026, 10, 17);
        var today = new DateOnly(2026, 10, 18);
        File.WriteAllText(Path.Combine(log.Folder, RequestTimingsLog.FileName(yesterday)), "# header\n" + Line(Now.AddDays(-1)).Format() + "\n");
        File.WriteAllText(Path.Combine(log.Folder, RequestTimingsLog.FileName(today)), Line(Now).Format() + "\n");

        log.Housekeep(Now);

        var summary = File.ReadAllText(Path.Combine(log.Folder, RequestTimingsLog.SummaryName(yesterday)));
        Assert.Contains("requests 1  processes 1", summary, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(log.Folder, RequestTimingsLog.SummaryName(today))));
        Assert.Empty(Directory.GetFiles(log.Folder, "*.tmp"));
    }

    [Fact]
    public void ASummaryAlreadyWrittenIsNotWrittenAgain()
    {
        var log = Log();
        Directory.CreateDirectory(log.Folder);
        var yesterday = new DateOnly(2026, 10, 17);
        File.WriteAllText(Path.Combine(log.Folder, RequestTimingsLog.FileName(yesterday)), Line(Now.AddDays(-1)).Format() + "\n");
        File.WriteAllText(Path.Combine(log.Folder, RequestTimingsLog.SummaryName(yesterday)), "written by another process");

        log.Housekeep(Now);

        Assert.Equal("written by another process", File.ReadAllText(Path.Combine(log.Folder, RequestTimingsLog.SummaryName(yesterday))));
    }

    [Fact]
    public void HousekeepingRemovesTheDaysPastTheRetentionAndNothingElse()
    {
        var log = Log(retainedDays: 3);
        Directory.CreateDirectory(log.Folder);
        var kept = new DateOnly(2026, 10, 16);
        var gone = new DateOnly(2026, 10, 15);
        foreach (var name in new[] { RequestTimingsLog.FileName(kept), RequestTimingsLog.FileName(gone), RequestTimingsLog.SummaryName(gone) })
        {
            File.WriteAllText(Path.Combine(log.Folder, name), string.Empty);
        }

        File.WriteAllText(Path.Combine(log.Folder, "starts.txt"), "kept");

        log.Housekeep(Now);

        Assert.True(File.Exists(Path.Combine(log.Folder, RequestTimingsLog.FileName(kept))));
        Assert.False(File.Exists(Path.Combine(log.Folder, RequestTimingsLog.FileName(gone))));
        Assert.False(File.Exists(Path.Combine(log.Folder, RequestTimingsLog.SummaryName(gone))));
        Assert.True(File.Exists(Path.Combine(log.Folder, "starts.txt")));
    }

    [Fact]
    public void AStartSaysTheCodeCompiledInsideAStepAndWhatItCostBeforeReady()
    {
        var line = StartsLog.StartLine(
            Now,
            4242,
            new BuildInfo("0.9.2", "0123456789abcdef0123456789abcdef01234567", DateTime.UnixEpoch, ".NET 10.0.0"),
            TimeSpan.FromMilliseconds(6400),
            "none, first start in this file",
            new ProcessMemory(180L << 20, 210L << 20, 45L << 20),
            [new StartupStep("runtime", TimeSpan.FromMilliseconds(821), TimeSpan.FromMilliseconds(300)), new StartupStep("listening", TimeSpan.FromMilliseconds(30))],
            costs: new StartCosts(TimeSpan.FromMilliseconds(2400), 9100, 2, TimeSpan.FromMilliseconds(640), 7, TimeSpan.FromMilliseconds(95)));

        Assert.EndsWith(
            "memory 180 MB (peak 210 MB, managed heap 45 MB)  jit 2400 ms in 9100 methods  db 2 opens 640 ms, 7 commands 95 ms  " +
            "steps ms: runtime 821 (jit 300), listening 30",
            line,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AStopSaysTheCodeCompiledInTheWholeLife()
    {
        var line = StartsLog.StopLine(Now, 4242, TimeSpan.FromSeconds(40), 7, TimeSpan.FromSeconds(8.14), new ProcessMemory(0, 0, 0), TimeSpan.FromMilliseconds(4100));

        Assert.EndsWith("  jit 4100 ms in all", line, StringComparison.Ordinal);
    }

    private RequestTimingsLog Log(int retainedDays = 14) =>
        new(Options.Create(new RequestTimingsOptions { Enabled = true, RetainedDays = retainedDays }), _paths);

    private static RequestTimingsLine Line(
        DateTime at,
        int pid = 2491177,
        long number = 1,
        string route = "/api/me",
        double totalMs = 200,
        double? firstByteMs = 190) => new(
            at,
            pid,
            number,
            TimeSpan.FromMilliseconds(6420),
            "GET",
            200,
            route,
            TimeSpan.FromMilliseconds(totalMs),
            firstByteMs is { } firstByte ? TimeSpan.FromMilliseconds(firstByte) : null,
            3,
            TimeSpan.FromMilliseconds(45),
            1,
            TimeSpan.FromMilliseconds(30),
            0,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(120),
            800,
            2048);
}
