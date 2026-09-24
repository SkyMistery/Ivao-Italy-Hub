using IvaoHub.Core.Weather;

namespace IvaoHub.Modules.FlightOps.Weather;

/// <summary>
/// A METAR or a TAF the tours kept (design M2 §1.13), <c>fo_weather_reports</c>: one row per bulletin, as it was published,
/// and never two for the same airport, kind and moment. The core fetches it (<see cref="IWeatherSource"/>); keeping it and
/// deleting it is the module's (note 2026-09-15-meteo-e-confini-dei-fir §3.1).
/// <para>Not a row anybody edits: no audit, no department. Only the job and the send write it, and the retention job
/// deletes it (<see cref="WeatherRetentionJob"/>).</para>
/// </summary>
public sealed class WeatherBulletin
{
    public long Id { get; set; }

    public string Icao { get; set; } = string.Empty;

    public WeatherReportKind Kind { get; set; }

    /// <summary>When it was observed (a METAR) or issued (a TAF).</summary>
    public DateTime IssuedAt { get; set; }

    public string Raw { get; set; } = string.Empty;

    /// <summary>Who answered — a validator looking at an old decision deserves to know where the weather came from.</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime FetchedAt { get; set; }

    public const int MaxRawLength = 2000;
}
