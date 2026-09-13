using IvaoHub.Core.Auth;
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
    /// The module's own model first, then the filter over it. A module overrides
    /// <see cref="ConfigureModel"/> rather than this, so that the filter cannot be left out.
    /// </summary>
    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasCharSet(HubDbContext.CharSet).UseCollation(HubDbContext.Collation);
        ConfigureModel(modelBuilder);
        VisibilityQueryFilter.ApplyToModel(modelBuilder, this);
    }

    /// <summary>The tables of the module.</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);
}
