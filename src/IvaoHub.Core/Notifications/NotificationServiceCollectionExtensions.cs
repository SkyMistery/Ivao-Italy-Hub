using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;

namespace IvaoHub.Core.Notifications;

/// <summary>
/// The notification service of the core and the job that empties its queue. One call, so that a
/// host cannot end up with intents being queued and nothing sending them.
/// </summary>
public static class NotificationServiceCollectionExtensions
{
    /// <summary>Every minute. A queue that waits longer is a queue people stop trusting.</summary>
    private const string EveryMinuteCron = "0 * * * * ?";

    public static IServiceCollection AddHubNotifications(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.Section);

        // Nothing is read here: whether a server is configured is asked when the job runs, so that
        // a host which adds configuration after this method — a test host, a deployment — is not
        // frozen into "no mail" (the lesson of AddIvaoIntegration).
        services.TryAddSingleton<IMailSender, SmtpMailSender>();
        services.TryAddScoped<INotificationService, NotificationService>();
        services.AddScoped<NotificationDispatchJob>();

        // In UTC, said rather than left to the server's clock (note 2026-10-09-i-job-che-recuperano): a minute is a minute
        // in every zone, and a schedule that names no zone is one somebody has to guess.
        services.AddQuartz(quartz => quartz
            .AddJob<NotificationDispatchJob>(job => job.WithIdentity(NotificationDispatchJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(NotificationDispatchJob.JobName)
                .WithIdentity(TriggerName)
                .WithCronSchedule(EveryMinuteCron, schedule => schedule.InTimeZone(TimeZoneInfo.Utc))));

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        return services;
    }

    /// <summary>The trigger of every minute, by name: what a test reads to see the zone it was given.</summary>
    public const string TriggerName = $"{NotificationDispatchJob.JobName}-minute";
}
