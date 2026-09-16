using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A row of the test module in the care of several departments (M2, note
/// 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.3): what an event will be, with nothing of an
/// event in it. It exists so that the rules the core applies to such a row — the global query filter,
/// the single authorization handler, the write check of the interceptor, the narrowing of a list and
/// the base department of the module — are proved against a real module context and a real table,
/// before the first real module needs them.
/// </summary>
/// <para>Since T3 it also carries the two relations a row can have with a person: the scope a
/// permission can be granted on by itself (<see cref="IHasResourceScope"/>) and the member the row
/// is about (<see cref="IHasStakeholder"/>), so that both are proved here before the first PIREP
/// exists.</para>
[PermissionArea(SampleModule.PermissionArea)]
public sealed class SampleItem : IOwnedByDepartment, IVisible, IAuditable, IHasResourceScope, IHasStakeholder
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>The member this row is about, when it is about one.</summary>
    public int? StakeholderVid { get; set; }

    /// <summary>
    /// What a grant has to name to reach this row alone. A module chooses the shape; the core only
    /// ever compares it.
    /// </summary>
    public string ResourceScope => $"{SampleModule.ModuleKey}:item:{Id}";

    public Visibility Visibility { get; set; }

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    /// <summary>Every department the row is in the care of: its column.</summary>
    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }
}

/// <summary>The context of the test module: one table, and the hub's rule of who reads what.</summary>
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options, ICurrentUser? currentUser = null)
    : ModuleDbContext(options, currentUser)
{
    public DbSet<SampleItem> Items => Set<SampleItem>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SampleItem>(item =>
        {
            item.ToTable("smp_items");
            item.HasKey(row => row.Id);
            item.Property(row => row.Title).HasMaxLength(128).IsRequired();
            item.Ignore(row => row.ResourceScope);
            item.Property(row => row.OwnerDepartment).HasConversion<string>().HasMaxLength(4);
            item.Property(row => row.Visibility).HasConversion<string>().HasMaxLength(16);
        });
    }
}

/// <summary>Lets <c>dotnet ef</c> build the model of the test module, the same way the hub's is built.</summary>
public sealed class SampleDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SampleDbContext>
{
    public SampleDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>();
        options.UseMySql(
            "Server=localhost;Port=3306;Database=ivaohub;User ID=ivaohub;Password=ivaohub",
            new MariaDbServerVersion(HubDbContext.ServerVersion),
            mySql => mySql.MigrationsHistoryTable($"__EFMigrationsHistory_{SampleModule.ModuleKey}"));
        options.UseSnakeCaseNamingConvention();

        return new SampleDbContext(options.Options);
    }
}

public sealed record SampleItemDto(
    long Id,
    string Title,
    Visibility Visibility,
    IReadOnlyList<Department> OwnerDepartments,
    int? StakeholderVid);

public sealed record SampleItemWriteDto(
    string Title,
    Visibility Visibility,
    IReadOnlyList<Department> OwnerDepartments,
    int? StakeholderVid = null);

internal static class SampleItemMapping
{
    public static SampleItemDto ToDto(SampleItem item) =>
        new(
            item.Id,
            item.Title,
            item.Visibility,
            ((IOwnedByDepartment)item).OwnerDepartments,
            item.StakeholderVid);

    public static void Apply(SampleItemWriteDto payload, SampleItem item)
    {
        item.Title = payload.Title;
        item.Visibility = payload.Visibility;
        item.StakeholderVid = payload.StakeholderVid;
        item.OwnerDepartmentMask = DepartmentMask.Of(payload.OwnerDepartments);

        // A module with no base department would say which department is the row's own; this one
        // has one, and the interceptor writes it. The first named is a fair default either way.
        if (payload.OwnerDepartments.Count > 0)
        {
            item.OwnerDepartment = payload.OwnerDepartments[0];
        }
    }
}
