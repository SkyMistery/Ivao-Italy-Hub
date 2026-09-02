using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Data;

/// <summary>
/// The one place that builds a context on MariaDB. Every module context is registered through the
/// sibling method, so nothing that is added here (the interceptor, in F4) can be forgotten.
/// </summary>
public static class HubDbContextServiceCollectionExtensions
{
    public static IServiceCollection AddHubDbContext(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDbContext<HubDbContext>(options => ConfigureHub(options, connectionString));
        return services;
    }

    /// <summary>
    /// A module context: same provider, same conventions, its own migration history table, and no
    /// foreign key towards another context (plan section 16.12).
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string moduleKey)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);

        services.AddDbContext<TContext>(options => Configure(
            options,
            connectionString,
            $"__EFMigrationsHistory_{moduleKey}"));
        return services;
    }

    private static void ConfigureHub(DbContextOptionsBuilder options, string connectionString) =>
        Configure(options, connectionString, "__EFMigrationsHistory");

    private static void Configure(DbContextOptionsBuilder options, string connectionString, string historyTable)
    {
        options.UseMySql(
            connectionString,
            new MariaDbServerVersion(HubDbContext.ServerVersion),
            mySql => mySql.MigrationsHistoryTable(historyTable));
        options.UseSnakeCaseNamingConvention();
    }
}
