using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>
/// One word of the vocabulary a department files its news and its documents under. It exists so
/// that <see cref="ContentEntry.Category"/> is not free text: without it two editors write "Guides"
/// and "guides" and the public list shows two categories where there is one (design M1 section 3.4).
/// <para>⚠️ There is <b>no foreign key</b> from a content row to one of these, and there never will
/// be. It is the rule that holds between two modules applied to a vocabulary that can change under
/// rows already published: a content row keeps the <see cref="Key"/> it was filed under, and if
/// somebody deletes the category the row keeps its key and the list shows it as it is.</para>
/// <para>The seed is empty on purpose. Plan section 15.8 left the vocabulary open — "to be agreed
/// with each coordinator" — and what M1 decided is not <i>which</i> categories exist but that they
/// are rows a coordinator writes rather than code somebody has to release.</para>
/// </summary>
[Audited]
[PermissionArea("Content")]
public sealed class ContentCategory : IOwnedByDepartment, IAuditable
{
    public long Id { get; set; }

    /// <summary>Which list this word belongs to. A news category is not a document category.</summary>
    public ContentKind Kind { get; set; }

    public Department OwnerDepartment { get; set; }

    /// <summary>
    /// The stable name, the one a content row stores and a URL carries. It never changes: what a
    /// reader is shown is <see cref="Label"/>, and that is the half that may be rewritten and
    /// translated freely.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public Localized<string> Label { get; set; } = Localized<string>.Empty;

    /// <summary>Where the category sits in a grouped list; ties are broken by the key.</summary>
    public int Sort { get; set; }

    /// <summary>
    /// False retires a word without deleting it: the rows filed under it keep their key and stay
    /// where they are, and nobody can file a new one there.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}
