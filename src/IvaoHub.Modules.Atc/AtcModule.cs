using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Modules.Atc;

/// <summary>
/// The ATC operations module. In M0 it is deliberately almost empty: no table, no screen of its
/// own beyond a placeholder page, one endpoint. What it exists to prove is the mechanism — that the
/// core composes a module's menu, its path exclusions and its endpoints without knowing its name,
/// and that maintenance closes it for writing (design M0 section 6.4).
/// <para>Everything a division actually does with ATC operations — the roster, the bookings, the
/// live positions — is M3 and later. This class is the seam those will hang from.</para>
/// </summary>
public sealed class AtcModule : ModuleBase
{
    /// <summary>Module key, used in <c>division.modules</c> and as the <c>/api/{key}</c> prefix.</summary>
    public const string ModuleKey = "atc";

    public override string Key => ModuleKey;

    public override Department? Department => IvaoHub.Core.Division.Department.AOD;

    /// <summary>A department module: a division cannot switch ATC operations off.</summary>
    public override bool IsOptional => false;

    public override IReadOnlyList<NavItemDescriptor> PublicNavigation =>
        [new NavItemDescriptor("nav.atc", "/atc")];

    /// <summary>
    /// What lives behind the same host and is not this application. vIPI, which the hub replaces
    /// department by department, still answers for these while the migration lasts: the single page
    /// application must hand them back to the server rather than draw its own "page not found"
    /// over something that exists (design M0 section 6.4).
    /// </summary>
    public override IReadOnlyList<string> SpaFallbackExclusions =>
        ["/services/vsop", "/vsop", "/_content", "/_framework"];

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup($"/api/{ModuleKey}").WithTags("Atc");

        // Anonymous, and it says only that the module is mapped. It is what the composition test
        // reaches for, and what an operator curls to tell "the module is not installed" from
        // "the module is closed for maintenance" -- the second answers 503 to a write and this to
        // a read.
        group.MapGet("/ping", () => TypedResults.Ok(new AtcPing(ModuleKey)))
            .WithName("AtcPing");
    }
}

/// <summary>What <c>GET /api/atc/ping</c> answers. Typed, so that it reaches the OpenAPI document
/// and from there the generated client, like every other response of the hub.</summary>
public sealed record AtcPing(string Module);
