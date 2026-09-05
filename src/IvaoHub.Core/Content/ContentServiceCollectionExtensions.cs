using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IvaoHub.Core.Content;

/// <summary>
/// What editorial content needs in the container: the block registry, the providers that answer for
/// data blocks, publication and the seeder of the system templates.
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
        services.TryAddScoped<DataBlockProviders>();

        // Where the uploaded files live. A singleton because it holds one path and no state; the
        // limits it is built with are read when it is built, never when it is registered.
        services.AddOptions<MediaOptions>()
            .BindConfiguration(MediaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<MediaStorage>();

        services.TryAddScoped<ContentPublishService>();
        services.TryAddScoped<ContentTemplateSeeder>();

        return services;
    }
}
