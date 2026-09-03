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

    /// <summary>The whole catalogue of the core, in a stable order.</summary>
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

    /// <summary>The departmental permissions, the ones a coordinator holds on their own department.</summary>
    public static readonly IReadOnlyList<string> Departmental =
        [.. All.Where(permission => !permission.IsGlobal).Select(permission => permission.Name)];

    /// <summary>The permissions that have no department, and that a grant may therefore never confer.</summary>
    public static readonly IReadOnlyList<string> Global =
        [.. All.Where(permission => permission.IsGlobal).Select(permission => permission.Name)];

    private static readonly Dictionary<string, PermissionDescriptor> ByName =
        All.ToDictionary(permission => permission.Name, StringComparer.Ordinal);

    public static bool IsKnown(string? name) => name is not null && ByName.ContainsKey(name);

    public static bool IsGlobalPermission(string name) => ByName.TryGetValue(name, out var found) && found.IsGlobal;

    /// <summary>
    /// The view permission of the same area, so that <c>Edit</c> can imply <c>View</c> in one place.
    /// Returns null when the name has no matching view permission.
    /// </summary>
    public static string? ViewOf(string name)
    {
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
        {
            return null;
        }

        var view = string.Concat(name.AsSpan(0, dot), ".View");
        return IsKnown(view) ? view : null;
    }
}
