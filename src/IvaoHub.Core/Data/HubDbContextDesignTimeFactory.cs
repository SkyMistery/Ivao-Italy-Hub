using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IvaoHub.Core.Data;

/// <summary>
/// Lets <c>dotnet ef</c> build the model without starting the host, which would refuse to run
/// without the configuration of a real division. The connection string is only used to pick the
/// provider: no migration command touches a database unless it is asked to.
/// </summary>
public sealed class HubDbContextDesignTimeFactory : IDesignTimeDbContextFactory<HubDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost;Port=3306;Database=ivaohub;User ID=ivaohub;Password=ivaohub";

    public HubDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<HubDbContext>();
        options.UseMySql(connectionString, new MariaDbServerVersion(HubDbContext.ServerVersion));
        options.UseSnakeCaseNamingConvention();

        return new HubDbContext(options.Options);
    }
}
