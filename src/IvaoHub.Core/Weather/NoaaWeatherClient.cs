using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Weather;

/// <summary>
/// The first link of the weather chain: the data service of the American weather service, free and
/// without a key. Measured on 16 September 2026:
/// <list type="bullet">
/// <item>the whole world's current METARs come in one gzipped CSV, about 240 KB, which is what they
/// ask you to use instead of many small queries;</item>
/// <item>there is <b>no</b> equivalent file for TAFs (404), so those are asked for by code, in
/// batches — forty in one call answered forty rows;</item>
/// <item>history is <c>date</c> plus <c>hours</c> for METARs and <c>date</c> alone for TAFs
/// (<c>hours</c> on a TAF is a 400), and it reaches back thirty days exactly.</item>
/// </list>
/// <para>Their policy asks for at most a hundred calls a minute and for a user agent of our own, so
/// that automated filtering does not mistake the hub for a scraper.</para>
/// </summary>
public sealed class NoaaWeatherClient(HttpClient http, ILogger<NoaaWeatherClient> logger)
{
    /// <summary>The name of this source on a saved bulletin.</summary>
    public const string SourceName = "noaa";

    /// <summary>How many codes go in one query; measured to work, and short enough for a URL.</summary>
    private const int BatchSize = 40;

    /// <summary>
    /// Above this many airports the gzipped file of the whole world costs less than the queries it
    /// replaces: a hundred and fifty airports would otherwise be four calls of a few hundred
    /// kilobytes each, against one of 240 KB that also covers every airport asked for next time.
    /// </summary>
    private const int FileIsCheaperAbove = 60;

    /// <summary>Current METARs of the airports asked for, from the file or from the query.</summary>
    public async Task<IReadOnlyList<WeatherReport>> GetCurrentMetarsAsync(
        IReadOnlyCollection<string> icaos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(icaos);
        if (icaos.Count == 0)
        {
            return [];
        }

        return icaos.Count > FileIsCheaperAbove
            ? await FromWorldFileAsync(icaos, cancellationToken)
            : await FromQueryAsync(icaos, WeatherReportKind.Metar, query: null, cancellationToken);
    }

    /// <summary>Current TAFs; there is no file for these, so they are asked for in batches.</summary>
    public Task<IReadOnlyList<WeatherReport>> GetCurrentTafsAsync(
        IReadOnlyCollection<string> icaos,
        CancellationToken cancellationToken) =>
        icaos.Count == 0
            ? Task.FromResult<IReadOnlyList<WeatherReport>>([])
            : FromQueryAsync(icaos, WeatherReportKind.Taf, query: null, cancellationToken);

    /// <summary>
    /// What one airport published between two moments. <c>null</c> when the window is beyond what
    /// NOAA keeps or when it could not be reached.
    /// </summary>
    public async Task<IReadOnlyList<WeatherReport>?> GetHistoryAsync(
        string icao,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);

        if (DateTime.UtcNow - fromUtc > IWeatherSource.HistoryWindow)
        {
            logger.LogInformation(
                "The weather of {Icao} at {From:u} is older than NOAA keeps; answering that it is unavailable.",
                icao,
                fromUtc);
            return null;
        }

