using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Web.Endpoints;

/// <summary>
/// The single bootstrap endpoint. Everything the single page application needs in order to draw
/// itself comes from here: nothing about the division, the menus, the modules, the permissions or
/// the registries is ever hardcoded in the client (plan section 16.7).
/// </summary>
internal static class MeEndpoints
{
    /// <summary>
    /// The way into the back office, for whoever has one. It is the last entry the code still
    /// writes: the public menu is a table now, and the home of the site is a row of it like every
    /// other entry (design M1 section 8.1).
    /// </summary>
    private static readonly NavItem Staff = new(Key: "nav.staff", Path: "/staff", Label: null, Children: []);

    public static void MapMeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me", async (
            ICurrentUser user,
            IOptions<DivisionOptions> division,
            ModuleRegistry modules,
            BlockRegistry blocks,
            WidgetRegistry widgets,
            PermissionCatalog catalogue,
            HubDbContext database,
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
                    options.FirStaffScope.ToString().ToLowerInvariant(),
                    SiteOwnership.Department.ToString()),
                Modules: moduleStates,
                Navigation: new BootstrapNavigation(
                    Public: await MenuAsync(database, MenuScope.Public, modules.PublicNavigation, user, cancellationToken),
                    Footer: await MenuAsync(database, MenuScope.Footer, [], user, cancellationToken),
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
    /// One menu of the site: the entries the staff wrote, then the ones the modules registered.
    /// <para>Two kinds of entry meet here and they stay two kinds. An editorial row carries its
    /// <b>text</b>, already translated, because the person who typed it cannot be asked to invent a
    /// translation key; a module row carries a <b>key</b>, because a module cannot know which
    /// language this browser is reading. They are two fields and never one string that is sometimes
    /// a key (design M1 section 8.1).</para>
    /// <para>Which rows come back is not decided here: the global query filter has already dropped
    /// the ones this reader may not see, which is how an entry pointing at a members' page stays
    /// off a visitor's screen without a line of its own.</para>
    /// <para>Depth is one. A row naming a parent hangs under it; a row naming a parent that is not
    /// in this scope has nowhere to hang and is left at the top rather than dropped, because a menu
    /// entry disappearing without a trace is the worse of the two failures.</para>
    /// </summary>
    private static async Task<IReadOnlyList<NavItem>> MenuAsync(
        HubDbContext database,
        MenuScope scope,
        IEnumerable<NavItemDescriptor> fromModules,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var rows = await database.MenuItems
            .AsNoTracking()
            .Where(item => item.Scope == scope && item.IsActive)
            .OrderBy(item => item.Sort)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var present = rows.Select(row => row.Id).ToHashSet();

        var children = rows
            .Where(row => row.ParentId is { } parent && present.Contains(parent))
            .GroupBy(row => row.ParentId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<MenuItem>)[.. group]);

        var editorial = rows
            .Where(row => row.ParentId is not { } parent || !present.Contains(parent))
            .Select(row => Editorial(row, children.GetValueOrDefault(row.Id, [])))
            .ToList();

        // A module entry whose address the staff has already put in the menu would appear twice,
        // and the editorial one wins: it is the one somebody chose the wording and the place of.
        var taken = editorial.Select(entry => entry.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. editorial, .. Visible(fromModules, user).Where(entry => !taken.Contains(entry.Path))];
    }

    private static NavItem Editorial(MenuItem row, IReadOnlyList<MenuItem> children) => new(
        Key: null,
        Path: row.Path,
        Label: row.Label,
        Children: [.. children.Select(child => Editorial(child, []))]);

    /// <summary>
    /// The entries this person may actually follow. A menu entry that leads to a 403 is a menu
    /// entry that teaches people to ignore the menu, so the permission a module declares on one is
    /// checked here rather than left to the screen behind it.
    /// </summary>
    private static IEnumerable<NavItem> Visible(IEnumerable<NavItemDescriptor> entries, ICurrentUser user) =>
        entries
            .Where(entry => entry.Permission is null || user.HasAny(entry.Permission))
            .Select(entry => new NavItem(entry.Key, entry.Path, Label: null, Children: []));
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

/// <summary>
/// <paramref name="SiteDepartment"/> is the department the site itself belongs to: its menu, its
/// system templates and the pages the installation was born with. The client needs it in order to
/// know where the menu screen lives, and it arrives here rather than being written into the client,
/// which is the whole rule of this endpoint (CLAUDE.md §2).
/// </summary>
internal sealed record BootstrapDivision(
    string Code,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyList<string> Locales,
    string DefaultLocale,
    string Timezone,
    string FirStaffScope,
    string SiteDepartment);

/// <summary>
/// One module of this build. <paramref name="Enabled"/> is false for an optional module the
/// division switched off: it is compiled in and silent, and saying so is what lets the
/// administration screen show it as something that can be switched back on.
/// </summary>
internal sealed record BootstrapModule(string Key, string? Department, bool Enabled, bool Maintenance);

internal sealed record BootstrapNavigation(
    IReadOnlyList<NavItem> Public,
    IReadOnlyList<NavItem> Footer,
    IReadOnlyList<NavItem> Staff);

/// <summary>
/// One entry of a menu. Exactly one of the two names is set: <paramref name="Key"/> is a
/// translation key such as <c>nav.staff</c>, which is what a module registers because it cannot
/// know the language of the browser; <paramref name="Label"/> is the text itself in every language
/// of the division, which is what an editorial row carries because the person who wrote it typed
/// words and not a key.
/// </summary>
/// <param name="Key">Translation key of a module's entry, null for an editorial one.</param>
/// <param name="Path">Where the entry leads: a path of this site, or an address of somewhere else.</param>
/// <param name="Label">The text of an editorial entry, null for a module's one.</param>
/// <param name="Children">Sub entries, one level deep and never more.</param>
internal sealed record NavItem(
    string? Key,
    string Path,
    Localized<string>? Label,
    IReadOnlyList<NavItem> Children);

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
