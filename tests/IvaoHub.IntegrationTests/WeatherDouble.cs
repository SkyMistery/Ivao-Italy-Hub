using System.Collections.Concurrent;
using IvaoHub.Core.Weather;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The weather of a test host (T16): no test asks the real sources, which answer with the weather of today and may not answer
/// at all. The factory registers an empty one — every report sent by any test fills its flight's weather at the send, and
/// finds none — and a test that is about the weather gives its host one with bulletins of its own.
/// <para>Current bulletins are answered for the airports asked for; history is the bulletins of that airport inside the
/// window — a TAF up to thirty hours before it — or <c>null</c>, "not available", when the test put none there.</para>
/// </summary>
public sealed class WeatherDouble : IWeatherSource
{
    private readonly ConcurrentBag<WeatherReport> _current = [];
    private readonly ConcurrentBag<WeatherReport> _history = [];

    /// <summary>Every list of airports <see cref="GetCurrentAsync"/> was asked for.</summary>
    public ConcurrentQueue<IReadOnlyCollection<string>> CurrentAsked { get; } = new();

    /// <summary>Every airport <see cref="GetHistoryAsync"/> was asked for.</summary>
    public ConcurrentQueue<string> HistoryAsked { get; } = new();

    /// <summary>History that throws, as a source that does not answer in time does at the send.</summary>
    public bool HistoryFails { get; set; }

    public void AddCurrent(WeatherReport report) => _current.Add(report);

    public void AddHistory(WeatherReport report) => _history.Add(report);

    public Task<IReadOnlyList<WeatherReport>> GetCurrentAsync(IReadOnlyCollection<string> icaos, CancellationToken cancellationToken = default)
    {
        CurrentAsked.Enqueue([.. icaos]);
        return Task.FromResult<IReadOnlyList<WeatherReport>>([.. _current.Where(report => icaos.Contains(report.Icao))]);
    }

    public Task<IReadOnlyList<WeatherReport>?> GetHistoryAsync(
        string icao,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        HistoryAsked.Enqueue(icao);
        if (HistoryFails)
        {
            throw new HttpRequestException($"The weather of {icao} did not answer.");
        }

        // A TAF asked for by date is the one in force then, issued before the window — as the real source answers.
        var found = _history
            .Where(report => report.Icao == icao
                && report.IssuedAt <= toUtc
                && report.IssuedAt >= (report.Kind == WeatherReportKind.Taf ? fromUtc.AddHours(-30) : fromUtc))
            .ToList();
        return Task.FromResult<IReadOnlyList<WeatherReport>?>(found.Count == 0 ? null : found);
    }
}
