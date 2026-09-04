using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;

namespace IvaoHub.Web.Endpoints;

/// <summary>
/// The single bootstrap endpoint. Everything the single page application needs in order to draw
/// itself comes from here: nothing about the division, the menus, the modules, the permissions or
/// the registries is ever hardcoded in the client (plan section 16.7).
/// </summary>
internal static class MeEndpoints
{
    /// <summary>The home entry, which belongs to the core and is always first.</summary>
    private static readonly NavItem Home = new("nav.home", "/");

    /// <summary>The way into the back office, for whoever has one.</summary>
    private static readonly NavItem Staff = new("nav.staff", "/staff");

    public static void MapMeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me", async (
            ICurrentUser user,
            IOptions<DivisionOptions> division,
            ModuleRegistry modules,
            BlockRegistry blocks,
            WidgetRegistry widgets,
            PermissionCatalog catalogue,
            BuildInfo build,
            CancellationToken cancellationToken) =>
        {
            var options = division.Value;

            var moduleStates = new List<BootstrapModule>(modules.All.Count);
            foreach (var module in modules.All)
            {
                var enabled = modules.EnabledKeys.Contains(module.Key);

                moduleStates.Add(new BootstrapModule(
                    module.Key,
                    module.Department?.ToString(),
                    enabled,
                    enabled && await modules.IsInMaintenanceAsync(module.Key, cancellationToken)));
            }

            // Typed rather than IResult so that the shape reaches the OpenAPI document, and from
            // there the generated client: the payload of the bootstrap is written once.
            return TypedResults.Ok(new BootstrapResponse(
                User: user.IsAuthenticated
                    ? new BootstrapUser(
                        user.Vid,
                        user.FirstName,
                        user.LastName,
                        user.Positions,
                        user.IsStaff,
                        user.IsSuperadmin,
                        user.HasAllDepartments,
                        user.Locale,
                        [.. user.Departments.Select(department => department.ToString())],
                        [.. user.Firs])
                    : null,
                Permissions: [.. user.Permissions.Select(permission =>
                    new BootstrapPermission(permission.Name, permission.Department?.ToString()))],
                Division: new BootstrapDivision(
                    options.Code,
                    options.Name,
                    options.Locales,
                    options.DefaultLocale,
                    options.Timezone,
                    options.FirStaffScope.ToString().ToLowerInvariant()),
                Modules: moduleStates,
                Navigation: new BootstrapNavigation(
                    Public: [Home, .. Visible(modules.PublicNavigation, user)],
                    Staff: user.IsStaff || user.IsSuperadmin
                        ? [Staff, .. Visible(modules.StaffNavigation, user)]
                        : []),
                // What the server knows how to talk about. The client checks it has a component for
                // each one and warns the staff in the ui-kit when it does not: a page built on a
                // block this browser cannot draw is better said out loud than drawn as a gap.
                Registries: new BootstrapRegistries(
                    [.. blocks.All.Select(block => new BootstrapBlock(
                        block.Type,
                        block.Version,
                        block.Kind,
                        block.AlwaysLive))],
                    [.. widgets.All.Select(widget => new BootstrapWidget(
                        widget.Key,
                        widget.Department?.ToString(),
                        widget.TitleKey,
                        widget.Sizes))],
                    [.. catalogue.All.Select(permission =>
                        new BootstrapPermissionName(permission.Name, permission.IsGlobal))]),
                Version: build.Version));
        });
    }

    /// <summary>
    /// The entries this person may actually follow. A menu entry that leads to a 403 is a menu
    /// entry that teaches people to ignore the menu, so the permission a module declares on one is
    /// checked here rather than left to the screen behind it.
    /// </summary>
    private static IEnumerable<NavItem> Visible(IEnumerable<NavItemDescriptor> entries, ICurrentUser user) =>
        entries
            .Where(entry => entry.Permission is null || user.HasAny(entry.Permission))
            .Select(entry => new NavItem(entry.Key, entry.Path));
}

/// <summary>
/// The bootstrap payload. Menu entries are translation keys, never text: the server does not know
/// which language the browser is showing.
/// </summary>
internal sealed record BootstrapResponse(
    BootstrapUser? User,
    IReadOnlyList<BootstrapPermission> Permissions,
    BootstrapDivision Division,
    IReadOnlyList<BootstrapModule> Modules,
    BootstrapNavigation Navigation,
    BootstrapRegistries Registries,
    string Version);

internal sealed record BootstrapUser(
    int Vid,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Positions,
    bool IsStaff,
    bool IsSuperadmin,
    // The director, the web team and a super administrator reach every department, so the staff
    // sidebar has to list all of them rather than the ones the positions name. It is stated here
    // because the client must not guess it from the shape of the permission list, for the same
    // reason the server does not (design M0 section 3.3).
    bool HasAllDepartments,
    string Locale,
    IReadOnlyList<string> Departments,
    IReadOnlyList<string> Firs);

/// <summary>A department of null means the permission is held on every department.</summary>
internal sealed record BootstrapPermission(string Name, string? Department);

internal sealed record BootstrapDivision(
    string Code,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyList<string> Locales,
    string DefaultLocale,
    string Timezone,
    string FirStaffScope);

/// <summary>
/// One module of this build. <paramref name="Enabled"/> is false for an optional module the
/// division switched off: it is compiled in and silent, and saying so is what lets the
/// administration screen show it as something that can be switched back on.
/// </summary>
internal sealed record BootstrapModule(string Key, string? Department, bool Enabled, bool Maintenance);

internal sealed record BootstrapNavigation(IReadOnlyList<NavItem> Public, IReadOnlyList<NavItem> Staff);

/// <summary><paramref name="Key"/> is a translation key such as <c>nav.home</c>.</summary>
internal sealed record NavItem(string Key, string Path);

internal sealed record BootstrapRegistries(
    IReadOnlyList<BootstrapBlock> Blocks,
    IReadOnlyList<BootstrapWidget> Widgets,
    IReadOnlyList<BootstrapPermissionName> Permissions);

/// <summary>
/// One permission of the catalogue: core plus whatever the installed modules declare. It is here so
/// that the screen which hands a permission out can offer the ones that exist rather than a text
/// box — the set is not knowable at compile time, because it depends on which modules were built in.
/// <para>Not sensitive: the catalogue is in the source of every fork. What is sensitive is who
/// holds what, and that is <c>Permissions</c> above, which only ever describes the caller.</para>
/// </summary>
internal sealed record BootstrapPermissionName(string Name, bool IsGlobal);

/// <summary>
/// One block, as the server declares it. What it looks like and what its properties mean live in
/// TypeScript and nowhere else (CLAUDE.md section 2); this is the envelope side of it.
/// </summary>
internal sealed record BootstrapBlock(string Type, int Version, BlockKind Kind, bool AlwaysLive);

/// <summary>One dashboard tile, on the same terms as a block: the envelope, never the drawing.</summary>
internal sealed record BootstrapWidget(
    string Key,
    string? Department,
    string TitleKey,
    IReadOnlyList<string> Sizes);
