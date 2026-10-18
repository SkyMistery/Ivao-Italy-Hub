using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Services;

/// <summary>The timings of the requests (note 2026-10-18-i-tempi-delle-richieste).</summary>
public static class RequestTimingsServiceCollectionExtensions
{
    /// <summary>
    /// The writer of <c>requests-YYYYMMDD.txt</c>, and the two measures it reads: the database interceptor on every context and
    /// the handler on every outbound HTTP client. Those two run whatever the setting: they only add to counters, and the
    /// line of the start in <c>starts.txt</c> reads them too. Whether lines are written is decided when the writer starts,
    /// from <c>Diagnostics:Requests:Enabled</c> or, without it, from <paramref name="hubProcess"/>.
    /// </summary>
    public static IServiceCollection AddRequestTimings(this IServiceCollection services, IConfiguration configuration, bool hubProcess)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RequestTimingsOptions>()
            .Bind(configuration.GetSection(RequestTimingsOptions.SectionName))
            .PostConfigure(options => options.Enabled ??= hubProcess);

        services.AddSingleton<RequestTimingsLog>();
        services.AddHostedService(provider => provider.GetRequiredService<RequestTimingsLog>());

        services.AddSingleton<DatabaseTimingInterceptor>();
        services.AddSingleton<IInterceptor>(provider => provider.GetRequiredService<DatabaseTimingInterceptor>());

        services.AddTransient<OutboundTimingHandler>();
        services.ConfigureHttpClientDefaults(http => http.AddHttpMessageHandler<OutboundTimingHandler>());
        return services;
    }
}
