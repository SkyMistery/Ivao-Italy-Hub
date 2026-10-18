using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Services;

/// <summary>
/// <c>diagnostics/requests-YYYYMMDD.txt</c>: one line for every request the hub answered (<see cref="RequestTimingsLine"/>),
/// one file per UTC day, and for every day that is over a <c>requests-YYYYMMDD-summary.txt</c> next to it (note
/// 2026-10-18-i-tempi-delle-richieste).
/// </summary>
/// <remarks>
/// <para>A request only puts its line in a queue; one task writes the queue to the file, so a request never waits for the
/// disk and never fails because of it. A full queue, a full day or a disk that refuses loses lines, never a request.</para>
/// <para>The summaries of the days that are over, and the removal of the days past <see cref="RequestTimingsOptions.RetainedDays"/>,
/// are done by the first process that has lived <see cref="RequestTimingsOptions.HousekeepingDelaySeconds"/>: on a host that
/// stops the hub after half a minute of quiet no process sees midnight, so the next day's does the work, away from the
/// first request it was started for. Two processes alive together write the same summary once: it is moved into place
/// only if it is not there yet.</para>
/// <para>The writer is a singleton and the hosted service at once, so the host's stop and the provider's dispose may come in
/// either order, and more than once (issue #302): whichever comes first stops the work and writes what is queued, the other
/// finds nothing left to do. The lines are read by the one task only, so none is written twice.</para>
/// </remarks>
public sealed partial class RequestTimingsLog : IHostedService, IDisposable
{
    private const int QueueLength = 4_096;

    private readonly Channel<RequestTimingsLine> _queue = Channel.CreateBounded<RequestTimingsLine>(
        new BoundedChannelOptions(QueueLength) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<DateOnly, long> _sizes = [];
    private readonly RequestTimingsOptions _options;
    private Task _pump = Task.CompletedTask;
    private Task _housekeeping = Task.CompletedTask;
    private bool _disposed;

    public RequestTimingsLog(IOptions<RequestTimingsOptions> options, HubPaths paths)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(paths);

        _options = options.Value;
        Folder = string.IsNullOrWhiteSpace(_options.Directory) ? paths.Diagnostics : _options.Directory;
    }

    public string Folder { get; }

    /// <summary>Whether this process writes lines (<see cref="RequestTimingsOptions.Enabled"/>).</summary>
    public bool Enabled => _options.Enabled == true;

    public static string FileName(DateOnly day) => $"requests-{day:yyyyMMdd}.txt";

    public static string SummaryName(DateOnly day) => $"requests-{day:yyyyMMdd}-summary.txt";

    /// <summary>Queues the line of a request; false when the queue is full and the line is lost.</summary>
    public bool TryWrite(RequestTimingsLine line) => _queue.Writer.TryWrite(line);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        CancellationToken stopping;
        lock (_gate)
        {
            if (!Enabled || _disposed)
            {
                return Task.CompletedTask;
            }

            // Taken here, while the source is alive: a token outlives the dispose of its source, the source's Token does not.
            stopping = _stopping.Token;
        }

        _pump = Task.Run(PumpAsync, CancellationToken.None);
        _housekeeping = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.HousekeepingDelaySeconds)), stopping);
                }
                catch (OperationCanceledException)
                {
                    // Stopped before: the next process does it.
                    return;
                }

                Housekeep(DateTime.UtcNow);
            },
            CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes what is still queued, for at most two seconds: the system that asked the process to stop will not wait longer.
    /// Safe after <see cref="Dispose"/> and a second time.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Stop();
        await Task.WhenAny(_pump, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
    }

    /// <summary>
    /// Writes the summary of every day that is over and has none yet, and removes the days past the retention. Never throws:
    /// a summary that cannot be written is written by the next process.
    /// </summary>
    public void Housekeep(DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        var oldestKept = today.AddDays(1 - Math.Max(1, _options.RetainedDays));
        try
        {
            if (!System.IO.Directory.Exists(Folder))
            {
                return;
            }

            foreach (var file in System.IO.Directory.EnumerateFiles(Folder, "requests-*.txt"))
            {
                if (DayOf(Path.GetFileName(file)) is not { } match)
                {
                    continue;
                }

                if (match.Day < oldestKept)
                {
                    Quietly(() => File.Delete(file));
                }
                else if (!match.IsSummary && match.Day < today && !File.Exists(Path.Combine(Folder, SummaryName(match.Day))))
                {
                    Quietly(() => Summarise(match.Day, file));
                }
            }
        }
#pragma warning disable CA1031 // Housekeeping a diagnostics folder must never become a failure of the hub.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Console.Error.WriteLine($"Could not tidy the request timings: {exception.Message}");
        }
    }

    /// <summary>Stops like <see cref="StopAsync"/> if the host has not, waiting as long for what is queued; safe twice.</summary>
    public void Dispose()
    {
        Stop();
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        // No sync context runs the pump, and it never waits for anything but the queue and the disk.
        _pump.Wait(TimeSpan.FromSeconds(2));
        lock (_gate)
        {
            _stopping.Dispose();
        }
    }

    /// <summary>Cancels the housekeeping and closes the queue; the pump then writes what is in it and ends.</summary>
    private void Stop()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _stopping.Cancel();
            }
        }

        _queue.Writer.TryComplete();
    }

    private void Summarise(DateOnly day, string file)
    {
        var lines = File.ReadLines(file).Select(RequestTimingsLine.Parse).OfType<RequestTimingsLine>().ToList();
        var summary = RequestTimingsSummary.Build(day, lines);
        var temporary = Path.Combine(Folder, $"{SummaryName(day)}.{Environment.ProcessId}.tmp");
        File.WriteAllText(temporary, summary);
        try
        {
            File.Move(temporary, Path.Combine(Folder, SummaryName(day)), overwrite: false);
        }
        catch (IOException)
        {
            // Another process wrote it first: the same summary of the same lines.
            File.Delete(temporary);
        }
    }

    private async Task PumpAsync()
    {
        var reader = _queue.Reader;
        var batch = new SortedDictionary<DateOnly, StringBuilder>();
        try
        {
            while (await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out var line))
                {
                    var day = DateOnly.FromDateTime(line.AtUtc);
                    if (!batch.TryGetValue(day, out var text))
                    {
                        batch[day] = text = new StringBuilder();
                    }

                    text.Append(line.Format()).Append('\n');
                }

                foreach (var (day, text) in batch)
                {
                    Quietly(() => Append(day, text.ToString()));
                }

                batch.Clear();
            }
        }
