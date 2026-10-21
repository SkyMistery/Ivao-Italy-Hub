using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Weather;

/// <summary>
/// Wires the weather. The two outside providers are named <b>here and nowhere else</b> in the hub,
/// which is what lets a module ask for the weather without knowing that NOAA exists (plan section
/// 4.2); an architecture test holds that line.
/// </summary>
public static class WeatherServiceCollectionExtensions
{
    /// <summary>What the hub calls itself when it knocks on a public API.</summary>
    public const string UserAgent = "IvaoDivisionHub/1.0 (+https://github.com/SkyMistery/Ivao-Italy-Hub)";

    public static IServiceCollection AddWeather(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Their policy asks for a user agent of our own and for at most a hundred calls a minute;
        // the standard resilience handler carries the retries and the circuit breaker, as it does
        // for IVAO. Its total timeout of thirty seconds is the only one: the handler leaves the client's own timeout
        // infinite, so the sixty and fifteen seconds once written here were waits nobody got (measured on 21 October 2026,
        // issue #325 item 1). What it throws when it gives up, the clients read as "could not be reached" (OutboundFailure).
        services.AddHttpClient<NoaaWeatherClient>(client =>
        {
            client.BaseAddress = new Uri("https://aviationweather.gov");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }).AddStandardResilienceHandler();

        services.AddHttpClient<VatsimMetarClient>(client =>
        {
            client.BaseAddress = new Uri("https://metar.vatsim.net");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }).AddStandardResilienceHandler();

        services.AddScoped<IWeatherSource, WeatherSource>();

        return services;
    }
}
