using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IvaoHub.Core.Modules;

/// <summary>
/// A row of a module in the care of several departments always has the base department of the module,
/// whoever writes it and whatever the payload says: events are always the Events department's (Carmine,
/// 13 September 2026; note 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.3). Without a base
/// department, the row's own department is in its set, which is what makes
/// <see cref="IOwnedByDepartment.OwnerDepartment"/> one of them.
/// <para>One rule, applied in the two places that look at a row before it is stored: the generic CRUD
/// engine right after a payload is applied — so that the permission is checked on the row as it will
/// be — and the save changes interceptor, for every other road a row takes into the database.</para>
/// </summary>
public static class ModuleBaseDepartment
{
    public static void Keep(ModuleRegistry? modules, EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Entity is not IOwnedByDepartment || !DepartmentMask.IsStoredOn(entry.Metadata.ClrType))
        {
            return;
        }

        var owner = entry.Property(nameof(IOwnedByDepartment.OwnerDepartment));
        if (modules?.BaseDepartmentOf(entry.Context.GetType()) is { } baseDepartment)
        {
            owner.CurrentValue = baseDepartment;
        }

        var mask = entry.Property(DepartmentMask.PropertyName);
        mask.CurrentValue = (int)mask.CurrentValue! | DepartmentMask.Of((Department)owner.CurrentValue!);
    }
}
