namespace IvaoHub.Core.Auth.Permissions;

/// <summary>
/// One entry of the permission catalogue. A permission is scoped to the department that owns the
/// resource unless it is declared global (design M0 section 3.7).
/// </summary>
/// <param name="Name">The name, always <c>Area.Action</c>.</param>
/// <param name="IsGlobal">True when the permission has no department to be scoped to.</param>
public sealed record PermissionDescriptor(string Name, bool IsGlobal);

/// <summary>
/// The permissions of the core. Modules add their own through <c>IModule.Permissions</c>; nobody
/// ever adds an authorization handler.
/// <para>Rule of the catalogue: every departmental area declares both <c>View</c> and <c>Edit</c>,
/// and <c>Edit</c> implies <c>View</c> when effective permissions are computed.</para>
/// </summary>
public static class CorePermissions
{
    /// <summary>The area the CRUD engine derives <c>Content.View</c> and <c>Content.Edit</c> from.</summary>
    public const string ContentArea = "Content";

    public const string ContentView = "Content.View";
    public const string ContentEdit = "Content.Edit";
    public const string ContentPublish = "Content.Publish";
    public const string ContentManageTemplates = "Content.ManageTemplates";

    /// <summary>
    /// Putting an interactive block on a page, whose source is code somebody wrote
    /// (12 September 2026, <c>decisions/2026-09-12-il-blocco-interattivo.md</c>). Separate from
    /// <c>Content.Edit</c> because it is a different kind of act: the frame it lands in can do
    /// nothing to the hub — no origin, no network, no cookies — but what it draws is a statement
    /// about a procedure, and whoever publishes one answers for it.
    /// </summary>
    public const string ContentEmbedCode = "Content.EmbedCode";

    /// <summary>
    /// Taking a page to the site on the division's behalf (note 2026-09-13-contenuti-centralizzati):
    /// putting a page at the top of the site, where the navigation of the public lives, and — from
    /// G19 — approving a page for publication. Held by nobody's level: the director and the web team
    /// reach it because they reach every permission, and anybody else only by a grant.
    /// </summary>
    public const string ContentApprove = "Content.Approve";

    /// <summary>The area the CRUD engine derives <c>Links.View</c> and <c>Links.Edit</c> from.</summary>
    public const string LinksArea = "Links";

    public const string LinksView = "Links.View";
    public const string LinksEdit = "Links.Edit";

    /// <summary>The area the CRUD engine derives <c>Media.View</c> and <c>Media.Edit</c> from.</summary>
    public const string MediaArea = "Media";

    public const string MediaView = "Media.View";
    public const string MediaEdit = "Media.Edit";

    /// <summary>The area the CRUD engine derives <c>Calendar.View</c> and <c>Calendar.Edit</c> from.</summary>
    public const string CalendarArea = "Calendar";

    public const string CalendarView = "Calendar.View";
    public const string CalendarEdit = "Calendar.Edit";

    /// <summary>
    /// Who decides the vocabulary of kinds, which belongs to the division and not to a department.
    /// ⚠️ Global on purpose, and therefore held by the roles that reach everywhere and by nobody
    /// else: a coordinator writes entries of their own department, and the words those entries are
    /// filed under are the same for all nine (decided 7 Sep 2026, after the demo of M1).
    /// </summary>
    public const string CalendarManageKinds = "Calendar.ManageKinds";

    /// <summary>The area the CRUD engine derives <c>Contacts.View</c> and <c>Contacts.Edit</c> from.</summary>
    public const string ContactsArea = "Contacts";

    public const string ContactsView = "Contacts.View";
    public const string ContactsEdit = "Contacts.Edit";

    /// <summary>The area the CRUD engine derives <c>Menu.View</c> and <c>Menu.Edit</c> from.</summary>
    public const string MenuArea = "Menu";

    public const string MenuView = "Menu.View";
    public const string MenuEdit = "Menu.Edit";

    public const string PermissionsManage = "Permissions.Manage";
    public const string ModulesManage = "Modules.Manage";
    public const string AuditView = "Audit.View";
    public const string AwardsAssign = "Awards.Assign";
    public const string AdminAccess = "Admin.Access";

    /// <summary>
    /// The core's own contribution to the catalogue, in a stable order. It is not "the catalogue":
    /// what the installation runs with is this plus every <c>IModule.Permissions</c>, composed into
    /// <see cref="PermissionCatalog"/>, which is what anything asking "is this a permission?" asks.
    /// </summary>
    public static readonly IReadOnlyList<PermissionDescriptor> All =
    [
        new(ContentView, IsGlobal: false),
        new(ContentEdit, IsGlobal: false),
        new(ContentPublish, IsGlobal: false),
        new(ContentManageTemplates, IsGlobal: false),
        new(ContentEmbedCode, IsGlobal: false),
        new(ContentApprove, IsGlobal: false),
        new(LinksView, IsGlobal: false),
        new(LinksEdit, IsGlobal: false),
        new(MediaView, IsGlobal: false),
        new(MediaEdit, IsGlobal: false),
        new(CalendarView, IsGlobal: false),
        new(CalendarEdit, IsGlobal: false),
        new(CalendarManageKinds, IsGlobal: true),
        new(ContactsView, IsGlobal: false),
        new(ContactsEdit, IsGlobal: false),
        new(MenuView, IsGlobal: false),
        new(MenuEdit, IsGlobal: false),
        new(PermissionsManage, IsGlobal: true),
        new(ModulesManage, IsGlobal: true),
        new(AuditView, IsGlobal: true),
        new(AwardsAssign, IsGlobal: true),
        new(AdminAccess, IsGlobal: true),
    ];
}
