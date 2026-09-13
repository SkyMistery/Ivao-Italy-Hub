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

    public int SchemaVersion { get; set; } = 1;

    public string? Changelog { get; set; }

    public DateTime PublishedAt { get; set; }

    public int PublishedBy { get; set; }

    public ContentEntry? Content { get; set; }
}
