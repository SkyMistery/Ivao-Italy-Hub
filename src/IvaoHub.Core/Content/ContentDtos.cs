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
    DateTime RowVersion);

/// <summary>
/// What the public site is given: the published version and nothing about the draft behind it.
/// There is no row version, no audit trail and no status, because a visitor has nothing to do with
/// any of them.
/// </summary>
public sealed record PublicContentDto(
    ContentKind Kind,
    string Slug,
    Localized<string> Title,
    Localized<string>? Summary,
    Localized<JsonNode>? Seo,
    JsonNode Body,
    int SchemaVersion,
    int Version,
    DateTime PublishedAt);

/// <summary>What publication is told, beyond which row it is about.</summary>
/// <param name="Changelog">A line for the staff about what changed. Never shown to a visitor.</param>
public sealed record ContentPublishRequest(string? Changelog);

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
