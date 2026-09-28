using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Exams;
using IvaoHub.Modules.Training.Sessions;
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
/// and the bans (A6a), then the dates the trainers propose (A8), then the sessions that are over and the sheets the reports
/// filled (A9), then the exams in the calendar (A10c).</para>
/// </summary>
public sealed class TrainingDbContext : ModuleDbContext
{
    /// <param name="options">The context's options, as the core registers every context.</param>
    /// <param name="currentUser">Who reads and writes, for the filters and the guard of the core.</param>
    /// <param name="vocabulary">
    /// The core's ratings: every training this context tracks is told the short name of its own (<see cref="Training.RatingShortName"/>),
    /// because the session it projects into the calendar is called by it (A8), and a projection is worked out by the row itself; every
    /// exam is told the vocabulary, and names its rating when it projects, because its rating may change after it was read (A10c).
    /// </param>
    public TrainingDbContext(DbContextOptions<TrainingDbContext> options, ICurrentUser? currentUser = null, RatingVocabulary? vocabulary = null)
        : base(options, currentUser)
    {
        if (vocabulary is not null)
        {
            // ⚠️ Only a row this context tracks is told. One read with AsNoTracking and handed to the core's ProjectionRefresh, or
            // one of a context built without the vocabulary (dotnet ef's, a test's by hand), projects its title with no rating: its
            // position alone, and a pilot's training or exam, which has none, becomes "#id".
            ChangeTracker.Tracked += (_, tracked) =>
            {
                if (tracked.Entry.Entity is Training training)
                {
                    training.RatingShortName = vocabulary.Find(training.Kind, training.Rating)?.ShortName;
                }
                else if (tracked.Entry.Entity is Exam exam)
                {
                    exam.Knows(vocabulary);
                }
            };
        }
    }

    public DbSet<SheetItem> SheetItems => Set<SheetItem>();

    public DbSet<Training> Trainings => Set<Training>();

    public DbSet<TraineeBan> TraineeBans => Set<TraineeBan>();

    public DbSet<TrainingSlot> Slots => Set<TrainingSlot>();

    public DbSet<TrainingSession> Sessions => Set<TrainingSession>();

    public DbSet<TrainingEvaluation> Evaluations => Set<TrainingEvaluation>();

    public DbSet<Exam> Exams => Set<Exam>();

    /// <summary>The enums of the training are stored as text, like the core's: readable without the code next to them.</summary>
    protected override void ConfigureModuleConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<RatingKind>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<SheetSection>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<TrainingState>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<TrainingRejection>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<SessionOutcome>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<TheoryMark>().HaveConversion<string>().HaveMaxLength(16);
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
            training.Ignore(row => row.RatingShortName);
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

        modelBuilder.Entity<TrainingSlot>(slot =>
        {
            slot.ToTable("trn_slots");
            slot.HasKey(row => row.Id);
            slot.Property(row => row.WarningsJson).HasColumnName("warnings_json").HasColumnType("json").IsRequired();

            // A child of its training, in the same context: it goes with it, and its index is the one every reading of the
            // proposals of a training uses.
            slot.HasOne<Training>().WithMany().HasForeignKey(row => row.TrainingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingSession>(session =>
        {
            session.ToTable("trn_sessions");
            session.HasKey(row => row.Id);
            session.Property(row => row.InternalNotes).HasMaxLength(Training.MaxTextLength);

            // A child of its training, like the dates: its history goes only with it.
            session.HasOne<Training>().WithMany().HasForeignKey(row => row.TrainingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingEvaluation>(evaluation =>
        {
            evaluation.ToTable("trn_evaluations");
            evaluation.HasKey(row => row.Id);
            evaluation.Property(row => row.TraineeComment).HasMaxLength(Training.MaxTextLength);
            evaluation.Property(row => row.StaffNote).HasMaxLength(Training.MaxTextLength);

            // A child of its training: the sheet its report filled, read in the order of the sheet.
            evaluation.HasOne<Training>().WithMany().HasForeignKey(row => row.TrainingId).OnDelete(DeleteBehavior.Cascade);
            evaluation.HasIndex(row => new { row.TrainingId, row.Sort });

            // No key towards the items: the report reads its copy of them. «Does a report mark this item?» reads this index.
            evaluation.HasIndex(row => row.SheetItemId);
        });

        modelBuilder.Entity<Exam>(exam =>
        {
            exam.ToTable("trn_exams");
            exam.HasKey(row => row.Id);
            exam.Ignore(row => row.RatingShortName);
            exam.Property(row => row.Position).HasMaxLength(Training.MaxPositionLength);
            exam.HasRowVersion(row => row.RowVersion);

            // The exams still to come, the soonest first (the site); and an examiner's own (the list, «mine»).
            exam.HasIndex(row => row.StartsAtUtc);
            exam.HasIndex(row => row.ExaminerVid);
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
