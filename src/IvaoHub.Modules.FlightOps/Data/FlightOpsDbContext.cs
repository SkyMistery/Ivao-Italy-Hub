using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Modules.FlightOps.Aircraft;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Shape;
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

    public DbSet<TourHub> Hubs => Set<TourHub>();

    public DbSet<Rotation> Rotations => Set<Rotation>();

    public DbSet<CallsignRule> CallsignRules => Set<CallsignRule>();

    public DbSet<TourConstraint> TourConstraints => Set<TourConstraint>();

    public DbSet<TourRule> Rules => Set<TourRule>();

    public DbSet<TourError> Errors => Set<TourError>();

    public DbSet<TourRuleError> RuleErrors => Set<TourRuleError>();

    /// <summary>The enums of the tours are stored as text, like the core's: readable without the code next to them.</summary>
    protected override void ConfigureModuleConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<TourKind>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<TourProgression>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<HubRotationOrder>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<OpenGoal>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<LegKind>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<CallsignMode>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<CallsignMatch>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<TourConstraintKind>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ErrorCategory>().HaveConversion<string>().HaveMaxLength(16);
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

            // A subtour's parent is a tour of the same table: a container with subtours is not deleted (T7b).
            tour.Ignore(row => row.IsSubtour);
            tour.HasOne<Tour>().WithMany().HasForeignKey(row => row.ParentTourId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Leg>(leg =>
        {
            leg.ToTable("fo_legs");
            leg.HasKey(row => row.Id);
            leg.Ignore(row => row.Aircraft);
            leg.Ignore(row => row.Callsigns);
            leg.Ignore(row => row.FlightNumbers);
            leg.Ignore(row => row.Departure);
            leg.Ignore(row => row.Arrival);
            leg.Property(row => row.DepartureIcao).HasMaxLength(4).IsRequired();
            leg.Property(row => row.ArrivalIcao).HasMaxLength(4).IsRequired();
            leg.Property(row => row.DistanceNm).HasPrecision(7, 1);
            leg.Property(row => row.RealCallsign).HasMaxLength(LegValidation.MaxCallsignLength);
            leg.Property(row => row.FlightNumber).HasMaxLength(LegValidation.MaxCallsignLength);
            leg.Property(row => row.AircraftJson).HasColumnName("aircraft_json").HasColumnType("json").IsRequired();
            leg.Property(row => row.CallsignsJson).HasColumnName("callsigns_json").HasColumnType("json").IsRequired();
            leg.Property(row => row.FlightNumbersJson).HasColumnName("flight_numbers_json").HasColumnType("json").IsRequired();
            leg.Property(row => row.RetiredReason).HasMaxLength(LegValidation.MaxReasonLength);
            leg.Property(row => row.ChangeReason).HasMaxLength(LegValidation.MaxReasonLength);
            leg.HasRowVersion(row => row.RowVersion);

            // Inside one context a key is allowed, and a tour deleted takes its legs with it (design M2 §1.2.2).
            leg.HasOne<Tour>().WithMany().HasForeignKey(row => row.TourId).OnDelete(DeleteBehavior.Cascade);

            // ⚠️ Not unique, although a number is unique in its tour: MariaDB checks a unique key row by row, so shifting
            // the numbers after an insert would collide half way. The server renumbers the whole tour, and that keeps it.
            leg.HasIndex(row => new { row.TourId, row.Number });

            // A rotation with legs is not deleted (T7b); a tour deleted takes both, and the legs forget the rotation.
            leg.HasOne<Rotation>().WithMany().HasForeignKey(row => row.RotationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TourHub>(hub =>
        {
            hub.ToTable("fo_hubs");
            hub.HasKey(row => row.Id);
            hub.Property(row => row.Icao).HasMaxLength(4).IsRequired();
            hub.HasRowVersion(row => row.RowVersion);
            hub.HasOne<Tour>().WithMany().HasForeignKey(row => row.TourId).OnDelete(DeleteBehavior.Cascade);

            // An airport is a hub of a tour once; the server says so before the index does.
            hub.HasIndex(row => new { row.TourId, row.Icao }).IsUnique();
        });

        modelBuilder.Entity<Rotation>(rotation =>
        {
            rotation.ToTable("fo_rotations");
            rotation.HasKey(row => row.Id);
            rotation.HasRowVersion(row => row.RowVersion);
            rotation.HasOne<Tour>().WithMany().HasForeignKey(row => row.TourId).OnDelete(DeleteBehavior.Cascade);

            // A hub with rotations is not deleted (T7b); the cascade is for the tour's own deletion.
            rotation.HasOne<TourHub>().WithMany().HasForeignKey(row => row.HubId).OnDelete(DeleteBehavior.Cascade);
            rotation.HasIndex(row => new { row.TourId, row.HubId, row.Sort });
        });

        modelBuilder.Entity<CallsignRule>(rule =>
        {
            rule.ToTable("fo_callsign_rules");
            rule.HasKey(row => row.Id);
            rule.Ignore(row => row.OnTemplate);
            rule.Property(row => row.Value).HasMaxLength(LegValidation.MaxCallsignLength).IsRequired();
            rule.HasRowVersion(row => row.RowVersion);
            rule.HasOne<Tour>().WithMany().HasForeignKey(row => row.TourId).OnDelete(DeleteBehavior.Cascade);
            rule.HasOne<Leg>().WithMany().HasForeignKey(row => row.LegId).OnDelete(DeleteBehavior.Cascade);
            rule.HasIndex(row => new { row.TourId, row.LegId });
        });

        modelBuilder.Entity<TourConstraint>(constraint =>
        {
            constraint.ToTable("fo_tour_constraints");
            constraint.HasKey(row => row.Id);
            constraint.Ignore(row => row.OnTemplate);
            constraint.Property(row => row.ParametersJson).HasColumnName("parameters_json").HasColumnType("json").IsRequired();
            constraint.HasRowVersion(row => row.RowVersion);
            constraint.HasOne<Tour>().WithMany().HasForeignKey(row => row.TourId).OnDelete(DeleteBehavior.Cascade);

            // Once per kind, but once per airport for MinFlightsAt: the server says so, the index only finds them.
            constraint.HasIndex(row => new { row.TourId, row.Kind });
        });

        modelBuilder.Entity<TourRule>(rule =>
        {
            rule.ToTable("fo_rules");
            rule.HasKey(row => row.Id);
            rule.Ignore(row => row.IsGeneral);
            rule.Ignore(row => row.OnTemplate);
            rule.Ignore(row => row.RequestedErrorIds);
            rule.Property(row => row.Code).HasMaxLength(RuleValidation.MaxCodeLength).IsRequired();
            rule.Property(row => row.CheckKey).HasMaxLength(RuleValidation.MaxCheckKeyLength);
            rule.Property(row => row.ParametersJson).HasColumnName("parameters_json").HasColumnType("json").IsRequired();
            rule.HasRowVersion(row => row.RowVersion);

            // A tour deleted takes its rules; a general rule with amendments stays until they go (the server says so first).
            rule.HasOne<Tour>().WithMany().HasForeignKey(row => row.TourId).OnDelete(DeleteBehavior.Cascade);
            rule.HasOne<TourRule>().WithMany().HasForeignKey(row => row.AmendsRuleId).OnDelete(DeleteBehavior.Restrict);
            rule.HasIndex(row => new { row.TourId, row.Sort });
        });

        modelBuilder.Entity<TourError>(error =>
        {
            error.ToTable("fo_errors");
            error.HasKey(row => row.Id);
            error.Property(row => row.CheckKey).HasMaxLength(RuleValidation.MaxCheckKeyLength);
            error.HasRowVersion(row => row.RowVersion);
        });

        modelBuilder.Entity<TourRuleError>(link =>
        {
            link.ToTable("fo_rule_errors");
            link.HasKey(row => new { row.RuleId, row.ErrorId });
            link.HasOne<TourRule>().WithMany(rule => rule.ErrorLinks).HasForeignKey(row => row.RuleId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne<TourError>().WithMany().HasForeignKey(row => row.ErrorId).OnDelete(DeleteBehavior.Cascade);
            link.HasIndex(row => row.ErrorId);
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
