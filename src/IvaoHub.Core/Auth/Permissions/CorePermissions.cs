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
    public const string ContentView = "Content.View";
    public const string ContentEdit = "Content.Edit";
    public const string ContentPublish = "Content.Publish";
    public const string ContentManageTemplates = "Content.ManageTemplates";

    /// <summary>The area the CRUD engine derives <c>Links.View</c> and <c>Links.Edit</c> from.</summary>
    public const string LinksArea = "Links";

    public const string LinksView = "Links.View";
    public const string LinksEdit = "Links.Edit";

    public const string CalendarView = "Calendar.View";
    public const string CalendarEdit = "Calendar.Edit";

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
        new(LinksView, IsGlobal: false),
        new(LinksEdit, IsGlobal: false),
        new(CalendarView, IsGlobal: false),
        new(CalendarEdit, IsGlobal: false),
        new(PermissionsManage, IsGlobal: true),
        new(ModulesManage, IsGlobal: true),
        new(AuditView, IsGlobal: true),
        new(AwardsAssign, IsGlobal: true),
        new(AdminAccess, IsGlobal: true),
    ];
}
