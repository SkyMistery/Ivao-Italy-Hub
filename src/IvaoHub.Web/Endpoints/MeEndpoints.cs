using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;

namespace IvaoHub.Web.Endpoints;

/// <summary>
/// The single bootstrap endpoint. Everything the single page application needs in order to draw
/// itself comes from here: nothing about the division, the menus or the permissions is ever
/// hardcoded in the client (plan section 16.7).
/// </summary>
internal static class MeEndpoints
{
    public static void MapMeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me", (
            ICurrentUser user,
            IOptions<DivisionOptions> division,
            BlockRegistry blocks,
            BuildInfo build) =>
        {
            var options = division.Value;

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
                // Composed by the module registry in F8; empty and static until then.
                Modules: [],
                Navigation: new BootstrapNavigation(
                    Public: [new NavItem("nav.home", "/")],
                    Staff: user.IsStaff ? [new NavItem("nav.staff", "/staff")] : []),
                // The blocks the server knows. The client checks that it has a component for each
                // one and warns the staff in the ui-kit when it does not: a page built on a block
                // this browser cannot draw is better said out loud than drawn as a gap.
                Registries: new BootstrapRegistries(
                    [.. blocks.All.Select(block => new BootstrapBlock(
                        block.Type,
                        block.Version,
                        block.Kind,
                        block.AlwaysLive))],
                    []),
                Version: build.Version));
        });
    }
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

internal sealed record BootstrapModule(string Key, string? Department, bool Enabled, bool Maintenance);

internal sealed record BootstrapNavigation(IReadOnlyList<NavItem> Public, IReadOnlyList<NavItem> Staff);

/// <summary><paramref name="Key"/> is a translation key such as <c>nav.home</c>.</summary>
internal sealed record NavItem(string Key, string Path);

internal sealed record BootstrapRegistries(
    IReadOnlyList<BootstrapBlock> Blocks,
    IReadOnlyList<string> Widgets);

/// <summary>
/// One block, as the server declares it. What it looks like and what its properties mean live in
/// TypeScript and nowhere else (CLAUDE.md section 2); this is the envelope side of it.
/// </summary>
internal sealed record BootstrapBlock(string Type, int Version, BlockKind Kind, bool AlwaysLive);
