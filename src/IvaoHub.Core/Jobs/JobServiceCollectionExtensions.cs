using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;

namespace IvaoHub.Core.Jobs;

/// <summary>
/// The scheduled jobs of every module and of the core, run once per occurrence whichever process is alive (note
/// 2026-10-09-i-job-che-recuperano): the guard in front of every run, the check that makes up the lost ones, and what the
/// installation says about them. No job registers anything for it: a job that Quartz knows is covered.
/// </summary>
public static class JobServiceCollectionExtensions
{
    public static IServiceCollection AddHubJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Read when a run or a check asks, never here: a test host and a deployment add their configuration after this.
        services.AddOptions<JobOptions>().BindConfiguration(JobOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JobOptions>, JobOptionsValidator>();

        // One instance of each: the locks of the process on its one connection, the guard of every trigger, and the launcher
        // the check and the scheduled task's address call.
        services.TryAddSingleton<JobLocks>();
        services.TryAddSingleton<ScheduledJobs>();
        services.AddQuartz(quartz => quartz.AddTriggerListener(
            provider => provider.GetRequiredService<ScheduledJobs>(),
            EverythingMatcher<TriggerKey>.AllTriggers()));

        // The scheduler itself is started by the notifications and the reference data, which register Quartz's own service.
        services.AddHostedService<JobCatchUp>();

        return services;
    }
}
