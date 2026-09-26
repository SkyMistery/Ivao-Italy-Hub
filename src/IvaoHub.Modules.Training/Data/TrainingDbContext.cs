using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Sheets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IvaoHub.Modules.Training.Data;

/// <summary>
/// The context of the training: every <c>trn_</c> table, with its own history <c>__EFMigrationsHistory_training</c> (design M3
/// §1, §9). No foreign key towards the core: a <c>vid</c> and a callsign are plain columns.
/// <para>Born with no table of its own (A4): its <c>Initial</c> migration holds only the tables of the core that every module
/// context maps and leaves out of its migrations, and it is never touched again. The tables of the module arrive with the
/// phases that need them, one additive migration each: the items of the evaluation sheet first (A5), then the trainings, whole,
/// and the bans (A6a).</para>
/// </summary>
public sealed class TrainingDbContext(DbContextOptions<TrainingDbContext> options, ICurrentUser? currentUser = null)
    : ModuleDbContext(options, currentUser)
{
    public DbSet<SheetItem> SheetItems => Set<SheetItem>();

    public DbSet<Training> Trainings => Set<Training>();

    public DbSet<TraineeBan> TraineeBans => Set<TraineeBan>();

    /// <summary>The enums of the training are stored as text, like the core's: readable without the code next to them.</summary>
    protected override void ConfigureModuleConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<RatingKind>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<SheetSection>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<TrainingState>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<TrainingRejection>().HaveConversion<string>().HaveMaxLength(16);
    }

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SheetItem>(item =>
        {
            item.ToTable("trn_sheet_items");
            item.HasKey(row => row.Id);
            item.HasRowVersion(row => row.RowVersion);

            // The sheet of one rating, in its order: what the list narrowed to a rating reads, and what a report will.
            item.HasIndex(row => new { row.Kind, row.Rating, row.Sort });
        });

        modelBuilder.Entity<Training>(training =>
        {
            training.ToTable("trn_trainings");
            training.HasKey(row => row.Id);
            training.Ignore(row => row.StakeholderVid);
            training.Ignore(row => row.ResourceScope);
            training.Property(row => row.Position).HasMaxLength(Training.MaxPositionLength);
            training.Property(row => row.AirportIcao).HasMaxLength(Training.MaxAirportLength);
            training.Property(row => row.Fir).HasMaxLength(Training.MaxFirLength);
            training.Property(row => row.TraineeHoursAtRequest).HasPrecision(9, 2);
            training.Property(row => row.AvailabilityText).HasMaxLength(Training.MaxTextLength);
            training.Property(row => row.NotesText).HasMaxLength(Training.MaxTextLength);
            training.Property(row => row.RejectionReason).HasMaxLength(Training.MaxTextLength);
            training.Property(row => row.CloseReason).HasMaxLength(Training.MaxTextLength);

            // The report's comments may run long: text, which the size of a row does not count, bounded by the rules of A9.
            training.Property(row => row.GeneralComment).HasColumnType("text");
            training.Property(row => row.StaffComment).HasColumnType("text");
            training.HasRowVersion(row => row.RowVersion);

            // One open training per trainee and ladder (§2.2 point 2), as a key: the column is empty once the training is not
            // open, and empty values never collide. The trainee's own trainings read through it too.
            training.HasIndex(row => new { row.TraineeVid, row.OpenKind }).IsUnique();

            // The staff's lists by state, and the sessions of a day or about to start (A7, A8).
            training.HasIndex(row => new { row.State, row.ScheduledStartUtc });

            // A trainer's own trainings (A10's queue).
            training.HasIndex(row => new { row.TrainerVid, row.State });
        });

        modelBuilder.Entity<TraineeBan>(ban =>
        {
            ban.ToTable("trn_bans");
            ban.HasKey(row => row.Id);
            ban.Ignore(row => row.StakeholderVid);
            ban.Property(row => row.Reason).HasMaxLength(Training.MaxTextLength).IsRequired();
            ban.HasRowVersion(row => row.RowVersion);

            // A member's bans, read at every request of theirs.
            ban.HasIndex(row => row.Vid);
        });
    }
}

/// <summary>Lets <c>dotnet ef</c> build the model of the training, the same way the hub's is built.</summary>
public sealed class TrainingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TrainingDbContext>
{
    public TrainingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrainingDbContext>();
        options.UseMySql(
            "Server=localhost;Port=3306;Database=ivaohub;User ID=ivaohub;Password=ivaohub",
            new MariaDbServerVersion(HubDbContext.ServerVersion),
            mySql => mySql.MigrationsHistoryTable($"__EFMigrationsHistory_{TrainingModule.ModuleKey}"));
        options.UseSnakeCaseNamingConvention();

        return new TrainingDbContext(options.Options);
    }
}