        // The end of the window and how far back from it: that is the shape NOAA takes. An hour of
        // margin on each side, because a bulletin is issued before the moment it describes.
        var end = toUtc.AddHours(1);
        var hours = (int)Math.Ceiling((end - fromUtc.AddHours(-1)).TotalHours);
        var stamp = end.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);

        var metars = await FromQueryAsync(
            [icao],
            WeatherReportKind.Metar,
            $"&date={stamp}&hours={Math.Clamp(hours, 1, 72)}",
            cancellationToken,
            failureIsNull: true);

        // A TAF refuses "hours": the date alone gives the bulletin in force then.
        var tafs = await FromQueryAsync(
            [icao],
            WeatherReportKind.Taf,
            $"&date={stamp}",
            cancellationToken,
            failureIsNull: true);

        if (metars is null && tafs is null)
        {
            return null;
        }

        return [.. (metars ?? []).Concat(tafs ?? []).OrderBy(report => report.IssuedAt)];
    }

    private async Task<IReadOnlyList<WeatherReport>> FromQueryAsync(
        IReadOnlyCollection<string> icaos,
        WeatherReportKind kind,
        string? query,
        CancellationToken cancellationToken) =>
        await FromQueryAsync(icaos, kind, query, cancellationToken, failureIsNull: false) ?? [];

    private async Task<IReadOnlyList<WeatherReport>?> FromQueryAsync(
        IReadOnlyCollection<string> icaos,
        WeatherReportKind kind,
        string? query,
        CancellationToken cancellationToken,
        bool failureIsNull)
    {
        var path = kind == WeatherReportKind.Metar ? "metar" : "taf";
        var reports = new List<WeatherReport>();
        var reached = false;

        foreach (var batch in Batches(icaos))
        {
            var url = $"/api/data/{path}?ids={string.Join(',', batch)}&format=json{query}";
            using var response = await SendAsync(url, cancellationToken);
            if (response is null || !response.IsSuccessStatusCode)
            {
                logger.LogWarning("NOAA answered {Url} with {Status}.", url, (int?)response?.StatusCode ?? 0);
                continue;
            }

            reached = true;
            try
            {
                using var document = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    cancellationToken: cancellationToken);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (Read(item, kind) is { } report)
                    {
                        reports.Add(report);
                    }
                }
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "NOAA answered {Url} with something that is not JSON.", url);
            }
        }

        return !reached && failureIsNull ? null : reports;
    }

    /// <summary>
    /// The current observation of every airport in the world, in one gzipped CSV. The first line is
    /// the header; the raw bulletin is the first field and is quoted, and a METAR never contains a
    /// quotation mark, which is what makes reading it this cheaply honest.
    /// </summary>
    private async Task<IReadOnlyList<WeatherReport>> FromWorldFileAsync(
        IReadOnlyCollection<string> icaos,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync("/data/cache/metars.cache.csv.gz", cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            logger.LogWarning("NOAA did not serve the world METAR file ({Status}).", (int?)response?.StatusCode ?? 0);
            return [];
        }

        var wanted = new HashSet<string>(icaos.Select(icao => icao.ToUpperInvariant()), StringComparer.Ordinal);
        var reports = new List<WeatherReport>();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (ReadCsvLine(line) is not { } report || !wanted.Contains(report.Icao))
            {
                continue;
            }

            reports.Add(report);
            if (reports.Count == wanted.Count)
            {
                break;
            }
        }

        logger.LogInformation(
            "Read {Count} of {Wanted} METAR(s) from the NOAA world file.",
            reports.Count,
            wanted.Count);

        return reports;
    }

    internal static WeatherReport? ReadCsvLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith('"'))
        {
            return null;
        }

        var end = line.IndexOf('"', 1);
        if (end < 0)
        {
            return null;
        }

        var raw = line[1..end];
        var fields = line[(end + 1)..].Split(',');
        if (fields.Length < 3 || string.IsNullOrWhiteSpace(fields[1]))
        {
            return null;
        }

        if (!DateTime.TryParse(
            fields[2],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var observed))
        {
            return null;
        }

        return new WeatherReport(
            fields[1].ToUpperInvariant(),
            WeatherReportKind.Metar,
            observed,
            raw.Trim(),
            SourceName);
    }

    internal static WeatherReport? Read(JsonElement item, WeatherReportKind kind)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var icao = Text(item, "icaoId");
        var raw = Text(item, kind == WeatherReportKind.Metar ? "rawOb" : "rawTAF") ?? Text(item, "rawOb");
        var issued = Moment(item, kind == WeatherReportKind.Metar ? "reportTime" : "issueTime")
            ?? Moment(item, "receiptTime");

        if (icao is null || raw is null || issued is null)
        {
            return null;
        }

        return new WeatherReport(icao.ToUpperInvariant(), kind, issued.Value, raw, SourceName);
    }

    private async Task<HttpResponseMessage?> SendAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await http.GetAsync(new Uri(url, UriKind.Relative), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning(exception, "NOAA could not be reached at {Url}.", url);
            return null;
        }
    }

    private static IEnumerable<string[]> Batches(IReadOnlyCollection<string> icaos) =>
        icaos
            .Select(icao => icao.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Chunk(BatchSize);

    private static string? Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() is { Length: > 0 } text ? text : null
            : null;

    private static DateTime? Moment(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String when DateTime.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed) => parsed,
            JsonValueKind.Number when value.TryGetInt64(out var unix) =>
                DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime,
            _ => null,
        };
    }
}
