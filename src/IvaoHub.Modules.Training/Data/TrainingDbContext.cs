using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Sheets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IvaoHub.Modules.Training.Data;

/// <summary>
/// The context of the training: every <c>trn_</c> table, with its own history <c>__EFMigrationsHistory_training</c> (design M3
/// §1, §9). No foreign key towards the core: a <c>vid</c> and a callsign are plain columns.
/// <para>Born with no table of its own (A4): its <c>Initial</c> migration holds only the tables of the core that every module
/// context maps and leaves out of its migrations, and it is never touched again. The tables of the module arrive with the
/// phases that need them, one additive migration each: the items of the evaluation sheet first (A5).</para>
/// </summary>
public sealed class TrainingDbContext(DbContextOptions<TrainingDbContext> options, ICurrentUser? currentUser = null)
    : ModuleDbContext(options, currentUser)
{
    public DbSet<SheetItem> SheetItems => Set<SheetItem>();

    /// <summary>The enums of the training are stored as text, like the core's: readable without the code next to them.</summary>
    protected override void ConfigureModuleConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<RatingKind>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<SheetSection>().HaveConversion<string>().HaveMaxLength(16);
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
