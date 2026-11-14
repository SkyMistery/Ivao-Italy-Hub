using System.Globalization;
using System.Text;
using IvaoHub.Core.Content;

namespace IvaoHub.Core.Services;

/// <summary>
/// One request in <c>diagnostics/requests-YYYYMMDD.txt</c> (note 2026-10-18-i-tempi-delle-richieste): when it ended, which
/// process answered it and how far into that process's life, what was asked as a route pattern, and where its time went.
/// </summary>
/// <remarks>
/// <para>Nothing in it names a person, and nothing in it is what was asked of a route: the route is the pattern the hub
/// declares (<c>/api/content/public/{kind}/{slug}</c>), never the path the visitor sent, never a query string. No address,
/// no user agent, no VID, no cookie.</para>
/// <para>Tab separated, one request per line, so that a spreadsheet or three lines of a script read it; a line starting with
/// <c>#</c> is a comment.</para>
/// <para>Issue #402 (note 2026-11-14-le-richieste-in-corso-nella-diagnostica): a data block's route names its type when the
/// type is registered, and <see cref="Others"/> is the last column, so the eighteen before it did not move. A line written
/// before it has eighteen columns and still reads, with <see cref="Others"/> null.</para>
/// </remarks>
public sealed record RequestTimingsLine(
    DateTime AtUtc,
    int Pid,
    long Number,
    TimeSpan Uptime,
    string Method,
    int Status,
    string Route,
    TimeSpan Total,
    TimeSpan? FirstByte,
    long Commands,
    TimeSpan CommandTime,
    long Opens,
    TimeSpan OpenTime,
    long Calls,
    TimeSpan CallTime,
    TimeSpan Jit,
    long JitMethods,
    long AllocatedKb,
    long? Others = null)
{
    public const string StampFormat = "yyyy-MM-dd HH:mm:ss.fff'Z'";

    /// <summary>The names of the columns, in order; the line of the header says them once more for whoever opens the file.</summary>
    /// <remarks>
    /// <c>others_n</c>: the other requests that ran at some moment of this one's life, the ones already running when it
    /// began and the ones that began while it ran. With 0, <c>jit_ms</c> and <c>alloc_kb</c>, which are the whole
    /// process's, are this request's alone.
    /// </remarks>
    public static readonly IReadOnlyList<string> Columns =
    [
        "time", "pid", "n", "up_ms", "method", "status", "route", "total_ms", "ttfb_ms",
        "db_n", "db_ms", "open_n", "open_ms", "http_n", "http_ms", "jit_ms", "jit_n", "alloc_kb", "others_n",
    ];

    /// <summary>The columns of a line written before <c>others_n</c> (issue #402).</summary>
    private static readonly int ColumnsBeforeOthers = Columns.Count - 1;

    /// <summary>
    /// The route of a data block whose type the registry knows: <c>/api/blocks/data/networkStats</c>. The caller passes the
    /// name the registry holds, never the text of the request: a type nobody registered keeps the pattern.
    /// </summary>
    public static string DataBlockRoute(string registeredType)
    {
        ArgumentException.ThrowIfNullOrEmpty(registeredType);
        return ContentEndpoints.BlockDataPattern.Replace("{type}", registeredType, StringComparison.Ordinal);
    }

    public string Format()
    {
        var text = new StringBuilder(160);
        text.Append(AtUtc.ToString(StampFormat, CultureInfo.InvariantCulture)).Append('\t')
            .Append(Invariant(Pid)).Append('\t')
            .Append(Invariant(Number)).Append('\t')
            .Append(Ms(Uptime)).Append('\t')
            .Append(Clean(Method)).Append('\t')
            .Append(Invariant(Status)).Append('\t')
            .Append(Clean(Route)).Append('\t')
            .Append(Ms(Total)).Append('\t')
            .Append(FirstByte is { } firstByte ? Ms(firstByte) : "-").Append('\t')
            .Append(Invariant(Commands)).Append('\t')
            .Append(Ms(CommandTime)).Append('\t')
            .Append(Invariant(Opens)).Append('\t')
            .Append(Ms(OpenTime)).Append('\t')
            .Append(Invariant(Calls)).Append('\t')
            .Append(Ms(CallTime)).Append('\t')
            .Append(Ms(Jit)).Append('\t')
            .Append(Invariant(JitMethods)).Append('\t')
            .Append(Invariant(AllocatedKb)).Append('\t')
            .Append(Others is { } others ? Invariant(others) : "-");
        return text.ToString();
    }

    /// <summary>A line of the file read back; null for a comment, an empty line or one that is not ours.</summary>
    public static RequestTimingsLine? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith('#'))
        {
            return null;
        }

        var parts = text.Split('\t');
        if ((parts.Length != Columns.Count && parts.Length != ColumnsBeforeOthers)
            || !DateTime.TryParseExact(
                parts[0],
                StampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var at))
        {
            return null;
        }

        try
        {
            return new RequestTimingsLine(
                at,
                int.Parse(parts[1], CultureInfo.InvariantCulture),
                long.Parse(parts[2], CultureInfo.InvariantCulture),
                ReadMs(parts[3]),
                parts[4],
                int.Parse(parts[5], CultureInfo.InvariantCulture),
                parts[6],
                ReadMs(parts[7]),
                parts[8] == "-" ? null : ReadMs(parts[8]),
                long.Parse(parts[9], CultureInfo.InvariantCulture),
                ReadMs(parts[10]),
                long.Parse(parts[11], CultureInfo.InvariantCulture),
                ReadMs(parts[12]),
                long.Parse(parts[13], CultureInfo.InvariantCulture),
                ReadMs(parts[14]),
                ReadMs(parts[15]),
                long.Parse(parts[16], CultureInfo.InvariantCulture),
                long.Parse(parts[17], CultureInfo.InvariantCulture),
                parts.Length == ColumnsBeforeOthers || parts[18] == "-" ? null : long.Parse(parts[18], CultureInfo.InvariantCulture));
        }
        catch (FormatException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// What a request that reached no endpoint is written as: the first segment of its path, when it is a plain word
    /// (<c>favicon.ico</c>, <c>assets</c>, <c>wp-login.php</c>), and nothing of it otherwise. A digit is never a plain word:
    /// a segment with one may be somebody's number.
    /// </summary>
    public static string NoEndpoint(string? path)
    {
        var segment = (path ?? string.Empty).TrimStart('/').Split('/')[0];
        var plain = segment.Length is > 0 and <= 40
                    && segment.All(character => char.IsAsciiLetter(character) || character is '.' or '-' or '_');
        return plain ? $"(no endpoint) /{segment}" : "(no endpoint)";
    }

    private static string Ms(TimeSpan duration) =>
        Math.Max(0, duration.TotalMilliseconds).ToString("0", CultureInfo.InvariantCulture);

    private static TimeSpan ReadMs(string text) =>
        TimeSpan.FromMilliseconds(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));

    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A tab or a line break would move the columns: a route pattern has none, and this says so.</summary>
    private static string Clean(string text) => string.IsNullOrEmpty(text)
        ? "-"
        : text.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
}
