using System.ComponentModel.DataAnnotations.Schema;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>
/// One file in the library of a department: an image uploaded once and used in a hero, in a gallery
/// and as the cover of a news item without being uploaded three times (design M1 section 2).
/// <para>It is owned by a department, visible to somebody and audited like every other row. It is
/// deliberately <b>not</b> projectable: a file is not something a visitor searches for, the page
/// that uses it is.</para>
/// </summary>
[Audited]
public sealed class MediaAsset : IOwnedByDepartment, IVisible, IAuditable
{
    public long Id { get; set; }

    public Department OwnerDepartment { get; set; }

    public Visibility Visibility { get; set; }

    /// <summary>The name the file was uploaded with, kept for whoever downloads it.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The name on disk. Opaque and generated, never derived from <see cref="FileName"/>: two
    /// departments uploading <c>logo.png</c> must not overwrite each other, and a name somebody
    /// chose must never become a path.
    /// </summary>
    public string StoredName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long ByteSize { get; set; }

    /// <summary>Read from the header of the file, for the formats the parser knows.</summary>
    public int? Width { get; set; }

    public int? Height { get; set; }

    /// <summary>
    /// The alternative text, written once next to the file rather than at every use: a block that
    /// leaves its own <c>alt</c> empty inherits this one (design M1 section 1.2).
    /// </summary>
    public Localized<string> Alt { get; set; } = Localized<string>.Empty;

    public Localized<string>? Title { get; set; }

    /// <summary>Free text, chosen by the department; not an enum, like every other category.</summary>
    public string? Category { get; set; }

    /// <summary>
    /// When somebody deleted it. The row survives the deletion on purpose: a page that was already
    /// published names this identifier, and the address a browser has cached is
    /// <c>/media/{id}/{slug}</c> — take the row away and an already printed page breaks. What the
    /// deletion does take away is the file, and only when nothing published still names it.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>False once the file itself is gone from disk, so a serve can answer honestly.</summary>
    public bool HasFile { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>
    /// Where this file is read. Derived from the row rather than stored, so that renaming a file
    /// changes its address and nothing has to be kept in step (see <see cref="MediaUrl"/>).
    /// </summary>
    [NotMapped]
    public string Url => MediaUrl.For(this);
}
