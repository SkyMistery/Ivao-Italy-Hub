using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>
/// One calendar for the whole division (plan section 9.5). Entries that belong to a module are
/// projections written by the interceptor in the same transaction as the source row; the rest are
/// created by the staff.
/// <para>Owner, visibility and audit columns are declared, not merely present: the global query
/// filter therefore applies, and an entry the staff writes by hand needs <c>Calendar.Edit</c> on
/// its department and gets its four audit columns from the interceptor. The rows the interceptor
/// writes in its own second pass are stamped by <see cref="ProjectionWriter"/> instead, because a
/// projection is the result of a write and not a write of its own.</para>
/// </summary>
[PermissionArea("Calendar")]
public sealed class CalendarEntry : IOwnedByDepartment, IVisible, IAuditable
{
    public long Id { get; set; }

    public Department OwnerDepartment { get; set; }

    /// <summary>For example <c>event</c>, <c>training</c>, <c>tour</c>, <c>meeting</c>, <c>deadline</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public DateTime StartsAtUtc { get; set; }

    public DateTime? EndsAtUtc { get; set; }

    public bool AllDay { get; set; }

    public Visibility Visibility { get; set; }

    /// <summary><c>core</c> for an entry created by the staff, otherwise the module key.</summary>
    public string SourceModule { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public Localized<string> Title { get; set; } = Localized<string>.Empty;

    public Localized<string>? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }
}
