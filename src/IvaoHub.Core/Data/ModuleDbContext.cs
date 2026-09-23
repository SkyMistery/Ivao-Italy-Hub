using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Configurations;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Data;

/// <summary>
/// The context of a module: its own tables and its own migration history (plan section 16.12), and
/// the <b>same</b> rule of who reads what as the hub's context (M2). A module that derives from this
/// gets the global query filter on every entity of its that is visible and owned by a department,
/// read from the same current user, and nothing to remember.
/// <para>A module registers it with <c>AddModuleDbContext&lt;T&gt;</c>, which puts the same interceptor
/// behind it: audit, the write check and, for a row in the care of several departments, the base
/// department of the module.</para>
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, ICurrentUser? currentUser = null)
    : DbContext(options), IVisibilityScope
{
    /// <inheritdoc />
    public bool SeesEveryDepartment => currentUser is { IsSuperadmin: true } or { HasAllDepartments: true };

    /// <inheritdoc />
    public bool SeesMemberRows => currentUser is { IsAuthenticated: true };

    /// <inheritdoc />
    public bool SeesStaffRows => currentUser is { IsStaff: true };

    /// <inheritdoc />
    public List<Department> VisibleDepartments => currentUser is null ? [] : [.. currentUser.Departments];

    /// <inheritdoc />
    public int VisibleDepartmentMask => currentUser is null ? 0 : DepartmentMask.Of(currentUser.Departments);

    /// <summary>
    /// The conventions of the hub, then the module's own. Sealed for the same reason as
    /// <see cref="OnModelCreating"/>: the projection tables this context maps are the core's, and
    /// they are written with the core's conventions or not at all.
    /// </summary>
    protected sealed override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        HubDbContext.ApplyConventions(configurationBuilder);
        ConfigureModuleConventions(configurationBuilder);
    }

    /// <summary>
    /// The module's own model first, then the projection tables of the core, then the filter over all
    /// of it. A module overrides <see cref="ConfigureModel"/> rather than this, so that neither the
    /// tables nor the filter can be left out.
    /// </summary>
    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasCharSet(HubDbContext.CharSet).UseCollation(HubDbContext.Collation);
        ConfigureModel(modelBuilder);
        MapProjectionTables(modelBuilder);

        // The same SQL functions the hub's context knows. Until T6 a module context lacked them, and the
        // list of any module resource searching a translated column answered 500: the aircraft groups of
        // T5 had one and no test searched it; the tours found it.
        LocalizedQuery.Register(modelBuilder);
        JsonQuery.Register(modelBuilder);

        VisibilityQueryFilter.ApplyToModel(modelBuilder, this);
    }

    /// <summary>The tables of the module.</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);

    /// <summary>Conventions of the module on top of the hub's, when it has any.</summary>
    protected virtual void ConfigureModuleConventions(ModelConfigurationBuilder configurationBuilder)
    {
    }

    /// <summary>
    /// The tables a row of a module projects into (M2, T4). Until T4 a module context did not map
    /// them, and the interceptor skipped the projection of every row of a module without a word: no
    /// search, no calendar, no award signal (note 2026-09-15-contatti-con-risposte §3.3).
    /// <para>Mapped with the configuration of the core, so a column cannot mean one thing here and
    /// another there, and <b>left out of the module's migrations</b>: the tables belong to the core's
    /// history, and a module only writes into them — on its own connection, inside the transaction of
    /// its own row, which is the whole reason for mapping them rather than calling a service after the
    /// save.</para>
    /// </summary>
    private static void MapProjectionTables(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SearchIndexEntryConfiguration());
        modelBuilder.ApplyConfiguration(new CalendarEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AwardSignalConfiguration());
        modelBuilder.ApplyConfiguration(new MediaUseConfiguration());

        // The threads a row opens (M2, T14, ThreadOpeningProjection): the message and what it cites, never the answers,
        // which only the core writes.
        modelBuilder.ApplyConfiguration(new ContactMessageConfiguration());
        modelBuilder.ApplyConfiguration(new ContactReferenceConfiguration());

        foreach (var projection in new[]
        {
            typeof(SearchIndexEntry), typeof(CalendarEntry), typeof(AwardSignal), typeof(MediaUse),
            typeof(ContactMessage), typeof(ContactReference),
        })
        {
            modelBuilder.Entity(projection).Metadata.SetIsTableExcludedFromMigrations(true);
        }
    }
}
