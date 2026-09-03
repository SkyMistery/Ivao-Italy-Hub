using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Core.Ivao;

/// <summary>Wires the one client that talks to IVAO, and the job that keeps the snapshot fresh.</summary>
public static class IvaoServiceCollectionExtensions
{
    /// <summary>Set <c>Ivao:UseFixtures=true</c> to read the reference data from files instead.</summary>
    public const string UseFixturesKey = "Ivao:UseFixtures";

    /// <summary>03:15 in the time zone of the division: late enough to be quiet, early enough to be ready.</summary>
    private const string DailyCron = "0 15 3 * * ?";

    public static IServiceCollection AddIvaoIntegration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        DivisionOptions division)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(division);

        var useFixtures = configuration.GetValue<bool>(UseFixturesKey);
        if (useFixtures && !environment.IsDevelopment())
        {
            // A live site serving invented airspace would be worse than a live site with none.
            throw new InvalidOperationException(
                $"{UseFixturesKey} is only allowed in development. Remove it, or give the division an "
                + "OAuth client that may read the IVAO reference endpoints.");
        }

        services.AddScoped<IFirDirectory, FirDirectory>();
        services.AddScoped<RefDataSyncJob>();

        // The token of the application, on its own client so a slow token endpoint cannot exhaust
        // the connections of the data calls.
        services.AddHttpClient<IvaoApiTokenProvider>(ConfigureAuthority).AddStandardResilienceHandler();
        services.AddHttpClient<IvaoApiClient>(ConfigureAuthority).AddStandardResilienceHandler();
        services.AddScoped<FixtureIvaoApiClient>();

        // Which one answers is decided when the client is built, not when it is registered: a test
        // host and a deployment both add configuration sources after registration.
        services.AddScoped<IIvaoApiClient>(provider =>
            provider.GetRequiredService<IConfiguration>().GetValue<bool>(UseFixturesKey)
                ? provider.GetRequiredService<FixtureIvaoApiClient>()
                : provider.GetRequiredService<IvaoApiClient>());

        services.AddQuartz(quartz => quartz
            .AddJob<RefDataSyncJob>(job => job.WithIdentity(RefDataSyncJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(RefDataSyncJob.JobName)
                .WithIdentity($"{RefDataSyncJob.JobName}-daily")
                .WithCronSchedule(DailyCron, schedule => schedule.InTimeZone(TimeZone(division)))));

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        return services;
    }

    private static void ConfigureAuthority(IServiceProvider provider, HttpClient client)
    {
        var ivao = provider.GetRequiredService<IOptions<IvaoOAuthOptions>>().Value;
        client.BaseAddress = new Uri(ivao.Authority);
        client.Timeout = TimeSpan.FromSeconds(30);
    }

    private static TimeZoneInfo TimeZone(DivisionOptions division)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(division.Timezone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // The options validator already refuses an unknown time zone at start up; this is only
            // here so that a schedule can never be the thing that stops the site.
            return TimeZoneInfo.Utc;
        }
    }
}
