using IvaoHub.Core.Division;

namespace IvaoHub.Core.Content;

/// <summary>
/// Whose the site itself is: its menu, its system templates and the pages an installation is born
/// with all belong to one department, and this is the one place that says which.
/// <para>It is a constant and not configuration, and that is a decision rather than an omission.
/// Every division of this network has a web team; making it a setting would be a knob nobody turns,
/// with a second state — a division that named a department without one — that nothing would test.
/// If a fork ever needs to move it, moving it is this line.</para>
/// <para>The client does not repeat it. It arrives in <c>/api/me</c>, like everything else the
/// single page application needs in order to draw itself (CLAUDE.md §2): the address of the menu
/// screen is then one fact in one place instead of a constant on each side that can drift.</para>
/// </summary>
public static class SiteOwnership
{
    public const Department Department = Division.Department.WD;
}
