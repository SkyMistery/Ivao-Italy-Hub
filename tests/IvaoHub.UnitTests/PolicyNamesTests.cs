using System.Reflection;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// A policy is a permission of the catalogue, and nothing else. A name nobody declared has to
/// fail loudly at start up: a policy that silently does not exist would deny everybody, which is
/// the kind of bug that only shows up on the day somebody needs the screen.
/// </summary>
public sealed class PolicyNamesTests
{
    private static readonly HubPolicyProvider Provider =
        new(Options.Create(new AuthorizationOptions()), PermissionCatalog.Core, new TokenAudienceCatalog(
            [("roster", [new TokenAudienceDescriptor("roster.agent", CorePermissions.LinksEdit)])]));

    [Fact]
    public async Task EveryPermissionOfTheCatalogueIsAPolicy()
    {
        foreach (var permission in CorePermissions.All)
        {
            var policy = await Provider.GetPolicyAsync(permission.Name);

            Assert.NotNull(policy);
            var requirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
            Assert.Equal(permission.Name, requirement.Permission);
        }
    }

    /// <summary>
    /// The policy of a token's audience (T19a): the token scheme and only that one, the audience's claim, and the permission
    /// the audience needs held somewhere. An audience nobody declared is as loud as a permission nobody declared.
    /// </summary>
    [Fact]
    public async Task AnAudienceIsAPolicyOfTheTokenSchemeAlone()
    {
        var policy = await Provider.GetPolicyAsync(PersonalTokenPolicy.For("roster.agent"));

        Assert.NotNull(policy);
        Assert.Equal([HubClaims.TokenScheme], policy.AuthenticationSchemes);
        Assert.Equal(CorePermissions.LinksEdit, Assert.Single(policy.Requirements.OfType<PermissionRequirement>()).Permission);
        Assert.Contains(policy.Requirements.OfType<ClaimsAuthorizationRequirement>(), requirement =>
            requirement.ClaimType == HubClaims.Audience && requirement.AllowedValues!.SequenceEqual(["roster.agent"]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Provider.GetPolicyAsync(PersonalTokenPolicy.For("roster.invented")));
    }

    [Fact]
    public void AnAudienceIsNamedAfterItsModule()
    {
        Assert.Throws<InvalidOperationException>(() => new TokenAudienceCatalog(
            [("roster", [new TokenAudienceDescriptor("agent", CorePermissions.LinksEdit)])]));
        Assert.Throws<InvalidOperationException>(() => new TokenAudienceCatalog(
            [("roster", [new TokenAudienceDescriptor("roster.agent", CorePermissions.LinksEdit), new TokenAudienceDescriptor("roster.agent", CorePermissions.LinksEdit)])]));
    }

    [Fact]
    public async Task APolicyThatLooksLikeAPermissionButIsNotInTheCatalogueIsARefusal()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Provider.GetPolicyAsync("Links.Invent"));
    }

    [Fact]
    public async Task EveryPolicyTheCodeAsksForExistsInTheCatalogue()
    {
        var used = new[] { typeof(HubDbContext).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(type.GetMethods().SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>())))
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Distinct()
            .ToArray();

        Assert.All(used, policy => Assert.True(
            PermissionCatalog.Core.IsKnown(policy),
            $"The policy '{policy}' is used but is not in the catalogue."));
    }

    [Fact]
    public void EveryDepartmentalAreaDeclaresBothViewAndEdit()
    {
        var areas = PermissionCatalog.Core.Departmental
            .Select(name => name[..name.IndexOf('.', StringComparison.Ordinal)])
            .Distinct();

        foreach (var area in areas)
        {
            Assert.True(PermissionCatalog.Core.IsKnown($"{area}.View"), $"{area} has no View permission.");
            Assert.True(PermissionCatalog.Core.IsKnown($"{area}.Edit"), $"{area} has no Edit permission.");
        }
    }
}
