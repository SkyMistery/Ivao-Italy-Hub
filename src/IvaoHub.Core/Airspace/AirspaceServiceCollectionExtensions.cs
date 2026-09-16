using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace IvaoHub.Core.Airspace;

/// <summary>
/// Wires the outlines of the flight information regions. The provider is named in
/// <see cref="VatSpyFirBoundarySource"/> and nowhere else, which is what lets a module ask
/// <see cref="IFirLocator"/> without knowing who drew the polygons; an architecture test holds it.
/// </summary>
public static class AirspaceServiceCollectionExtensions
{
    /// <summary>Sundays at 04:10: the outlines change about as often as borders do.</summary>
    private const string WeeklyCron = "0 10 4 ? * SUN";

    public static IServiceCollection AddAirspace(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<IFirBoundarySource, VatSpyFirBoundarySource>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "IvaoDivisionHub/1.0 (+https://github.com/SkyMistery/Ivao-Italy-Hub)");
        }).AddStandardResilienceHandler();

        services.AddScoped<IFirLocator, FirLocator>();
        services.AddScoped<FirSyncJob>();

        services.AddQuartz(quartz =>
        {
            quartz.AddJob<FirSyncJob>(job => job.WithIdentity(FirSyncJob.JobName));
            quartz.AddTrigger(trigger => trigger
                .ForJob(FirSyncJob.JobName)
                .WithIdentity($"{FirSyncJob.JobName}-weekly")
                .WithCronSchedule(WeeklyCron));
        });

        return services;
    }
}
