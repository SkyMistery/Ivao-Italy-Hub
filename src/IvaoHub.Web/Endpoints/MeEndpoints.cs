using IvaoHub.Core.Auth;
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
        app.MapGet("/api/me", (ICurrentUser user, IOptions<DivisionOptions> division, BuildInfo build) =>
        {
            var options = division.Value;

            return Results.Ok(new BootstrapResponse(
                User: user.IsAuthenticated
                    ? new BootstrapUser(
                        user.Vid,
                        user.FirstName,
                        user.LastName,
                        user.Positions,
                        user.IsStaff,
                        user.IsSuperadmin,
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
                Registries: new BootstrapRegistries([], []),
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

internal sealed record BootstrapRegistries(IReadOnlyList<string> Blocks, IReadOnlyList<string> Widgets);
