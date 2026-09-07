using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>Which menu of the site an entry belongs to.</summary>
public enum MenuScope
{
    /// <summary>The navigation at the top of every page.</summary>
    Public,

    /// <summary>The links at the bottom of every page.</summary>
    Footer,
}

/// <summary>
/// One entry of the site navigation. The menu of an editorial site is editorial: it is a table the
/// staff edits, not a list in the code, and taking an entry out of it takes it off the site without
/// anything being recompiled (design M1 section 8.1).
/// <para><see cref="OwnerDepartment"/> is pinned to <see cref="Owner"/> and is not part of the write
/// payload: the menu belongs to the web team, and saying so is the whole of its authorisation — the
/// single handler and the write guard of the interceptor then decide who may touch it, with no rule
/// written here. A coordinator of another department holds <c>Menu.Edit</c> on their own department
/// and therefore on no row that exists.</para>
/// <para>Depth is <b>one</b>: an entry either sits at the top or under one that does. A three level
/// menu is a menu nobody uses, and the second level is already the last one a navigation bar can
/// draw without becoming a different component.</para>
/// </summary>
[Audited]
[PermissionArea("Menu")]
public sealed class MenuItem : IOwnedByDepartment, IVisible, IAuditable
{
    /// <summary>
    /// The department the menu belongs to: the one that owns the site, the same one the system
    /// templates and the seeded pages belong to (<see cref="SiteOwnership"/>). It is a constant and
    /// not a column somebody can move — a menu owned by two departments is a menu with two orders.
    /// </summary>
    public const Department Owner = SiteOwnership.Department;

    public long Id { get; set; }

    public MenuScope Scope { get; set; }

    /// <summary>The entry this one hangs under, or null for a top level one.</summary>
    public long? ParentId { get; set; }

    /// <summary>Where the entry sits among its siblings; ties are broken by the identifier.</summary>
    public int Sort { get; set; }

    /// <summary>
    /// What the reader sees, already translated. It is text and not a translation key, which is the
    /// difference between an editorial entry and one a module registers: the module cannot know the
    /// language of the browser, and the editor cannot be asked to invent a key.
    /// </summary>
    public Localized<string> Label { get; set; } = Localized<string>.Empty;

    /// <summary>
    /// Where it leads. A path of this site, or an absolute address of somewhere else: both are
    /// things a division puts in its own menu, and the client decides how to follow one.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Who the entry is shown to, enforced by the global query filter like every other visible row.
    /// A menu entry to a members' page has no business being on screen for a visitor.
    /// </summary>
    public Visibility Visibility { get; set; }

    /// <summary>False hides the entry without deleting it, for a page that is not ready yet.</summary>
    public bool IsActive { get; set; } = true;

    public Department OwnerDepartment { get; set; } = Owner;

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}
