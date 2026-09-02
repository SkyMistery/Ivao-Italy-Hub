using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth.Permissions;

/// <summary>
/// What a staff position is worth. One table, in one file, tested row by row: this is the only
/// place where "who may do what" is decided, and no module ever writes its own version of it
/// (design M0 section 3.7).
/// </summary>
public static class RolePermissionMatrix
{
    /// <summary>
    /// Permissions a position holds on its own department, by seniority.
    /// An advisor edits but does not publish and does not touch templates; a trainer, who is a
    /// member of the training department rather than part of its staff, holds nothing of the core.
    /// </summary>
    private static readonly Dictionary<StaffLevel, string[]> ByLevel = new()
    {
        [StaffLevel.Coordinator] =
        [
            CorePermissions.ContentView,
            CorePermissions.ContentEdit,
            CorePermissions.ContentPublish,
            CorePermissions.ContentManageTemplates,
            CorePermissions.LinksView,
            CorePermissions.LinksEdit,
            CorePermissions.CalendarView,
            CorePermissions.CalendarEdit,
        ],
        [StaffLevel.Assistant] =
        [
            CorePermissions.ContentView,
            CorePermissions.ContentEdit,
            CorePermissions.ContentPublish,
            CorePermissions.ContentManageTemplates,
            CorePermissions.LinksView,
            CorePermissions.LinksEdit,
            CorePermissions.CalendarView,
            CorePermissions.CalendarEdit,
        ],
        [StaffLevel.Advisor] =
        [
            CorePermissions.ContentView,
            CorePermissions.ContentEdit,
            CorePermissions.LinksView,
            CorePermissions.LinksEdit,
            CorePermissions.CalendarView,
            CorePermissions.CalendarEdit,
        ],
        [StaffLevel.Member] = [],
    };

    /// <summary>What this position is worth on the department it belongs to.</summary>
    public static IReadOnlyList<string> OnOwnDepartment(StaffLevel level) =>
        ByLevel.TryGetValue(level, out var permissions) ? permissions : [];

    /// <summary>
    /// The director of the division and the web team reach every department and hold the global
    /// permissions: they are the two roles that have to be able to fix anything.
    /// Their advisors do not: an advisor is an advisor of their own department.
    /// </summary>
    public static bool ReachesEveryDepartment(StaffPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        return position.Role is StaffRole.Director or StaffRole.Web
            && position.Level is StaffLevel.Coordinator or StaffLevel.Assistant;
    }

    /// <summary>
    /// A position of IVAO headquarters reads the content of every department and writes nothing.
    /// </summary>
    public static bool ReadsEveryDepartment(StaffPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return position.Role is StaffRole.HqStaff;
    }

    /// <summary>
    /// Every department, used to turn a permission held everywhere into an explicit list when a
    /// single department has to be taken away from it.
    /// </summary>
    public static readonly IReadOnlyList<Department> AllDepartments = Enum.GetValues<Department>();
}
