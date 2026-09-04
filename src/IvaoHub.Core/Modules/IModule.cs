using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Modules;

/// <summary>
/// One entry of a menu, as the server declares it: a translation key and a path, never a phrase.
/// The server does not know which language the browser is showing (plan section 16.7).
/// </summary>
/// <param name="Key">Translation key, for example <c>nav.atc</c>.</param>
/// <param name="Path">Where it goes, inside this application.</param>
/// <param name="Permission">
/// The permission the entry is behind, or null when anybody may follow it. A menu entry that leads
/// to a 403 is a menu entry that teaches people to ignore the menu.
/// </param>
public sealed record NavItemDescriptor(string Key, string Path, string? Permission = null);

/// <summary>
/// A tile of a dashboard. The core registers <c>welcome</c>; a module registers its own and the
/// dashboard composes whatever it is handed, so no screen holds a list of tiles (design M0 section 6.3).
/// </summary>
/// <param name="Key">Identifier, matched by the client to the component that draws it.</param>
/// <param name="Department">The department the tile is about, or null when it is about everyone.</param>
/// <param name="TitleKey">Translation key of its heading.</param>
/// <param name="Sizes">The widths it can be drawn at, as the dashboard understands them.</param>
public sealed record WidgetDescriptor(
    string Key,
    Department? Department,
    string TitleKey,
    IReadOnlyList<string> Sizes);

/// <summary>
/// What a module is, and the whole of what the core knows about one (design M0 section 6.1).
/// <para>A module is <b>not</b> a plugin loaded at runtime: it is added to the monorepo and the
/// application is recompiled. The boundary is drawn as if it were one anyway, at no cost, so that
/// the day it has to become one the perimeter to extract is already there.</para>
/// <para>A module references <c>IvaoHub.Core</c> and nothing else — never another module — and the
/// core never references a module: <c>IvaoHub.Web/Modules.cs</c> holds the one explicit list, and
/// an architecture test fails if either rule is broken (design M0 section 6.2).</para>
/// </summary>
public interface IModule
{
    /// <summary>
    /// The key: <c>atc</c>, <c>events</c>. It is the prefix of the module's endpoints
    /// (<c>/api/{key}</c>), the name of its migration history table, and the key an optional module
    /// is switched off by in <c>division.modules</c>.
    /// </summary>
    string Key { get; }

    /// <summary>The department the module belongs to, or null when it belongs to none.</summary>
    Department? Department { get; }

    /// <summary>
    /// True when a division may switch it off in <c>division.json</c>. The four department modules
    /// and the editorial core are not optional.
    /// </summary>
    bool IsOptional { get; }

    /// <summary>
    /// The permissions the module adds to the catalogue. They become policies exactly like the
    /// ones of the core; nobody ever adds an authorization handler (plan section 16.2).
    /// </summary>
    IReadOnlyList<PermissionDescriptor> Permissions { get; }

    /// <summary>What the module adds to the public menu.</summary>
    IReadOnlyList<NavItemDescriptor> PublicNavigation { get; }

    /// <summary>What the module adds to the back office menu.</summary>
    IReadOnlyList<NavItemDescriptor> StaffNavigation { get; }

    /// <summary>
    /// The blocks the module contributes to the one block registry. They are registered in the
    /// container like the core's own, so nothing has to learn their names.
    /// </summary>
    IReadOnlyList<BlockDescriptor> Blocks { get; }

    /// <summary>The dashboard tiles the module contributes.</summary>
    IReadOnlyList<WidgetDescriptor> Widgets { get; }

    /// <summary>
    /// Path prefixes the single page application must not answer for. A module that puts something
    /// else behind the same host — a legacy service, a static bundle — says so here, and the
    /// fallback stops swallowing those addresses (design M0 section 6.4).
    /// </summary>
    IReadOnlyList<string> SpaFallbackExclusions { get; }

    /// <summary>
    /// Its own services: a database context through <c>AddModuleDbContext&lt;T&gt;</c>, its data
    /// block providers, its jobs. Never a second interceptor, a second handler or a second client
    /// for the IVAO API.
    /// </summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Its endpoints, which live under <c>/api/{Key}</c> and nowhere else.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// <summary>Its context types, so that the start up sequence migrates them like the core's.</summary>
    IEnumerable<Type> DbContextTypes { get; }
}

/// <summary>
/// What a module has to say only when it has something to say. Everything here is empty by
/// default: a module that brings one endpoint and a menu entry writes two members, not eleven.
/// </summary>
public abstract class ModuleBase : IModule
{
    public abstract string Key { get; }

    public virtual Department? Department => null;

    public virtual bool IsOptional => false;

    public virtual IReadOnlyList<PermissionDescriptor> Permissions => [];

    public virtual IReadOnlyList<NavItemDescriptor> PublicNavigation => [];

    public virtual IReadOnlyList<NavItemDescriptor> StaffNavigation => [];

    public virtual IReadOnlyList<BlockDescriptor> Blocks => [];

    public virtual IReadOnlyList<WidgetDescriptor> Widgets => [];

    public virtual IReadOnlyList<string> SpaFallbackExclusions => [];

    public virtual IEnumerable<Type> DbContextTypes => [];

    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Most modules have nothing to add: the core already registers everything that is shared.
    }

    public virtual void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // A module with no API of its own is a legitimate module: it may contribute only blocks.
    }
}