#pragma warning disable CA1031 // The writer stops; the requests do not.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Console.Error.WriteLine($"The request timings stopped: {exception.Message}");
        }
    }

    private void Append(DateOnly day, string text)
    {
        System.IO.Directory.CreateDirectory(Folder);
        var file = Path.Combine(Folder, FileName(day));
        var limit = (long)Math.Max(1, _options.MaxMegabytesPerDay) * 1024 * 1024;

        // Read from the disk the first time, because another process may have been writing all day; counted afterwards.
        if (!_sizes.TryGetValue(day, out var size))
        {
            size = File.Exists(file) ? new FileInfo(file).Length : 0;
        }

        if (size >= limit)
        {
            _sizes[day] = size;
            return;
        }

        var write = size == 0 ? Header() + text : text;
        if (size + write.Length >= limit)
        {
            write += string.Create(
                CultureInfo.InvariantCulture,
                $"# {_options.MaxMegabytesPerDay} MB reached at {DateTime.UtcNow:HH:mm:ss}Z: the rest of the day is not written.\n");
        }

        File.AppendAllText(file, write);
        _sizes[day] = size + Encoding.UTF8.GetByteCount(write);
    }

    private static string Header() =>
        "# requests-YYYYMMDD.txt: one line for every request the hub answered, appended when it ends. Times are UTC, durations ms.\n" +
        "# Files the web server serves from wwwroot/ never reach the hub and are not here. Nothing here names a person:\n" +
        "# the route is the pattern the hub declares, never the path that was asked, and there is no address, agent or VID.\n" +
        "#\n" +
        "# pid, n     the process and the number of this request in it: n 1 is the request that woke the hub (cold).\n" +
        "# up_ms      from the creation of the process to the moment the request reached the hub's code.\n" +
        "# total_ms   from there to the end of the response; ttfb_ms to its first byte (- when it had none).\n" +
        "# db_n/ms    database commands and their time, rows read included; open_n/ms connections opened and their time.\n" +
        "# http_n/ms  outbound calls (IVAO, weather...) and their time, retries included.\n" +
        "# jit_ms/n   code compiled by the runtime while the request ran, in the whole process; alloc_kb the same for memory.\n" +
        "#\n" +
        "# " + string.Join('\t', RequestTimingsLine.Columns) + "\n";

    private static void Quietly(Action action)
    {
        try
        {
            action();
        }
#pragma warning disable CA1031 // One file that cannot be written or removed loses its lines, nothing else.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Console.Error.WriteLine($"Request timings: {exception.Message}");
        }
    }

    private static (DateOnly Day, bool IsSummary)? DayOf(string fileName)
    {
        var match = DayPattern().Match(fileName);
        return match.Success && DateOnly.TryParseExact(match.Groups["day"].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? (day, match.Groups["summary"].Success)
            : null;
    }

    [GeneratedRegex(@"^requests-(?<day>\d{8})(?<summary>-summary)?\.txt$")]
    private static partial Regex DayPattern();
}

/// <summary>The settings of <see cref="RequestTimingsLog"/>, under <c>Diagnostics:Requests</c>.</summary>
public sealed class RequestTimingsOptions
{
    public const string SectionName = "Diagnostics:Requests";

    /// <summary>
    /// Null, the default: on in the hub's own process, off under a test host, where a process starts dozens of hosts. True or
    /// false decides for every process.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>How many days of files are kept, today included.</summary>
    public int RetainedDays { get; set; } = 14;

    /// <summary>Past this, the day's file stops growing until midnight UTC.</summary>
    public int MaxMegabytesPerDay { get; set; } = 20;

    /// <summary>How long a process lives before it writes the summaries of the days that are over.</summary>
    public int HousekeepingDelaySeconds { get; set; } = 20;

    /// <summary>Another folder than <c>diagnostics/</c>; empty for that one.</summary>
    public string? Directory { get; set; }
}
