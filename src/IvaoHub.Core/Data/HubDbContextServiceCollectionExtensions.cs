using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Data;

/// <summary>
/// The one place that builds a context on MariaDB. Every module context is registered through the
/// sibling method, so nothing that is added here (the interceptor, in F4) can be forgotten.
/// </summary>
public static class HubDbContextServiceCollectionExtensions
{
    private const string ConnectionStringName = "Default";

    public static IServiceCollection AddHubDbContext(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDbContext<HubDbContext>((provider, options) =>
            Configure(options, ResolveConnectionString(provider), "__EFMigrationsHistory"));
        return services;
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

        services.AddDbContext<TContext>((provider, options) => Configure(
            options,
            ResolveConnectionString(provider),
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

    private static void Configure(DbContextOptionsBuilder options, string connectionString, string historyTable)
    {
        options.UseMySql(
            connectionString,
            new MariaDbServerVersion(HubDbContext.ServerVersion),
            mySql => mySql.MigrationsHistoryTable(historyTable));
        options.UseSnakeCaseNamingConvention();
    }
}
