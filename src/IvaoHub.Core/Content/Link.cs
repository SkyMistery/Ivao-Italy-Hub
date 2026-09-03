using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>
/// A link published by a department: Discord, social accounts, the national ANSP, tools.
/// It is the guinea pig of M0 (plan section 16.15): localized, owned by a department, visible to
/// somebody, audited, exposed by the generic CRUD engine and projected into the search index.
/// </summary>
[Audited]
public sealed class Link : IOwnedByDepartment, IVisible, IAuditable, IProjectable
{
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
