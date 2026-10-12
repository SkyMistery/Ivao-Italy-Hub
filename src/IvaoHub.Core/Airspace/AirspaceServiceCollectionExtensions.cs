using IvaoHub.Core.Division;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Core.Airspace;

/// <summary>
/// Wires the outlines of the flight information regions. The provider is named in
/// <see cref="VatSpyFirBoundarySource"/> and nowhere else, which is what lets a module ask
/// <see cref="IFirLocator"/> without knowing who drew the polygons; an architecture test holds it.
/// </summary>
public static class AirspaceServiceCollectionExtensions
{
    /// <summary>
    /// Sundays at 04:10 in the time zone of the division, after the other nights of the core (the reference data at 03:15,
    /// the files at 04:00): the outlines change about as often as borders do.
    /// </summary>
    private const string WeeklyCron = "0 10 4 ? * SUN";

    /// <summary>The weekly trigger, by name: what a test reads to see the zone it was given.</summary>
    public const string TriggerName = $"{FirSyncJob.JobName}-weekly";

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

        services.AddQuartz(quartz => quartz.AddJob<FirSyncJob>(job => job.WithIdentity(FirSyncJob.JobName)));

        // In the division's own zone, like the other nights of the core, rather than on the server's clock (note
        // 2026-10-09-i-job-che-recuperano): the zone is read through the options pipeline, which is complete by the time
        // Quartz reads its options, as the reference data's trigger does.
        services.AddOptions<QuartzOptions>()
            .Configure<IOptions<DivisionOptions>>((options, division) => options.AddTrigger(trigger => trigger
                .ForJob(FirSyncJob.JobName)
                .WithIdentity(TriggerName)
                .WithCronSchedule(WeeklyCron, schedule => schedule.InTimeZone(division.Value.ResolveTimeZone()))));

        return services;
    }
}
