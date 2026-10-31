using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
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
        services.AddScoped<IRunwayDirectory, RunwayDirectory>();
        services.AddScoped<IAircraftTypeDirectory, AircraftTypeDirectory>();
        services.AddScoped<IAirportDirectory, AirportDirectory>();

        // The positions of the division a rating is trained on, which a module asks for a rating (M3, A2).
        services.AddScoped<IAtcPositionDirectory, AtcPositionDirectory>();

        // Who has booked a position of the network in a window, which a module asks without knowing how IVAO lists them (M4, E15a).
        services.AddScoped<IAtcBookingSource, AtcBookingSource>();

        // Who connected to the network in a window, which a module asks without naming whose network it is (M4, E10t).
        services.AddScoped<INetworkSessionSource, NetworkSessionSource>();

        // IVAO's ratings and their rules, which a module asks rather than writes (M3, A1), with the division's preferred ratings
        // (M4, E10c). Data, so one instance serves all, built once division.json has been read — and checked: the start refuses
        // a kind of position or a rating the core does not know, with a message that names the file and the key.
        services.AddSingleton(provider => IvaoRatings.WithPreferred(
            provider.GetRequiredService<IOptions<DivisionOptions>>().Value.PreferredAtcRatings));
        services.AddSingleton<IValidateOptions<DivisionOptions>, PreferredAtcRatingsValidator>();
        services.AddScoped<RefDataSyncJob>();

        // The token of the application, on its own client so a slow token endpoint cannot exhaust
        // the connections of the data calls.
        services.AddHttpClient<IvaoApiTokenProvider>(ConfigureAuthority).AddStandardResilienceHandler();
        services.AddHttpClient<IvaoApiClient>(ConfigureAuthority)
            .AddStandardResilienceHandler(WaitLongerThanIvao)
            .SelectPipelineBy(_ => request => CircuitOf(request.RequestUri));

        // The narrowed picture of the network on disk, for the process that comes next (issue #397).
        services.AddSingleton(provider => new IvaoNetworkPictureStore(
            provider.GetRequiredService<HubPaths>().Cache,
            TimeProvider.System,
            provider.GetRequiredService<ILogger<IvaoNetworkPictureStore>>()));
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
                .WithCronSchedule(DailyCron, schedule => schedule.InTimeZone(division.Value.ResolveTimeZone()))));

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        return services;
    }

    /// <summary>
    /// The circuit a call of the client goes through: one per family, each with its own breaker and the same options. With one
    /// breaker for all, a burst of failing tracker searches (the checks of the event reports make many) opened it for the
    /// sign-in's <c>/v2/users/me</c> too, and nobody could sign in for the break (issue #325, item 6).
    /// </summary>
    public static string CircuitOf(Uri? request)
    {
        var path = request is null ? string.Empty : request.IsAbsoluteUri ? request.AbsolutePath : request.OriginalString;

        return path.StartsWith("/v2/users/", StringComparison.OrdinalIgnoreCase) ? "sign-in"
            : path.StartsWith("/v2/tracker/whazzup", StringComparison.OrdinalIgnoreCase) ? "network"
            : path.StartsWith("/v2/tracker/", StringComparison.OrdinalIgnoreCase) ? "tracker"
            : path.StartsWith("/v2/atc/bookings", StringComparison.OrdinalIgnoreCase) ? "bookings"
            : "reference";
    }

    private static void ConfigureAuthority(IServiceProvider provider, HttpClient client)
    {
        var ivao = provider.GetRequiredService<IOptions<IvaoOAuthOptions>>().Value;
        client.BaseAddress = new Uri(ivao.Authority);

        // No timeout of the client's own: the standard resilience handler leaves it infinite, and its total of thirty
        // seconds is the limit (issue #325, item 1).
    }

    /// <summary>
    /// An attempt waits longer than IVAO's own gateway, which gives up at fifteen seconds: what comes back is then IVAO's
    /// answer — the page, or its 504 — and never a cut of the hub's own. The standard ten seconds are shorter than the page
    /// of the tracker holding the last session of an airport, which takes IVAO about ten and a half (measured on 30
    /// September 2026, M4 E10a): every attempt was cut and retried, and the search failed at thirty seconds after making
    /// IVAO do the same slow work three times. The total stays the standard thirty seconds, so a call that never answers
    /// costs no more than it did.
    /// </summary>
    private static void WaitLongerThanIvao(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);

        // The handler's own rule: the circuit breaker samples at least two attempts.
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(40);
    }
}
