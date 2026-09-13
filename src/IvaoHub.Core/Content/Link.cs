using System.Linq.Expressions;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>
/// A link published by a department: Discord, social accounts, the national ANSP, tools.
/// It is the guinea pig of M0 (plan section 16.15): localized, owned by a department, visible to
/// somebody, audited, exposed by the generic CRUD engine and projected into the search index.
/// </summary>
[Audited]
public sealed class Link : IOwnedByDepartment, IVisible, IAuditable, IProjectable, ISharedForReading
{
    /// <summary>
    /// Which links every department may read: the public ones. The Discord of the division is one link, whichever department wrote it down, and every department puts it on its pages (note
    /// 2026-09-13-contenuti-centralizzati, section 3.4). Changing and deleting one stays with the
    /// department that owns it; a link with visibility <c>Department</c> stays that department's.
    /// <para>Declared once, as an expression, for the same reason as the templates of
    /// <see cref="ContentEntry"/>: the CRUD engine puts it in the <c>WHERE</c> of the list, and the
    /// single authorization handler asks the row itself with the same expression compiled.</para>
    /// </summary>
    public static readonly Expression<Func<Link, bool>> SharedForReading =
        link => link.Visibility == Visibility.Public;

    private static readonly Func<Link, bool> SharedForReadingInMemory = SharedForReading.Compile();

    bool ISharedForReading.IsSharedForReading => SharedForReadingInMemory(this);

    public long Id { get; set; }

    public Department OwnerDepartment { get; set; }

    public Visibility Visibility { get; set; }

    public Localized<string> Title { get; set; } = Localized<string>.Empty;

    public string Url { get; set; } = string.Empty;

    public Localized<string>? Description { get; set; }

    /// <summary>Free text, chosen by the department; not an enum on purpose.</summary>
    public string? Category { get; set; }

    public int Sort { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    string IProjectable.SourceModule => ProjectionSource.Core;

    string IProjectable.SourceId => $"link:{Id}";

    /// <summary>
    /// A link is findable by its title and its description, and points at the site it links to:
    /// there is no page of the hub to send the reader to.
    /// </summary>
    ProjectionSnapshot? IProjectable.Project(ProjectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsActive)
        {
            return null;
        }

        return ProjectionSnapshot.ForSearch(new SearchProjection(
            Kind: "link",
            Url: Url,
            OwnerDepartment: OwnerDepartment,
            Visibility: Visibility,
            Title: Title,
            Text: Description ?? Localized<string>.Empty));
    }
}
