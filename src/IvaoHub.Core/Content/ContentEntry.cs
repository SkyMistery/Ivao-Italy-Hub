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
/// What an operational document is, when it is one (G14, note
/// 2026-09-10-il-documento-operativo-come-va-ivao-aero). Orthogonal to the category: a Tower SOP
/// and a Tower LoA are both filed under "Tower". Stored as its name; a new one is additive.
/// </summary>
public enum DocumentType
{
    /// <summary>Standard operating procedures of a position.</summary>
    Sop,

    /// <summary>A letter of agreement between two positions or units.</summary>
    Loa,
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

    // ---- the operational document (G14) --------------------------------------------------------
    // Six facts a controller's document carries and a page does not, all nullable: a document that
    // is a guide rather than a SOP simply has none of them. Columns of `cms_contents`, not a table
    // of their own, for the reason the news columns are (plan section 9.3).

    /// <summary>SOP or LoA. Null for a document that is neither.</summary>
    public DocumentType? DocumentType { get; set; }

    /// <summary>The position the document is about, as a callsign (<c>LIRF_TWR</c>).</summary>
    public string? PrimaryPosition { get; set; }

    /// <summary>The other side of a letter of agreement, or the position handed over to.</summary>
    public string? SecondaryPosition { get; set; }

    /// <summary>An airport of the division's snapshot; the validator refuses any other.</summary>
    public string? Icao { get; set; }

    /// <summary>A centre of the division's snapshot; the validator refuses any other.</summary>
    public string? Fir { get; set; }

    /// <summary>In force from this day; in the future, the reader is told so.</summary>
    public DateTime? EffectiveOn { get; set; }

    /// <summary>To be reviewed by this day; past it, the owning department is told once.</summary>
    public DateTime? ReviewOn { get; set; }

    /// <summary>
    /// When the document stopped being in force. Set, it is <b>archived</b>; with a successor too,
    /// <b>superseded</b>. Not a value of <see cref="PublishStatus"/>: a retired document stays
    /// published and readable, with the notice on top — somebody arriving from an old link needs
    /// the way on, not a 404 (implementation plan, G14).
    /// </summary>
    public DateTime? RetiredAt { get; set; }

    /// <summary>The document that replaced this one, when it was replaced rather than dropped.</summary>
    public long? SupersededById { get; set; }

    /// <summary>When the owning department was told the review date had passed, so it is told once.</summary>
    public DateTime? ReviewNotifiedAt { get; set; }

    /// <summary>Whether the footer — version, date, publisher, AIRAC, print — is drawn at the end.</summary>
    public bool ShowFooter { get; set; } = true;

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
