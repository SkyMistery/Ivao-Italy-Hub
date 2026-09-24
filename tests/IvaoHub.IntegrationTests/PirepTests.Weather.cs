using System.Net.Http.Json;
using IvaoHub.Core.Weather;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Weather;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The weather kept for the validators (M2, T16; design §1.13): the job of the half hour saves the airports of the open tours
/// without doubles, the send fills in what the flight is missing, the validation page shows it, and a bulletin stays while a
/// report of that day at that airport waits (Carmine's rule). The sources are a <see cref="WeatherDouble"/>.
/// </summary>
public sealed partial class PirepTests
{
    private const string WeatherSourceName = "fo-test";

    private readonly WeatherDouble _weather = new();

    /// <summary>The "done when" of T16 on the job: it asks for the legs' airports, and twice is no double.</summary>
    [Fact]
    public async Task TheWeatherJobKeepsTheAirportsOfTheOpenToursWithoutDoubles()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        await ReadyTourAsync(coordinator, dailyLimit: 5, token);

        var observed = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 20, 0, DateTimeKind.Utc);
        _weather.AddCurrent(Metar(Rome, observed));
        _weather.AddCurrent(new WeatherReport(Rome, WeatherReportKind.Taf, observed.AddHours(-1), $"TAF {Rome} fo-test", WeatherSourceName));
        _weather.AddCurrent(Metar(Milan, observed));

        for (var run = 0; run < 2; run++)
        {
            await using var scope = _host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<WeatherJob>().RunAsync(token);
        }

        Assert.Contains(_weather.CurrentAsked, asked => asked.Contains(Rome) && asked.Contains(Milan));

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            var kept = await database.WeatherBulletins.AsNoTracking()
                .Where(row => (row.Icao == Rome || row.Icao == Milan) && row.Source == WeatherSourceName && row.IssuedAt >= observed.AddHours(-1))
                .ToListAsync(token);

            Assert.Equal(3, kept.Count);
            Assert.Equal(2, kept.Count(row => row.Icao == Rome));
        }
    }

    /// <summary>
    /// The send asks for the history of the flight's airports the job did not keep, and the validation page shows the METARs
    /// inside the flight and the TAF in force, with who published them.
    /// </summary>
    [Fact]
    public async Task TheSendFillsInTheFlightsWeatherAndTheValidatorReadsIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var takeoff = DateTime.UtcNow.AddDays(-1).AddMinutes(-7);
        var flown = _flights.Add(PilotVid, "XAA160", Rome, Milan, takeoff);

        _weather.AddHistory(Metar(Rome, takeoff.AddMinutes(-15)));
        _weather.AddHistory(Metar(Milan, takeoff.AddMinutes(50)));
        _weather.AddHistory(new WeatherReport(Milan, WeatherReportKind.Taf, takeoff.AddHours(-5), $"TAF {Milan} fo-test", WeatherSourceName));

        var sent = await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], flown), token);
        Assert.Contains(Rome, _weather.HistoryAsked);
        Assert.Contains(Milan, _weather.HistoryAsked);

        var page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{Id(sent)}", token), token);
        var weather = page.GetProperty("weather").EnumerateArray().ToDictionary(row => row.GetProperty("icao").GetString()!);

        Assert.Equal("Departure", weather[Rome].GetProperty("role").GetString());
        var metar = Assert.Single(
            weather[Rome].GetProperty("metars").EnumerateArray(),
            row => row.GetProperty("source").GetString() == WeatherSourceName);
        Assert.Equal($"{Rome} fo-test {takeoff.AddMinutes(-15):HHmm}", metar.GetProperty("raw").GetString());

        Assert.Equal("Arrival", weather[Milan].GetProperty("role").GetString());
        Assert.Contains(weather[Milan].GetProperty("metars").EnumerateArray(), row => row.GetProperty("source").GetString() == WeatherSourceName);
        Assert.Contains(weather[Milan].GetProperty("tafs").EnumerateArray(), row => row.GetProperty("raw").GetString() == $"TAF {Milan} fo-test");
    }

    /// <summary>
    /// Carmine's rule (§1.13): past the longest report window a bulletin goes, unless a report still waiting flew that day at
    /// that airport; once the report is out of the queue — withdrawn here — it goes too. A bulletin of a day nobody flew
    /// goes at once.
    /// </summary>
    [Fact]
    public async Task ABulletinStaysWhileAReportOfThatDayAtThatAirportWaits()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        // Twelve days back, on a tour that takes reports that old: no other test of the class flies near then, so no other
        // report holds the day.
        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token, releasedDaysAgo: 15, reportWindowDays: 14);
        var takeoff = DateTime.UtcNow.AddDays(-12);
        var flown = _flights.Add(PilotVid, "XAA170", Rome, Milan, takeoff);
        _weather.AddHistory(Metar(Rome, takeoff.AddMinutes(-10)));

        var sent = await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], flown), token);

        // London is nobody's airport on that report: saved here the way the job would have.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var archive = scope.ServiceProvider.GetRequiredService<WeatherArchive>();
            Assert.Equal(1, await archive.SaveAsync([Metar(London, takeoff.AddMinutes(-10))], DateTime.UtcNow, token));
            Assert.Equal(0, await archive.SaveAsync([Metar(London, takeoff.AddMinutes(-10))], DateTime.UtcNow, token));
        }

        // Seventy days from now every report window is behind (sixty at most).
        var later = DateTime.UtcNow.AddDays(70);
        Assert.True(await DeleteExpiredAsync(later, token) >= 1);
        Assert.True(await KeptAsync(Rome, takeoff, token));
        Assert.False(await KeptAsync(London, takeoff, token));

        await OkAsync(
            await pilot.PostAsJsonAsync(
                $"{PirepEndpoints.Pattern}/{Id(sent)}/withdraw",
                new PirepWithdrawal(sent.GetProperty("rowVersion").GetDateTime()),
                token),
            token);

        await DeleteExpiredAsync(later, token);
        Assert.False(await KeptAsync(Rome, takeoff, token));
    }

    /// <summary>A METAR of the test source, on a whole second as the real sources give them.</summary>
    private static WeatherReport Metar(string icao, DateTime at) =>
        new(icao, WeatherReportKind.Metar, at.AddTicks(-(at.Ticks % TimeSpan.TicksPerSecond)), $"{icao} fo-test {at:HHmm}", WeatherSourceName);

    private async Task<int> DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WeatherArchive>().DeleteExpiredAsync(now, cancellationToken);
    }

    private async Task<bool> KeptAsync(string icao, DateTime takeoff, CancellationToken cancellationToken)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var at = takeoff.AddMinutes(-10);
        return await scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>().WeatherBulletins
            .AnyAsync(row => row.Icao == icao && row.Source == WeatherSourceName && row.IssuedAt >= at.AddSeconds(-1) && row.IssuedAt <= at.AddSeconds(1), cancellationToken);
    }

    /// <summary>The bulletins of the test airports, taken back with the class.</summary>
    private static async Task CleanWeatherAsync(FlightOpsDbContext database, CancellationToken cancellationToken) =>
        await database.WeatherBulletins
            .Where(row => row.Icao == Rome || row.Icao == Milan || row.Icao == London)
            .ExecuteDeleteAsync(cancellationToken);
}
