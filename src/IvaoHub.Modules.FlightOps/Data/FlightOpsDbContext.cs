using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Modules.FlightOps.Aircraft;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IvaoHub.Modules.FlightOps.Data;

/// <summary>
/// The context of the tours: every <c>fo_</c> table, with its own history <c>__EFMigrationsHistory_flightops</c>
/// (design M2 §1). No foreign key towards the core: a <c>vid</c>, an <c>icao</c>, a <c>media_id</c> are plain columns.
/// </summary>
public sealed class FlightOpsDbContext(DbContextOptions<FlightOpsDbContext> options, ICurrentUser? currentUser = null)
    : ModuleDbContext(options, currentUser)
{
    public DbSet<AircraftProfile> AircraftProfiles => Set<AircraftProfile>();

    public DbSet<AircraftGroup> AircraftGroups => Set<AircraftGroup>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AircraftProfile>(profile =>
        {
            profile.ToTable("fo_aircraft_profiles");
            profile.HasKey(row => row.Id);
            profile.Property(row => row.IcaoType).HasMaxLength(4).IsRequired();
            profile.Property(row => row.Note).HasMaxLength(AircraftValidation.MaxNoteLength);
            profile.HasRowVersion(row => row.RowVersion);

            // One profile per type: the estimated time would otherwise depend on which row was read first.
            profile.HasIndex(row => row.IcaoType).IsUnique();
        });

        modelBuilder.Entity<AircraftGroup>(group =>
        {
            group.ToTable("fo_aircraft_groups");
            group.HasKey(row => row.Id);
            group.Ignore(row => row.IcaoTypes);
            group.Property(row => row.IcaoTypesJson).HasColumnName("icao_types_json").HasColumnType("json").IsRequired();
            group.HasRowVersion(row => row.RowVersion);
        });
    }
}

/// <summary>Lets <c>dotnet ef</c> build the model of the tours, the same way the hub's is built.</summary>
public sealed class FlightOpsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FlightOpsDbContext>
{
    public FlightOpsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FlightOpsDbContext>();
        options.UseMySql(
            "Server=localhost;Port=3306;Database=ivaohub;User ID=ivaohub;Password=ivaohub",
            new MariaDbServerVersion(HubDbContext.ServerVersion),
            mySql => mySql.MigrationsHistoryTable($"__EFMigrationsHistory_{FlightOpsModule.ModuleKey}"));
        options.UseSnakeCaseNamingConvention();

        return new FlightOpsDbContext(options.Options);
    }
}
