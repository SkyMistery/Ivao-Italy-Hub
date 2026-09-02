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
}

/// <summary>
/// Any editorial content: a page, a news item, a document, or the template one of them was created
/// from. The body is an opaque tree of sections and blocks; the backend only ever checks the
/// envelope and its size, never the properties of a block (plan section 16.5).
/// </summary>
public sealed class ContentEntry
{
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
}
