using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
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
public sealed class MediaAsset : IOwnedByDepartment, IVisible, IAuditable, ISharedForReading
{
    /// <summary>
    /// Which files every department may read: the public ones. A logo the web team uploaded is used on a page of Training, and a picker that could not offer it would send somebody to upload it a second time (note
    /// 2026-09-13-contenuti-centralizzati, section 3.4). Changing and deleting one stays with the
    /// department that owns it; a file with visibility <c>Department</c> stays that department's.
    /// <para>Declared once, as an expression, for the same reason as the templates of
    /// <see cref="ContentEntry"/>: the CRUD engine puts it in the <c>WHERE</c> of the list, and the
    /// single authorization handler asks the row itself with the same expression compiled.</para>
    /// </summary>
    public static readonly Expression<Func<MediaAsset, bool>> SharedForReading =
        media => media.Visibility == Visibility.Public;

    private static readonly Func<MediaAsset, bool> SharedForReadingInMemory = SharedForReading.Compile();

    bool ISharedForReading.IsSharedForReading => SharedForReadingInMemory(this);

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

    /// <summary>
    /// The SHA-256 of the bytes, lower case hex, computed while they were written. It is what says
    /// "this file is already here": a second upload of the same bytes into the same department
    /// answers the row that exists (decision note of 12 September 2026). Null on the rows uploaded
    /// before the column existed, which are therefore never matched.
    /// </summary>
    public string? Sha256 { get; set; }

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
