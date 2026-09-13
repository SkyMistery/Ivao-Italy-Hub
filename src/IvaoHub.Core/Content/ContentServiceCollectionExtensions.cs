using IvaoHub.Core.Division;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Core.Content;

/// <summary>
/// What editorial content needs in the container: the block registry, the providers that answer for
/// data blocks, publication and the seeder of the system templates and pages.
/// <para>The registry is composed from every <see cref="IBlockDescriptor"/> registered, which is how
/// a module adds a block without the core ever learning its name.</para>
/// </summary>
public static class ContentServiceCollectionExtensions
{
    public static IServiceCollection AddHubContent(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var descriptor in CoreBlocks.All)
        {
            services.AddSingleton(descriptor);
        }

        services.TryAddSingleton<BlockRegistry>();

        // Scoped, because a provider reads the database as the caller: the visibility filter is
        // what decides which rows a block shows, and it needs the request's own current user.
        services.AddScoped<IDataBlockProvider, LinkListProvider>();
        services.AddScoped<IDataBlockProvider, StatsProvider>();
        services.AddScoped<IDataBlockProvider, NetworkStatsProvider>();
        services.AddScoped<IDataBlockProvider, CalendarBlockProvider>();
        services.AddScoped<IDataBlockProvider, NewsListProvider>();
        services.AddScoped<IDataBlockProvider, DocumentListProvider>();
        services.AddScoped<IDataBlockProvider, StaffListProvider>();
        services.TryAddScoped<DataBlockProviders>();

        // Where the uploaded files live. A singleton because it holds one path and no state; the
        // limits it is built with are read when it is built, never when it is registered.
        services.AddOptions<MediaOptions>()
            .BindConfiguration(MediaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<MediaStorage>();

        services.TryAddScoped<ContentPublishService>();
        services.TryAddScoped<ContentAddresses>();
        services.TryAddScoped<ContentSeeder>();

        // The review reminder of an operational document (G14), once a day at night in the
        // division's own zone — the same hour and the same reason as the reference data.
        services.AddScoped<DocumentReviewJob>();
        services.AddQuartz(quartz => quartz.AddJob<DocumentReviewJob>(job => job.WithIdentity(DocumentReviewJob.JobName)));
        services.AddOptions<QuartzOptions>()
            .Configure<IOptions<DivisionOptions>>((options, division) => options.AddTrigger(trigger => trigger
                .ForJob(DocumentReviewJob.JobName)
                .WithIdentity($"{DocumentReviewJob.JobName}-daily")
                .WithCronSchedule(DailyCron, schedule => schedule.InTimeZone(division.Value.ResolveTimeZone()))));

        return services;
    }

    /// <summary>03:30 in the time zone of the division: after the snapshot, before anybody is up.</summary>
    private const string DailyCron = "0 30 3 * * ?";
}
