namespace IvaoHub.Core.Weather;

/// <summary>Which bulletin this is: what the weather was, or what it is expected to be.</summary>
public enum WeatherReportKind
{
    /// <summary>An observation.</summary>
    Metar,

    /// <summary>A forecast.</summary>
    Taf,
}

/// <summary>
/// One bulletin, as it was published. The hub never parses it: a validator reads METARs, and the
/// one check that needs numbers (<c>vmc</c>) reads the few fields it needs from the raw text itself.
/// <para><see cref="Source"/> says who answered — <c>noaa</c>, <c>ivao</c>, <c>vatsim</c> — because a
/// validator looking at a five year old decision deserves to know where the weather came from.</para>
/// </summary>
public sealed record WeatherReport(
    string Icao,
    WeatherReportKind Kind,
    DateTime IssuedAt,
    string Raw,
    string Source);

/// <summary>
/// Where the weather comes from, for the whole hub. A module never names a provider: it asks this
/// (plan section 4.2, the same question as "does this name IVAO?").
/// <para>Two questions, because they are answered by different things: what the weather is now, for
/// the job that keeps the airports of open tours covered, and what it was during a flight, for the
/// validator looking at a report that arrived days later (design M2 section 1.13).</para>
/// </summary>
public interface IWeatherSource
{
    /// <summary>
    /// The current METARs and TAFs of these airports. Airports nobody publishes weather for are
    /// simply missing from the answer; an empty list means nothing could be reached.
    /// </summary>
    Task<IReadOnlyList<WeatherReport>> GetCurrentAsync(
        IReadOnlyCollection<string> icaos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The bulletins of one airport inside a window, for a flight that has already happened.
    /// <para><c>null</c> means the question could not be answered — the window is older than
    /// <see cref="HistoryWindow"/>, or the source could not be reached — and it is not the same as
    /// an empty list, which means "asked, and there was nothing". A check turns the first into
    /// <c>Unavailable</c>, never into a failure (Toursystem ADR-011).</para>
    /// </summary>
    Task<IReadOnlyList<WeatherReport>?> GetHistoryAsync(
        string icao,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How far back the history goes: thirty days, which NOAA states itself when asked for more
    /// ("Data is available for up to 30 days", measured 16 September 2026). The report window of a
    /// tour is seven days by default, so a PIREP arrives well inside it.
    /// </summary>
    public static readonly TimeSpan HistoryWindow = TimeSpan.FromDays(30);
}
