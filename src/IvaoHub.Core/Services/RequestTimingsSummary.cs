using System.Globalization;
using System.Text;

namespace IvaoHub.Core.Services;

/// <summary>
/// The summary of one day of <c>requests-YYYYMMDD.txt</c> (note 2026-10-18-i-tempi-delle-richieste): how many requests,
/// how long they took, the cold ones apart from the warm ones, by route, and the slowest ten in full.
/// </summary>
/// <remarks>
/// A request is <b>cold</b> when it is the first its process answered: the visitor who woke the hub waited for the start
/// too, which the line does not count (it begins when the request reaches the code), so <c>up_ms</c> of a cold line is the
/// better measure of that wait. The second to the tenth request of a process are apart as well: they still compile code.
/// Percentiles are by nearest rank, on the total time of the request.
/// </remarks>
public static class RequestTimingsSummary
{
    public const int SlowestShown = 10;

    public static string Build(DateOnly day, IReadOnlyCollection<RequestTimingsLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var text = new StringBuilder();
        text.Append(Invariant($"# Summary of requests-{day:yyyyMMdd}.txt: the requests the hub answered on {day:yyyy-MM-dd} (UTC).\n"));
        text.Append("# Times in ms, percentiles of the total time by nearest rank. cold = the first request of its process (it waited\n");
        text.Append("# for the start too, up_ms says how long); early = the 2nd to the 10th; warm = the rest. db, open, http = the\n");
        text.Append("# median per request of the time on database commands, on opening connections, on outbound calls.\n");
        text.Append("# Files the web server serves from wwwroot/ never reach the hub, so they are not here.\n\n");

        var processes = lines.Select(line => line.Pid).Distinct().Count();
        text.Append(Invariant($"requests {lines.Count}  processes {processes}\n\n"));
        if (lines.Count == 0)
        {
            return text.ToString();
        }

        text.Append(Cells("", "n", "p50", "p90", "p99", "max", "db", "open", "http", "jit")).Append('\n');
        text.Append(Row("all", lines)).Append('\n');
        text.Append(Row("cold", [.. lines.Where(line => line.Number == 1)])).Append('\n');
        text.Append(Row("early", [.. lines.Where(line => line.Number is > 1 and <= 10)])).Append('\n');
        text.Append(Row("warm", [.. lines.Where(line => line.Number > 10)])).Append('\n');

        var cold = lines.Where(line => line.Number == 1).ToList();
        if (cold.Count > 0)
        {
            var up = cold.Select(line => line.Uptime.TotalMilliseconds).Order().ToList();
            text.Append(Invariant(
                $"cold: the request began {Percentile(up, 50):0} ms after the process was created (p50), {Percentile(up, 90):0} ms (p90)\n"));
        }

        text.Append("\nby route, most requested first\n");
        text.Append(Cells("method route", "n", "p50", "p90", "p99", "max", "db", "open", "http", "jit")).Append('\n');
        foreach (var route in lines
                     .GroupBy(line => $"{line.Method} {line.Route}", StringComparer.Ordinal)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            text.Append(Row(route.Key, [.. route])).Append('\n');
        }

        text.Append(Invariant($"\nthe {Math.Min(SlowestShown, lines.Count)} slowest, as they are in the file\n"));
        text.Append("# ").Append(string.Join('\t', RequestTimingsLine.Columns)).Append('\n');
        foreach (var line in lines.OrderByDescending(line => line.Total).Take(SlowestShown))
        {
            text.Append(line.Format()).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>The value at <paramref name="percent"/> of an ordered list, by nearest rank.</summary>
    public static double Percentile(IReadOnlyList<double> ordered, double percent)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        if (ordered.Count == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percent / 100 * ordered.Count);
        return ordered[Math.Clamp(rank, 1, ordered.Count) - 1];
    }

    private static string Row(string name, IReadOnlyCollection<RequestTimingsLine> lines)
    {
        if (lines.Count == 0)
        {
            return Cells(name, "0", "-", "-", "-", "-", "-", "-", "-", "-");
        }

        var totals = lines.Select(line => line.Total.TotalMilliseconds).Order().ToList();
        return Cells(
            name,
            Invariant($"{lines.Count}"),
            Ms(Percentile(totals, 50)),
            Ms(Percentile(totals, 90)),
            Ms(Percentile(totals, 99)),
            Ms(totals[^1]),
            Median(lines, line => line.CommandTime),
            Median(lines, line => line.OpenTime),
            Median(lines, line => line.CallTime),
            Median(lines, line => line.Jit));
    }

    private static string Cells(string name, params string[] cells)
    {
        var text = new StringBuilder();
        text.Append(name.Length > 56 ? name[..55] + "…" : name.PadRight(56));
        foreach (var cell in cells)
        {
            text.Append(cell.PadLeft(8));
        }

        return text.ToString().TrimEnd();
    }

    private static string Median(IReadOnlyCollection<RequestTimingsLine> lines, Func<RequestTimingsLine, TimeSpan> part) =>
        Ms(Percentile([.. lines.Select(line => part(line).TotalMilliseconds).Order()], 50));

    private static string Ms(double milliseconds) => milliseconds.ToString("0", CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
