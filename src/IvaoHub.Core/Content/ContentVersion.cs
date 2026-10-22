using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>
/// The frozen picture of what the public sees. The public site reads this and never the draft,
/// so editing a page in the back office changes nothing until somebody publishes.
/// </summary>
public sealed class ContentVersion
{
    public long Id { get; set; }

    public long ContentId { get; set; }

    /// <summary>Increasing per content, starting at 1.</summary>
    public int Version { get; set; }

    public Localized<string> Title { get; set; } = Localized<string>.Empty;

    /// <summary>The body with the data blocks marked as frozen already resolved.</summary>
    public string BodyJson { get; set; } = "{}";

    // ---- the text of the row around the body, as it was published (note
    // 2026-10-21-i-campi-fuori-dalla-versione): what the public reads of a row is this, never the
    // row, which goes on being written. A document's dates, Pinned and Sort stay on the row: they
    // are management, not text.

    public Localized<string>? Summary { get; set; }

    public Localized<JsonNode>? Seo { get; set; }

    /// <summary>The collections the row was filed in, as a JSON array of keys (G20).</summary>
    public string CollectionsJson { get; set; } = "[]";

    /// <summary><see cref="CollectionsJson"/>, read as the list it is.</summary>
    public IReadOnlyList<string> Collections => JsonSerializer.Deserialize<string[]>(CollectionsJson) ?? [];

    /// <summary>News only: the cover image.</summary>
    public long? CoverMediaId { get; set; }

    /// <summary>Documents only: the attached file.</summary>
    public long? FileMediaId { get; set; }

    /// <summary>Whether the footer — version, date, publisher, print — is drawn at the end.</summary>
    public bool ShowFooter { get; set; } = true;

    public int SchemaVersion { get; set; } = 1;

    public string? Changelog { get; set; }

    public DateTime PublishedAt { get; set; }

    public int PublishedBy { get; set; }

    /// <summary>
    /// Who approved this version, when the kind goes through approval (G19): the publisher of an
    /// approved page, said apart from the person who wrote it. Null for a version nobody had to approve.
    /// </summary>
    public int? ApprovedBy { get; set; }

    public ContentEntry? Content { get; set; }
}
