using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// What the core does with the modules it is handed: composes their menus, their path exclusions,
/// their blocks and their permissions, and leaves out the ones a division switched off
/// (design M0 section 6.1).
/// <para>The real module of M0, <c>atc</c>, contributes a menu entry and four exclusions and
/// nothing else, which is exactly what it was written to prove. The rest of the contract is
/// exercised here with modules invented for the purpose: a mechanism that only ever sees one
/// implementation is a mechanism nobody has checked is general.</para>
/// </summary>
public sealed class ModuleCompositionTests
{
    [Fact]
    public void AnOptionalModuleTheDivisionSwitchedOffContributesNothing()
    {
        var registry = Compose(
            new Dictionary<string, bool> { ["optional"] = false },
            new TestModule("required"),
            new TestModule("optional", isOptional: true));

        Assert.Equal(["required", "optional"], registry.Keys);
        Assert.Equal(["required"], registry.EnabledKeys);

        // Not merely absent from the menu: absent from everything, so a page naming one of its
        // blocks is refused exactly as it would be on an installation that never had the module.
        Assert.Single(registry.PublicNavigation);
        Assert.Equal("nav.required", registry.PublicNavigation[0].Key);
        Assert.Null(registry.Find("optional"));
        Assert.NotNull(registry.Find("required"));
    }

    [Fact]
    public void AnOptionalModuleTheDivisionSaysNothingAboutStaysOn()
    {
        // A release that adds a module must work without every division editing its configuration
        // first: silence means yes, and only an explicit false switches one off.
        var registry = Compose(NoSwitches, new TestModule("optional", isOptional: true));

        Assert.Equal(["optional"], registry.EnabledKeys);
    }

    [Fact]
    public void ADivisionCannotSwitchOffAModuleThatIsNotOptional()
    {
        var registry = Compose(
            new Dictionary<string, bool> { ["required"] = false },
            new TestModule("required"));

        Assert.Equal(["required"], registry.EnabledKeys);
    }

    [Fact]
    public void TheExclusionsOfEveryModuleAreComposedAndDeduplicated()
    {
        var registry = Compose(
            NoSwitches,
            new TestModule("one", exclusions: ["/legacy", "/shared"]),
            new TestModule("two", exclusions: ["/shared", "/other"]));

        Assert.Equal(["/legacy", "/shared", "/other"], registry.SpaFallbackExclusions);
    }

    [Fact]
    public void TwoModulesAnsweringToOneKeyIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() =>
            Compose(NoSwitches, new TestModule("same"), new TestModule("same")));

        Assert.Contains("same", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/atc", "atc")]
    [InlineData("/api/atc/ping", "atc")]
    [InlineData("/API/ATC/ping", "atc")]
    [InlineData("/api/atcetera/ping", null)]
    [InlineData("/api/links", null)]
    [InlineData("/health", null)]
    public void TheModuleOfARequestIsReadFromItsPath(string path, string? expected)
    {
        var registry = Compose(NoSwitches, new TestModule("atc"));

        Assert.Equal(expected, registry.ForApiPath(path)?.Key);
    }

    // --- the permission catalogue --------------------------------------------------------------

    [Fact]
    public async Task APermissionAModuleDeclaresBecomesAPolicyLikeAnyOther()
    {
        var catalogue = new PermissionCatalog(
        [
            .. CorePermissions.All,
            new PermissionDescriptor("Roster.View", IsGlobal: false),
            new PermissionDescriptor("Roster.Edit", IsGlobal: false),
        ]);

        var provider = new HubPolicyProvider(Options.Create(new AuthorizationOptions()), catalogue);

        var policy = await provider.GetPolicyAsync("Roster.Edit");
        Assert.NotNull(policy);
        Assert.Equal("Roster.Edit", Assert.Single(policy.Requirements.OfType<PermissionRequirement>()).Permission);

        // And the rule that makes a mistake loud rather than silent still holds for a name nobody
        // declared: a policy that quietly does not exist would deny everybody.
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetPolicyAsync("Roster.Invent"));
    }

    [Fact]
    public void TheCatalogueRefusesTwoDeclarationsOfOneName()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => new PermissionCatalog(
        [
            .. CorePermissions.All,
            new PermissionDescriptor(CorePermissions.LinksEdit, IsGlobal: false),
        ]));

        Assert.Contains(CorePermissions.LinksEdit, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPermissionOfTheCoreIsInTheCatalogueOfAHubWithNoModules()
    {
        Assert.Equal(
            CorePermissions.All.Select(permission => permission.Name).Order(StringComparer.Ordinal),
            PermissionCatalog.Core.All.Select(permission => permission.Name).Order(StringComparer.Ordinal));
    }

    // --- the widget registry -------------------------------------------------------------------

    [Fact]
    public void TheWidgetRegistryIsComposedAndRefusesADuplicate()
    {
        var registry = new WidgetRegistry(
        [
            .. CoreWidgets.All,
            new WidgetDescriptor("atc.online", Department.AOD, "widgets.atc.online.title", ["half"]),
        ]);

        Assert.Equal(["atc.online", CoreWidgets.Welcome], registry.All.Select(widget => widget.Key));

        Assert.Throws<InvalidOperationException>(() => new WidgetRegistry(
            [.. CoreWidgets.All, .. CoreWidgets.All]));
    }

    /// <summary>A division that says nothing about any module, which is the ordinary case.</summary>
    private static Dictionary<string, bool> NoSwitches => [];

    private static ModuleRegistry Compose(Dictionary<string, bool> switches, params IModule[] modules)
    {
        var division = Options.Create(new DivisionOptions
        {
            Modules = new Dictionary<string, bool>(switches),
        });

        // The registry only reaches for a scope when it is asked whether a module is closed for
        // maintenance, which is a question about the database and not about composition.
        return new ModuleRegistry(
            modules,
            division,
            new EmptyServiceProvider(),
            new MemoryCache(new MemoryCacheOptions()));
    }

    /// <summary>A module invented for a test: a key, and whatever the case under test needs.</summary>
    private sealed class TestModule(
        string key,
        bool isOptional = false,
        IReadOnlyList<string>? exclusions = null) : ModuleBase
    {
        public override string Key => key;

        public override bool IsOptional => isOptional;

        public override IReadOnlyList<string> SpaFallbackExclusions => exclusions ?? [];

        public override IReadOnlyList<NavItemDescriptor> PublicNavigation =>
            [new NavItemDescriptor($"nav.{key}", $"/{key}")];
    }

    /// <summary>Enough of a scope factory to construct the registry, and nothing more.</summary>
    private sealed class EmptyServiceProvider : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        public IServiceProvider ServiceProvider => this;

        public IServiceScope CreateScope() => this;

        public object? GetService(Type serviceType) => null;

        public void Dispose()
        {
            // Nothing to release: this scope never resolved anything.
        }
    }
}
