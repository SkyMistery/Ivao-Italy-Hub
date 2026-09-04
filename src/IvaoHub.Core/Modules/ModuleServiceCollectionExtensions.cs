using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IvaoHub.Core.Modules;

/// <summary>
/// How the host hands the core its modules. One call, with the explicit list of
/// <c>IvaoHub.Web/Modules.cs</c>: from there on the core composes, and no screen, endpoint or
/// registry ever names a module (design M0 sections 6.1 and 6.5).
/// </summary>
public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// Registers the modules and everything they contribute: their permissions into the one
    /// catalogue, their blocks into the one block registry, their widgets into the one widget
    /// registry, and their own services through <c>ConfigureServices</c>.
    /// <para>Contributions are only taken from the modules this division actually runs. An optional
    /// module switched off in <c>division.modules</c> is compiled in and silent: its blocks are not
    /// registered, so a page naming one is refused exactly as it would be on an installation that
    /// never had the module at all.</para>
    /// </summary>
    /// <param name="services">The container being built.</param>
    /// <param name="modules">The explicit list; the order is the order menus come out in.</param>
    /// <param name="configuration">
    /// The configuration of the application, handed to each module. Not the division file: the two
    /// are loaded separately on purpose, so that a key of one can never shadow a key of the other.
    /// </param>
    /// <param name="division">
    /// The division, already bound. Which modules are on has to be known while the container is
    /// still being built — a service cannot be registered later — which is why this is a value and
    /// not an <c>IOptions</c>.
    /// </param>
    public static IServiceCollection AddHubModules(
        this IServiceCollection services,
        IReadOnlyList<IModule> modules,
        IConfiguration configuration,
        DivisionOptions division)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(division);

        // The maintenance flags are cached in it, and so is the security stamp.
        services.AddMemoryCache();

        foreach (var module in modules)
        {
            services.AddSingleton(module);
        }

        services.TryAddSingleton<ModuleRegistry>();

        var enabled = Enabled(modules, division);

        foreach (var module in enabled)
        {
            foreach (var block in module.Blocks)
            {
                services.AddSingleton<IBlockDescriptor>(block);
            }

            foreach (var widget in module.Widgets)
            {
                services.AddSingleton(widget);
            }

            module.ConfigureServices(services, configuration);
        }

        foreach (var widget in CoreWidgets.All)
        {
            services.AddSingleton(widget);
        }

        services.TryAddSingleton<WidgetRegistry>();

        // The catalogue that the policy provider, the calculator of effective permissions and the
        // validator of a grant all read. Composed here because this is the one place holding both
        // halves of it.
        services.TryAddSingleton(new PermissionCatalog(
            [.. CorePermissions.All, .. enabled.SelectMany(module => module.Permissions)]));

        return services;
    }

    /// <summary>Maps the endpoints of every enabled module, each under <c>/api/{Key}</c>.</summary>
    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        foreach (var module in app.ServiceProvider.GetRequiredService<ModuleRegistry>().Enabled)
        {
            module.MapEndpoints(app);
        }

        return app;
    }

    /// <summary>
    /// The same rule <see cref="ModuleRegistry"/> applies, used here while the container is still
    /// being built and no registry can exist yet: an optional module is off only when the division
    /// names it with false. The two readings are kept honest by
    /// <c>ModuleRegistryComposesNavAndExclusions</c>, which asserts on the registry the running
    /// application actually built.
    /// </summary>
    private static IReadOnlyList<IModule> Enabled(IReadOnlyList<IModule> modules, DivisionOptions division) =>
    [
        .. modules.Where(module => !module.IsOptional
            || !division.Modules.TryGetValue(module.Key, out var enabled)
            || enabled),
    ];
}
