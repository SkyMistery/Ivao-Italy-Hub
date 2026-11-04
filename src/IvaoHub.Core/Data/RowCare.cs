using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IvaoHub.Core.Data;

/// <summary>
/// A row whose departments are its parent's — the rows of an event, of a tour — takes them from the parent as it is now, before
/// the permission is asked (note 2026-11-05-la-cura-che-una-riga-prende-dal-genitore). What the row stored is a copy that may be
/// old: the parent's care changed after the row was written.
/// <para>So the departments are written as the row's <b>original</b> values as well as its current ones: the write guard, which
/// judges a deletion and a move on the row as it was loaded (issue #319, items 1 and 2), judges it on its parent as it is now —
/// the review of #233, point 1 — and never on a department the writer put in. A change still writes them to the table. The
/// audit's "before" of such a row shows its parent's departments, not the old copy.</para>
/// </summary>
public static class RowCare
{
    /// <summary>The parent's department and care on the row, as if the row had been stored with them.</summary>
    public static void Adopt(DbContext database, IOwnedByDepartment row, Department owner, int mask)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(row);

        var entry = database.Entry(row);
        Write(entry.Property(nameof(IOwnedByDepartment.OwnerDepartment)), owner);
        if (DepartmentMask.IsStoredOn(entry.Metadata.ClrType))
        {
            Write(entry.Property(DepartmentMask.PropertyName), mask);
        }
    }

    private static void Write(PropertyEntry property, object value)
    {
        var stored = property.EntityEntry.State is EntityState.Unchanged or EntityState.Modified or EntityState.Deleted;
        var differs = stored && !Equals(property.OriginalValue, value);

        property.CurrentValue = value;
        if (!stored)
        {
            return;
        }

        property.OriginalValue = value;

        // The table still holds the old copy: a change writes the parent's over it.
        if (differs && property.EntityEntry.State != EntityState.Deleted)
        {
            property.IsModified = true;
        }
    }
}
