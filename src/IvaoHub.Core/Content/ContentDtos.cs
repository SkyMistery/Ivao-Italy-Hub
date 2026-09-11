using System.Text.Json.Nodes;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Content;

/// <summary>
/// A content row as a list shows it. The body is deliberately absent: a list of pages does not
/// need a megabyte of blocks per row to draw a table.
/// </summary>
public sealed record ContentListDto(
    long Id,
    ContentKind Kind,
    string Slug,
    Department OwnerDepartment,
    Visibility Visibility,
    PublishStatus Status,
    bool IsTemplate,
    Localized<string> Title,
    string? Category,
    long? CoverMediaId,
    bool Pinned,
    int Sort,
    long? FileMediaId,
    DocumentType? DocumentType,
    DateTime? ReviewOn,
    DateTime? RetiredAt,
    long? SupersededById,
    DateTime? PublishedAt,
    DateTime UpdatedAt);

/// <summary>
/// A content row in full, as the editor loads it. <see cref="Body"/> travels as the JSON it is:
/// the backend never learned what a block means and it is not going to start here.
/// </summary>
public sealed record ContentDetailDto(
    long Id,
    ContentKind Kind,
    string Slug,
    Department OwnerDepartment,
    Visibility Visibility,
    PublishStatus Status,
    long? TemplateId,
    bool IsTemplate,
    Localized<string> Title,
    Localized<string>? Summary,
    Localized<JsonNode>? Seo,
    JsonNode Body,
    int SchemaVersion,
    string? Category,
    long? CoverMediaId,
    bool Pinned,
    int Sort,
    long? FileMediaId,
    DocumentType? DocumentType,
    string? PrimaryPosition,
    string? SecondaryPosition,
    string? Icao,
    string? Fir,
    DateTime? EffectiveOn,
    DateTime? ReviewOn,
    DateTime? RetiredAt,
    long? SupersededById,
    bool ShowFooter,
    long? PublishedVersionId,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy,
    DateTime RowVersion);

/// <summary>
/// What a client may set on a content row.
/// <para>Four things are missing on purpose. The audit columns and <c>publishedAt</c> are filled by
/// the interceptor and by publication. <c>status</c> is not a field either: a page becomes public
/// by being published, which is an endpoint with its own permission, not a checkbox. And
/// <c>templateId</c> is written once, by "new from template", so that the record of where a page
/// came from cannot be rewritten afterwards.</para>
/// <para>The operational fields (G14) are nullable and only a <c>Document</c> may carry them; the
/// validator refuses them on any other kind. <c>reviewNotifiedAt</c> is not here: the job writes
/// it, and a client that could clear it would be a client that could make the reminder ring twice.</para>
/// </summary>
public sealed record ContentWriteDto(
    ContentKind Kind,
    string Slug,
    Department OwnerDepartment,
    Visibility Visibility,
    bool IsTemplate,
    Localized<string> Title,
    Localized<string>? Summary,
    Localized<JsonNode>? Seo,
    JsonNode Body,
    int SchemaVersion,
    string? Category,
    long? CoverMediaId,
    bool Pinned,
    int Sort,
    long? FileMediaId,
    DocumentType? DocumentType,
    string? PrimaryPosition,
    string? SecondaryPosition,
    string? Icao,
    string? Fir,
    DateTime? EffectiveOn,
    DateTime? ReviewOn,
    DateTime? RetiredAt,
    long? SupersededById,
    DateTime RowVersion,
    // Last and defaulted: a client that never heard of the footer keeps it, rather than turning
    // it off on every page it saves.
    bool ShowFooter = true);

/// <summary>
/// What the public site is given: the published version and nothing about the draft behind it.
/// There is no row version, no audit trail and no status, because a visitor has nothing to do with
/// any of them.
/// <para>The three that belong to a <c>kind</c> travel because the page around the body needs them:
/// a news item shows its cover and its category above the blocks, and a document with a file is a
/// card with a download rather than something to read (design M1 section 3.3). They are read from
/// the row and not from the version, like the summary next to them: they are what the row <i>is</i>,
/// not what somebody wrote in it.</para>
/// </summary>
public sealed record PublicContentDto(
    ContentKind Kind,
    string Slug,
    Department OwnerDepartment,
    Localized<string> Title,
    Localized<string>? Summary,
    Localized<JsonNode>? Seo,
    JsonNode Body,
    int SchemaVersion,
    string? Category,
    long? CoverMediaId,
    long? FileMediaId,
    int Version,
    DateTime PublishedAt,
    // ---- the operational document (G14): the strip under the title, the notice, the footer ----
    DocumentType? DocumentType,
    string? PrimaryPosition,
    string? SecondaryPosition,
    string? Icao,
    string? Fir,
    DateTime? EffectiveOn,
    DateTime? ReviewOn,
    DateTime? RetiredAt,
    // Where the reader is sent when this one was superseded: the successor's address and name.
    string? SupersededBySlug,
    Localized<string>? SupersededByTitle,
    bool ShowFooter,
    // Who published, as a name: the VID is nobody's business on the public site.
    string? PublishedByName,
    string? Airac);

/// <summary>What publication is told, beyond which row it is about.</summary>
/// <param name="Changelog">A line for the staff about what changed. Never shown to a visitor.</param>
/// <param name="Airac">The AIRAC cycle of this publication (<c>2609</c>), optional, shown on the footer.</param>
public sealed record ContentPublishRequest(string? Changelog, string? Airac = null);

/// <summary>
/// What stands between a row and the public, asked before anybody presses publish.
/// <para>The shape is the refusal's own — one i18n key per path, and the languages that are
/// missing beside it — because the editor draws both with the same component. What differs is only
/// the moment it is asked for: this one answers 200 with two empty maps when there is nothing in
/// the way, where the refusal is a 400 nobody asked for.</para>
/// </summary>
/// <param name="Errors">One or more i18n keys per field, keyed by the path the editor knows.</param>
/// <param name="Localized">
/// For the fields whose problem is a missing translation, which languages are missing.
/// </param>
public sealed record ContentPublishProblemsDto(
    IReadOnlyDictionary<string, string[]> Errors,
    IReadOnlyDictionary<string, string[]> Localized);

/// <summary>
/// Entity to payload and back, generated. The body is the one field that needs saying out loud:
/// the column holds text and the contract holds JSON, so the two conversions live here and the
/// mapper uses them wherever the pair turns up.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class ContentMapper
{
    [MapProperty(nameof(ContentEntry.BodyJson), nameof(ContentDetailDto.Body))]
    public partial ContentDetailDto ToDetail(ContentEntry content);

    public partial ContentListDto ToList(ContentEntry content);

    [MapProperty(nameof(ContentWriteDto.Body), nameof(ContentEntry.BodyJson))]
    public partial void Apply(ContentWriteDto payload, ContentEntry content);

    /// <summary>An empty column is an empty document, never a null the renderer would trip on.</summary>
    private static JsonNode ParseBody(string json) => JsonNode.Parse(json) ?? new JsonObject();

    /// <summary>
    /// Both signatures are non-nullable on purpose. <c>JsonNode</c> carries explicit conversions to
    /// every primitive, so a nullable parameter here would not match the property and the generator
    /// would quietly reach for <c>(string)body</c> instead — which compiles, and throws at run time
    /// on the first page that is an object rather than a string.
    /// </summary>
    private static string WriteBody(JsonNode body) => body.ToJsonString();
}
