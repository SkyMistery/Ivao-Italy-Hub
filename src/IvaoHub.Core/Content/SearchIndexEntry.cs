using IvaoHub.Core.Division;

namespace IvaoHub.Core.Content;

/// <summary>
/// A projection, not an entity: one row per source row and per language, rewritten in full every
/// time the source is saved (design M0 section 3.6). One row per language is what makes a FULLTEXT
/// index possible without hardcoding a column per language, which no migration could do for a
/// division that has not been forked yet.
/// </summary>
public sealed class SearchIndexEntry
{
    public long Id { get; set; }

    /// <summary><c>core</c> for the editorial core, otherwise the module key.</summary>
    public string SourceModule { get; set; } = string.Empty;

    /// <summary>Stable identifier of the source row, for example <c>link:42</c>.</summary>
    public string SourceId { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    /// <summary>What the row is, used to pick an icon and to group results.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public Department OwnerDepartment { get; set; }

    public Visibility Visibility { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
