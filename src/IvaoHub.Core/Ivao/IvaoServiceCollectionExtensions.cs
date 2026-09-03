using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Everything is decided when the object is built, never when it is registered: a test host and
    /// a deployment both add configuration sources after this method has run, so anything read here
    /// would be read too early. That is why this takes no configuration, no environment and no
    /// division: it used to take all three, and each one was a way of freezing a value.
    /// </summary>
    public static IServiceCollection AddIvaoIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IFirDirectory, FirDirectory>();
        services.AddScoped<RefDataSyncJob>();

        // The token of the application, on its own client so a slow token endpoint cannot exhaust
        // the connections of the data calls.
        services.AddHttpClient<IvaoApiTokenProvider>(ConfigureAuthority).AddStandardResilienceHandler();
        services.AddHttpClient<IvaoApiClient>(ConfigureAuthority).AddStandardResilienceHandler();
        services.AddScoped<FixtureIvaoApiClient>();

        // Which one answers is decided when the client is built, not when it is registered.
        // FixtureIvaoApiClient refuses to exist outside development in its own constructor, which
        // is the check that matters: it is the object that would serve the invented airspace.
        services.AddScoped<IIvaoApiClient>(provider =>
            provider.GetRequiredService<IConfiguration>().GetValue<bool>(UseFixturesKey)
                ? provider.GetRequiredService<FixtureIvaoApiClient>()
                : provider.GetRequiredService<IvaoApiClient>());

        services.AddQuartz(quartz => quartz.AddJob<RefDataSyncJob>(job => job.WithIdentity(RefDataSyncJob.JobName)));

        // The trigger needs the time zone of the division, so it is added through the options
        // pipeline: by the time Quartz reads QuartzOptions the configuration is complete, which it
        // is not while AddIvaoIntegration is running.
        services.AddOptions<QuartzOptions>()
            .Configure<IOptions<DivisionOptions>>((options, division) => options.AddTrigger(trigger => trigger
                .ForJob(RefDataSyncJob.JobName)
                .WithIdentity($"{RefDataSyncJob.JobName}-daily")
                .WithCronSchedule(DailyCron, schedule => schedule.InTimeZone(TimeZone(division.Value)))));

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
