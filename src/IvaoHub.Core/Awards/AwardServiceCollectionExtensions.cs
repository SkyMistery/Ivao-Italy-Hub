using IvaoHub.Core.Division;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Core.Awards;

/// <summary>
/// What the awards need in the container besides their endpoints: the mail to whoever assigns them (M4, E10d, note
/// 2026-09-30-la-mail-a-chi-assegna-gli-award), and when it goes.
/// </summary>
public static class AwardServiceCollectionExtensions
{
    public static IServiceCollection AddHubAwards(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Once a day in the division's own time zone, like the review reminder of a document: the hour is the morning of the
        // people who read it, not the server's.
        services.AddScoped<AwardQueueMailJob>();
        services.AddQuartz(quartz => quartz.AddJob<AwardQueueMailJob>(job => job.WithIdentity(AwardQueueMailJob.JobName)));
        services.AddOptions<QuartzOptions>()
            .Configure<IOptions<DivisionOptions>>((options, division) => options.AddTrigger(trigger => trigger
                .ForJob(AwardQueueMailJob.JobName)
                .WithIdentity($"{AwardQueueMailJob.JobName}-daily")
                .WithCronSchedule(AwardQueueMailJob.Cron, schedule => schedule.InTimeZone(division.Value.ResolveTimeZone()))));

        return services;
    }
}
