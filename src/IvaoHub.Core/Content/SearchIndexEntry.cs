using IvaoHub.Core.Division;

namespace IvaoHub.Core.Content;

/// <summary>
/// A projection, not an entity: one row per source row and per language, rewritten in full every
/// time the source is saved (design M0 section 3.6). One row per language is what makes a FULLTEXT
/// index possible without hardcoding a column per language, which no migration could do for a
/// division that has not been forked yet.
/// <para>It carries an owner and a visibility, and declares them, so the global query filter
/// applies to it like to anything else: a search endpoint cannot return a row the reader may not
/// see, whatever it forgot. Only <see cref="ProjectionWriter"/> reads it past the filter, and it is
/// the only thing that writes it — a hand written insert would be refused by the write guard for
/// want of a permission called <c>SearchIndex.Edit</c>, which is the correct answer.</para>
/// </summary>
public sealed class SearchIndexEntry : IOwnedByDepartment, IVisible
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
