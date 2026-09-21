using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Modules.FlightOps.Aircraft;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Tours;
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

    public DbSet<Tour> Tours => Set<Tour>();

    public DbSet<Leg> Legs => Set<Leg>();

    /// <summary>The enums of the tours are stored as text, like the core's: readable without the code next to them.</summary>
    protected override void ConfigureModuleConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<TourKind>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<TourProgression>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<HubRotationOrder>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<OpenGoal>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<LegKind>().HaveConversion<string>().HaveMaxLength(16);
    }

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

        modelBuilder.Entity<Tour>(tour =>
        {
            tour.ToTable("fo_tours");
            tour.HasKey(row => row.Id);
            tour.Property(row => row.Slug).HasMaxLength(TourValidation.MaxSlugLength);
            tour.Property(row => row.OpenGoalJson).HasColumnName("open_goal_json").HasColumnType("json");
            tour.Property(row => row.BriefingJson).HasColumnName("briefing_json").HasColumnType("json").IsRequired();
            tour.Ignore(row => row.AllowedAircraft);
            tour.Property(row => row.AllowedAircraftJson).HasColumnName("allowed_aircraft_json").HasColumnType("json").IsRequired();
            tour.Property(row => row.ReferenceAircraftIcao).HasMaxLength(4);
            tour.HasRowVersion(row => row.RowVersion);

            // Unique among tours; a template has none, and MariaDB lets several rows hold no address.
            tour.HasIndex(row => row.Slug).IsUnique();
            tour.HasIndex(row => new { row.IsTemplate, row.ReleaseAt });
        });

        modelBuilder.Entity<Leg>(leg =>
        {
            leg.ToTable("fo_legs");
            leg.HasKey(row => row.Id);
            leg.Ignore(row => row.Aircraft);
            leg.Ignore(row => row.Departure);
            leg.Ignore(row => row.Arrival);
            leg.Property(row => row.DepartureIcao).HasMaxLength(4).IsRequired();
            leg.Property(row => row.ArrivalIcao).HasMaxLength(4).IsRequired();
            leg.Property(row => row.DistanceNm).HasPrecision(7, 1);
            leg.Property(row => row.RealCallsign).HasMaxLength(LegValidation.MaxCallsignLength);
            leg.Property(row => row.FlightNumber).HasMaxLength(LegValidation.MaxCallsignLength);
            leg.Property(row => row.AircraftJson).HasColumnName("aircraft_json").HasColumnType("json").IsRequired();
            leg.Property(row => row.RetiredReason).HasMaxLength(LegValidation.MaxReasonLength);
            leg.Property(row => row.ChangeReason).HasMaxLength(LegValidation.MaxReasonLength);
            leg.HasRowVersion(row => row.RowVersion);

            // Inside one context a key is allowed, and a tour deleted takes its legs with it (design M2 §1.2.2).
            leg.HasOne<Tour>().WithMany().HasForeignKey(row => row.TourId).OnDelete(DeleteBehavior.Cascade);

            // ⚠️ Not unique, although a number is unique in its tour: MariaDB checks a unique key row by row, so shifting
            // the numbers after an insert would collide half way. The server renumbers the whole tour, and that keeps it.
            leg.HasIndex(row => new { row.TourId, row.Number });
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
