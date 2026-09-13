using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A module that exists only in the test host. It took over from the ATC module on 13 September
/// 2026, when that one left the build together with vIPI (note 2026-09-13-staccarsi-da-vipi): the
/// build has no module of its own until events opens M2, and the composition — menu, endpoints,
/// fallback exclusions, maintenance, the catalogue of permissions — still has to be proved against
/// the real host rather than against a registry built by hand.
/// <para>It is added through <see cref="ModuleServiceCollectionExtensions.AddHubModule"/>, the same
/// call the application makes for every module of its list, so what is proved is the application's
/// own path and not a second one written for tests.</para>
/// </summary>
public sealed class SampleModule : ModuleBase
{
    public const string ModuleKey = "sample";

    /// <summary>A permission of the module, so that the catalogue is seen to compose it.</summary>
    public const string ReadPermission = "Sample.Read";

    public const string NavigationKey = "nav.sample";

    public const string NavigationPath = "/sample";

    /// <summary>Something behind the same host that is not the single page application.</summary>
    public const string Exclusion = "/sample-legacy";

    public override string Key => ModuleKey;

    public override IReadOnlyList<PermissionDescriptor> Permissions =>
        [new PermissionDescriptor(ReadPermission, IsGlobal: true)];

    public override IReadOnlyList<NavItemDescriptor> PublicNavigation =>
        [new NavItemDescriptor(NavigationKey, NavigationPath)];

    public override IReadOnlyList<string> SpaFallbackExclusions => [Exclusion];

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Anonymous, and it says only that the module is mapped: a read that maintenance leaves
        // open, and a POST to the same address that maintenance closes.
        endpoints.MapGroup($"/api/{ModuleKey}")
            .MapGet("/ping", () => TypedResults.Ok(new SamplePing(ModuleKey)));
    }
}

public sealed record SamplePing(string Module);
