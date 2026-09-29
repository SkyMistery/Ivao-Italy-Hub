using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace IvaoHub.Core.Services;

/// <summary>
/// <c>diagnostics/starts.txt</c>: one line for every start of the process and one for every stop, appended, never
/// rewritten (note 2026-09-28-i-job-quando-passenger-spegne-l-hub, decided with #165; the step timings, note
/// 2026-09-28-l-avvio-a-freddo, decided with #166). It is vIPI's <c>avvii.txt</c>, lessons included.
/// </summary>
/// <remarks>
/// <para><c>startup.txt</c> is rewritten at every start, so it says when the last one happened and never how many there
/// were: three starts a day (Passenger stopping an idle hub, expected) and forty (something breaking) leave the same file.
/// This one keeps the history, and says for every start how long it took, where the time went, and what happened to
/// the process before.</para>
/// <para>An orderly stop writes its <c>STOP</c> line; a crash, an out of memory or a process killed does not. So a
/// <c>START</c> whose previous process has no <c>STOP</c> and is not running any more is a process that died badly, and the
/// next start says so in words. The verdict looks at the <b>pid</b> of the last start and not at the last line: Passenger
/// sometimes keeps two processes alive together, and the last line may belong to one that is still running.</para>
/// <para>A <c>SIGNAL</c> line is written when the system asks the process to stop, whichever of the two handlers of that
/// signal runs first: a <c>SIGNAL</c> without its <c>STOP</c> is a process stopped from outside before it could close.</para>
/// <para>It never throws and never holds a start back: a file that cannot be written only loses the line. It holds no
/// secret: times, a pid, the version, durations, counts and memory. The web server denies <c>diagnostics/</c>.</para>
/// </remarks>
public static partial class StartsLog
{
    public const string FileName = "starts.txt";

    /// <summary>
    /// Past this, the oldest lines go at the next start. Two lines per life: on a host that stops an idle hub after half a
    /// minute that is days of history, and about 400 KB, which is what one opens over FTP without thinking.
    /// </summary>
    public const int MostLines = 2_000;

    /// <summary>What a trim keeps.</summary>
    public const int KeptLines = 1_000;

    private const string Start = "START";
    private const string Stop = "STOP";
    private const string Signal = "SIGNAL";

    private const string StampFormat = "yyyy-MM-dd HH:mm:ss'Z'";
    private static readonly int StampLength = "2026-09-28 21:14:07Z".Length;

    /// <summary>The start, the stop and the signal are written by three threads, and the last two arrive together.</summary>
    private static readonly Lock Pen = new();

