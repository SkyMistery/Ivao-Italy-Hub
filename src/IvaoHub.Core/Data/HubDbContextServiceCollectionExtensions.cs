using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Data;

/// <summary>
/// The one place that builds a context on MariaDB. Every module context is registered through the
/// sibling method, so the save changes interceptor cannot be forgotten by a module: audit, write
/// guard and projections are not something each context opts into.
/// </summary>
public static class HubDbContextServiceCollectionExtensions
{
    private const string ConnectionStringName = "Default";

    public static IServiceCollection AddHubDbContext(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHubDomainServices();
        services.AddDbContext<HubDbContext>((provider, options) =>
            Configure(options, provider, "__EFMigrationsHistory"));
        return services;
    }

    /// <summary>
    /// What the interceptor needs in order to do its job: the writer of the projections, and the
    /// languages of the division an entity has to project itself into.
    /// </summary>
    private static void AddHubDomainServices(this IServiceCollection services)
    {
        // The interceptor forgets a cached security stamp when a grant moves; the reader of that
        // cache lives elsewhere and cannot be injected here without asking the container to build a
        // context in order to build a context.
        services.AddMemoryCache();

        services.TryAddSingleton<BlockDocumentWalker>(provider =>
            new BlockDocumentWalker(provider.GetRequiredService<IOptions<DivisionOptions>>().Value.Locales));

        services.TryAddSingleton(provider =>
        {
            var division = provider.GetRequiredService<IOptions<DivisionOptions>>().Value;
            return new ProjectionContext(
                division.Locales,
                division.DefaultLocale,
                provider.GetRequiredService<BlockDocumentWalker>());
        });

        services.TryAddScoped<ProjectionWriter>();
        services.TryAddScoped<HubSaveChangesInterceptor>();
    }

    /// <summary>
    /// A module context: same provider, same conventions, its own migration history table, and no
    /// foreign key towards another context (plan section 16.12).
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string moduleKey)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);

        services.AddHubDomainServices();
        services.AddDbContext<TContext>((provider, options) => Configure(
            options,
            provider,
            $"__EFMigrationsHistory_{moduleKey}"));
        return services;
    }

    /// <summary>
    /// Read when the context is built, not when the host is being configured: a test host and a
    /// deployment both add configuration sources after that point.
    /// </summary>
    private static string ResolveConnectionString(IServiceProvider provider)
    {
        var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The connection string 'ConnectionStrings:Default' is not configured. "
                + "In development it is in appsettings.Development.json; in production it belongs to a file "
                + "under secrets/ or to the ConnectionStrings__Default environment variable.");
        }

        return connectionString;
    }

    private static void Configure(DbContextOptionsBuilder options, IServiceProvider provider, string historyTable)
    {
        options.UseMySql(
            ResolveConnectionString(provider),
            new MariaDbServerVersion(HubDbContext.ServerVersion),
            mySql => mySql.MigrationsHistoryTable(historyTable));
        options.UseSnakeCaseNamingConvention();
        options.AddInterceptors(provider.GetRequiredService<HubSaveChangesInterceptor>());

        // Anything else the host registered as an IInterceptor: EF Core does not pick those up on
        // its own once interceptors are added by hand here. It is how a diagnostic — a command
        // logger, a counter in a test — is attached without a second way of building a context.
        options.AddInterceptors(provider.GetServices<IInterceptor>());
    }
}
