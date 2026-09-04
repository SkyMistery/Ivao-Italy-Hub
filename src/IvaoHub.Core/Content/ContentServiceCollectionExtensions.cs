using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IvaoHub.Core.Content;

/// <summary>
/// What editorial content needs in the container: the block registry, the providers that answer for
/// data blocks, publication and the seeder of the system templates.
/// <para>The registry is composed from every <see cref="IBlockDescriptor"/> registered, which is how
/// a module adds a block in F8 without the core learning its name.</para>
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

        services.TryAddScoped<ContentPublishService>();
        services.TryAddScoped<ContentTemplateSeeder>();

        return services;
    }
}