    public static string StartLine(
        DateTime atUtc,
        int pid,
        BuildInfo build,
        TimeSpan took,
        string previous,
        ProcessMemory memory,
        IReadOnlyList<StartupStep> steps,
        string? initialisation = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(steps);

        var stepList = string.Join(", ", steps.Select(step => string.Create(
            CultureInfo.InvariantCulture, $"{step.Name} {step.Duration.TotalMilliseconds:0}")));

        // Whether the start initialised the database or found it initialised, and why (note
        // 2026-09-28-il-marcatore-d-inizializzazione): right after the time, which it explains.
        var init = initialisation is null ? string.Empty : $"  {initialisation}";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Stamp(atUtc)}  {Start,-6}  pid {pid,-7}  {build.Stamp}  ready in {took.TotalSeconds:0.00} s{init}  previous: {previous}  " +
            $"{memory}  steps ms: {stepList}");
    }

    /// <summary>
    /// The line of an orderly stop. <c>firstAnswer</c> is when the first response was finished, counted from the creation
    /// of the process: the wait of the visitor whose request woke the hub; null when no request came.
    /// </summary>
    public static string StopLine(DateTime atUtc, int pid, TimeSpan lived, long requests, TimeSpan? firstAnswer, ProcessMemory memory)
    {
        var first = firstAnswer is { } answer
            ? string.Create(CultureInfo.InvariantCulture, $"first answer at {answer.TotalSeconds:0.00} s")
            : "no request answered";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Stamp(atUtc)}  {Stop,-6}  pid {pid,-7}  lived {Duration(lived)}  requests {requests}  {first}  {memory}");
    }

    public static string SignalLine(DateTime atUtc, int pid, string signal) =>
        string.Create(CultureInfo.InvariantCulture, $"{Stamp(atUtc)}  {Signal,-6}  pid {pid,-7}  {signal} from the system");

    /// <summary>
    /// What happened to the process of the last start, in words, read by its pid. <c>alive</c> says whether a process
    /// with that pid is still running and is the hub (<see cref="IsHubRunning"/>); when the last start carries
    /// <c>currentPid</c>, this process's number, that process died and the system gave the number again.
    /// </summary>
    public static string Previous(IReadOnlyList<string> lines, DateTime nowUtc, int currentPid, Func<int, bool> alive)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(alive);

        var index = lines.Count - 1;
        while (index >= 0 && Read(lines[index]) is not { Kind: Start })
        {
            index--;
        }

        if (index < 0 || Read(lines[index]) is not { Pid: var pid, At: var startedAt })
        {
            return "none, first start in this file";
        }

        DateTime? signalled = null;
        string? signalName = null;
        for (var next = index + 1; next < lines.Count; next++)
        {
            if (Read(lines[next]) is not { } line || line.Pid != pid)
            {
                continue;
            }

            if (line.Kind == Stop)
            {
                return $"pid {pid} stopped in order {Duration(nowUtc - line.At)} ago";
            }

            if (line.Kind == Signal)
            {
                signalled = line.At;
                signalName = line.Rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }
        }

        if (pid != currentPid && alive(pid))
        {
            return $"pid {pid} is still running: two processes at once";
        }

        var outside = signalled is { } at
            ? $"; it had received {signalName} {Duration(nowUtc - at)} ago and was stopped from outside before it could close"
            : string.Empty;

        return $"!! pid {pid} did NOT stop in order, it had started {Duration(nowUtc - startedAt)} ago " +
               $"(a crash, out of memory, killed by the host, or a file overwritten by FTP){outside}";
    }

    /// <summary>Appends the line of a start, built from the lines already there; trims the file first when it is full.</summary>
    public static void AppendStart(HubPaths paths, Func<IReadOnlyList<string>, string> line)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(line);

        Write(paths, file =>
        {
            var lines = File.Exists(file) ? File.ReadAllLines(file) : [];
            var text = new StringBuilder();
            if (lines.Length == 0)
            {
                text.Append(Header());
            }

            text.Append(line(lines)).Append('\n');

            if (lines.Length > MostLines)
            {
                // At a start and never later: the only moment when a few milliseconds of disk take nothing from anybody.
                File.WriteAllText(
                    file,
                    Header()
                    + string.Create(
                        CultureInfo.InvariantCulture,
                        $"# {lines.Length - KeptLines} older lines removed on {Stamp(DateTime.UtcNow)}, so that the file stays small.\n")
                    + string.Join('\n', lines[^KeptLines..].Where(kept => !kept.StartsWith('#')))
                    + "\n");
            }

            File.AppendAllText(file, text.ToString());
        });
    }

    /// <summary>Appends one line: a stop or a signal.</summary>
    public static void Append(HubPaths paths, string line)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Write(paths, file => File.AppendAllText(file, line + "\n"));
    }

    /// <summary>
    /// Whether <paramref name="pid"/> is a running process with this process's name. The name is compared because
    /// numbers are given again: an hour-old pid may belong to another program by now.
    /// </summary>
    public static bool IsHubRunning(int pid)
    {
        try
        {
            using var other = Process.GetProcessById(pid);
            using var self = Process.GetCurrentProcess();
            return !other.HasExited && string.Equals(other.ProcessName, self.ProcessName, StringComparison.Ordinal);
        }
#pragma warning disable CA1031 // No such process, or one we may not look at: not a running hub.
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }

    /// <summary>A duration without ticks: <c>00:42:26</c>, and the days in front when there are some.</summary>
    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration.Days > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{duration.Days}d {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}");
    }

    private static void Write(HubPaths paths, Action<string> write)
    {
        try
        {
            lock (Pen)
            {
                Directory.CreateDirectory(paths.Diagnostics);
                write(Path.Combine(paths.Diagnostics, FileName));
            }
        }
#pragma warning disable CA1031 // Telling the story of a start must never become a failed start, nor a failed stop.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Console.Error.WriteLine($"Could not write {FileName}: {exception.Message}");
        }
    }

    private sealed record Line(DateTime At, string Kind, int Pid, string Rest);

    private static Line? Read(string text)
    {
        if (text.Length < StampLength || text.StartsWith('#')
            || !DateTime.TryParseExact(
                text[..StampLength],
                StampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var at))
        {
            return null;
        }

        var match = EventPattern().Match(text[StampLength..]);
        return match.Success
            ? new Line(at, match.Groups["kind"].Value, int.Parse(match.Groups["pid"].Value, CultureInfo.InvariantCulture), match.Groups["rest"].Value)
            : null;
    }

    [GeneratedRegex(@"^\s+(?<kind>START|STOP|SIGNAL)\s+pid (?<pid>\d+)\s+(?<rest>.*)$")]
    private static partial Regex EventPattern();

    private static string Stamp(DateTime utc) => utc.ToString(StampFormat, CultureInfo.InvariantCulture);

    private static string Header() =>
        "# starts.txt: one line for every start of the hub and one for every stop, always appended. The times are UTC.\n" +
        "#\n" +
        "# START   ready in = from the creation of the process to the moment it accepts requests; initialisation = full\n" +
        "#         (and why) or skipped (and which steps: nothing changed since the last full one); previous = what\n" +
        "#         happened to the process of the start before; memory when ready; steps ms = where the time went.\n" +
        "# STOP    an orderly stop: how long the process lived, the requests it answered, when the first answer left\n" +
        "#         (from the creation of the process: what the visitor who woke it waited), memory and its peak.\n" +
        "# SIGNAL  the system asked that pid to stop (Passenger stopping an idle hub sends SIGTERM).\n" +
        "#\n" +
        "# A few starts in quiet hours, each after an orderly stop, is Passenger stopping an idle hub: expected. A start\n" +
        "# that says !! means the process before died without closing: a crash, out of memory, killed by the host, or a file\n" +
        "# overwritten while it ran. Right after an upload that is expected once. Otherwise read logs/ and startup-error.txt.\n" +
        "#\n";
}

/// <summary>The memory of the process at one moment: what it uses, its peak, and the managed heap within it.</summary>
public readonly record struct ProcessMemory(long WorkingSet, long PeakWorkingSet, long ManagedHeap)
{
    public static ProcessMemory Now()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return new ProcessMemory(Environment.WorkingSet, process.PeakWorkingSet64, GC.GetTotalMemory(forceFullCollection: false));
        }
#pragma warning disable CA1031 // A platform that cannot say is written as zero, never as a failure.
        catch (Exception)
#pragma warning restore CA1031
        {
            return new ProcessMemory(Environment.WorkingSet, 0, GC.GetTotalMemory(forceFullCollection: false));
        }
    }

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"memory {Mb(WorkingSet)} MB (peak {Mb(PeakWorkingSet)} MB, managed heap {Mb(ManagedHeap)} MB)");

    private static long Mb(long bytes) => bytes / (1024 * 1024);
}
