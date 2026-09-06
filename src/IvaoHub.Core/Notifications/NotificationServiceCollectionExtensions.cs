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

        services.AddQuartz(quartz => quartz
            .AddJob<NotificationDispatchJob>(job => job.WithIdentity(NotificationDispatchJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(NotificationDispatchJob.JobName)
                .WithIdentity($"{NotificationDispatchJob.JobName}-minute")
                .WithCronSchedule(EveryMinuteCron)));

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        return services;
    }
}
