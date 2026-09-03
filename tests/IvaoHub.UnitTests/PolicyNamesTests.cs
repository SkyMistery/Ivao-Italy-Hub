using System.Reflection;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Modules.Atc;
using Microsoft.AspNetCore.Authorization;
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
    private static readonly HubPolicyProvider Provider = new(Options.Create(new AuthorizationOptions()));

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

    [Fact]
    public async Task APolicyThatLooksLikeAPermissionButIsNotInTheCatalogueIsARefusal()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Provider.GetPolicyAsync("Links.Invent"));
    }

    [Fact]
    public async Task EveryPolicyTheCodeAsksForExistsInTheCatalogue()
    {
        var used = new[] { typeof(HubDbContext).Assembly, typeof(AtcModule).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(type.GetMethods().SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>())))
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Distinct()
            .ToArray();

        Assert.All(used, policy => Assert.True(
            CorePermissions.IsKnown(policy),
            $"The policy '{policy}' is used but is not in the catalogue."));
    }

    [Fact]
    public void EveryDepartmentalAreaDeclaresBothViewAndEdit()
    {
        var areas = CorePermissions.Departmental
            .Select(name => name[..name.IndexOf('.', StringComparison.Ordinal)])
            .Distinct();

        foreach (var area in areas)
        {
            Assert.True(CorePermissions.IsKnown($"{area}.View"), $"{area} has no View permission.");
            Assert.True(CorePermissions.IsKnown($"{area}.Edit"), $"{area} has no Edit permission.");
        }
    }
}
