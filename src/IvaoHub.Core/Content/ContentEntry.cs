using System.Linq.Expressions;
using System.Text.Json.Nodes;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>What an editorial row is. One table for all three (plan section 9.3).</summary>
public enum ContentKind
{
    Page,
    News,
    Document,

    /// <summary>
    /// The home of a department inside the back office: one row per department, seeded from a
    /// system template and edited in the editor every department already uses (design M1 section
    /// 14, note 2026-09-05-dashboard-di-dipartimento).
    /// <para>Last in the enum because the values are stored as their names and a new one has to be
    /// additive. It is not a public address: <see cref="ContentEntry.Url"/> sends it to
    /// <c>/staff/{department}</c>, and the public route only ever serves <see cref="Page"/>.</para>
    /// </summary>
    Dashboard,
}

/// <summary>
/// Any editorial content: a page, a news item, a document, or the template one of them was created
/// from. The body is an opaque tree of sections and blocks; the backend only ever checks the
/// envelope and its size, never the properties of a block (plan section 16.5).
/// </summary>
[Audited]
[PermissionArea("Content")]
public sealed class ContentEntry
    : IOwnedByDepartment, IVisible, IPublishable, IAuditable, IProjectable, ISharedForReading
{
    /// <summary>
    /// Which content rows every department may read: the templates, and only those. A template
    /// belongs to the department that made it and is edited by that department alone, but a
    /// coordinator of another one has to be able to see it — without this, "new from a template"
    /// does not exist for eight departments out of nine, and a page born from a template its editor
    /// cannot read loses the template's own restrictions in the editor (design M1 section 9.4,
    /// note 2026-09-05-template-di-sistema-e-dipartimenti).
    /// <para>Declared once, as an expression, because it is asked in two languages: the CRUD engine
    /// puts it in the <c>WHERE</c> of the list, and the single authorization handler asks the row
    /// itself. The second reading is this same expression compiled, never a copy of it.</para>
    /// </summary>
    public static readonly Expression<Func<ContentEntry, bool>> SharedForReading =
        content => content.IsTemplate;

    private static readonly Func<ContentEntry, bool> SharedForReadingInMemory = SharedForReading.Compile();

    public long Id { get; set; }

    public ContentKind Kind { get; set; }

    /// <summary>Unique per <c>(kind, slug, is_template)</c>: MariaDB has no filtered indexes.</summary>
    public string Slug { get; set; } = string.Empty;

    public Department OwnerDepartment { get; set; }

    public Visibility Visibility { get; set; }

    public PublishStatus Status { get; set; }

    /// <summary>The template this row was created from, if any.</summary>
    public long? TemplateId { get; set; }

    public bool IsTemplate { get; set; }

    public Localized<string> Title { get; set; } = Localized<string>.Empty;

    public Localized<string>? Summary { get; set; }

    public Localized<JsonNode>? Seo { get; set; }

    /// <summary>The section and block tree, opaque JSON validated only as an envelope.</summary>
    public string BodyJson { get; set; } = "{}";

    public int SchemaVersion { get; set; } = 1;

    public long? PublishedVersionId { get; set; }

    public DateTime? PublishedAt { get; set; }

    /// <summary>News only: editorial category.</summary>
    public string? Category { get; set; }

    /// <summary>News only: cover image.</summary>
    public long? CoverMediaId { get; set; }

    /// <summary>News only: pinned to the top of the list.</summary>
    public bool Pinned { get; set; }

    /// <summary>Documents only: manual ordering inside a category.</summary>
    public int Sort { get; set; }

    /// <summary>Documents only: the attached file, when the document is a file rather than a page.</summary>
    public long? FileMediaId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>
    /// Where this row is read. One place decides, so the search index agrees with it.
    /// <para>A dashboard is the one kind whose address is not on the public site: it is the home of
    /// a department in the back office, and it is its <b>department</b> and not its slug that says
    /// which one — the slug is the department's own code, so the two agree, and this expression is
    /// the one that would still be right if they ever did not.</para>
    /// </summary>
    public string Url => Kind switch
    {
        ContentKind.News => $"/news/{Slug}",
        ContentKind.Document => $"/documents/{Slug}",
        ContentKind.Dashboard => $"/staff/{OwnerDepartment.ToString().ToLowerInvariant()}",
        _ => $"/{Slug}",
    };

    bool ISharedForReading.IsSharedForReading => SharedForReadingInMemory(this);

    string IProjectable.SourceModule => ProjectionSource.Core;

    string IProjectable.SourceId => $"content:{Id}";

    /// <summary>
    /// The text of a page is whatever its blocks say, in each language, extracted by the one
    /// walker that knows the envelope. A template is a tool of the staff and is never findable;
    /// a draft is stopped earlier, by the interceptor, for every publishable entity at once.
    /// </summary>
    ProjectionSnapshot? IProjectable.Project(ProjectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsTemplate)
        {
            return null;
        }

        var body = JsonNode.Parse(BodyJson);
        var text = context.Locales.ToDictionary(
            locale => locale,
            locale => context.Blocks.ExtractText(body, locale),
            StringComparer.OrdinalIgnoreCase);

        return ProjectionSnapshot.ForSearch(new SearchProjection(
            Kind: Kind.ToString().ToLowerInvariant(),
            Url: Url,
            OwnerDepartment: OwnerDepartment,
            Visibility: Visibility,
            Title: Title,
            Text: new Localized<string>(text)));
    }
}
