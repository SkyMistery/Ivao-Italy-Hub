using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth.Permissions;

/// <summary>
/// "Hold this permission." The only requirement of the hub: everything else, above all the
/// department a resource belongs to, is decided by the single handler.
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;

    public override string ToString() => Permission;
}

/// <summary>
/// Turns every permission of the catalogue into a policy of the same name, so that an endpoint
/// writes <c>RequireAuthorization(CorePermissions.LinksEdit)</c> and nothing has to be registered
/// by hand (design M0 section 3.7). A name that is not in the catalogue is a mistake, and it is
/// raised as one instead of quietly denying everybody.
/// </summary>
public sealed class HubPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        // A policy declared explicitly wins: the catalogue is the default, not a cage.
        var declared = await _fallback.GetPolicyAsync(policyName);
        if (declared is not null)
        {
            return declared;
        }

        if (CorePermissions.IsKnown(policyName))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
        }

        if (policyName.Contains('.', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{policyName}' looks like a permission but is not in the catalogue. "
                + "Add it to CorePermissions, or to the permissions of the module that needs it.");
        }

        return null;
    }
}

/// <summary>
/// The only authorization handler of the hub. "May this person do that to this row?" is answered
/// here and nowhere else: a module that wanted its own rule would be writing a second answer to a
/// question that already has one (plan section 16.2).
/// </summary>
public sealed class DepartmentAuthorizationHandler(ICurrentUser currentUser, IOptions<DivisionOptions> division)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (IsAllowed(context.Resource, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private bool IsAllowed(object? resource, string permission)
    {
        // Without a resource the question is "may they do this at all": holding the permission on
        // any department, or globally, is enough, and the department is checked row by row later.
        // Denying here would close the list of their own department to every coordinator.
        if (resource is not IOwnedByDepartment owned)
        {
            return currentUser.HasAny(permission);
        }

        if (!currentUser.Has(permission, owned.OwnerDepartment))
        {
            return false;
        }

        // When the division keeps FIR teams to their own FIR, a row that belongs to a FIR is only
        // theirs; whoever reaches every department is above the distinction.
        if (division.Value.FirStaffScope == FirStaffScope.Own
            && resource is IHasFir { Fir: { } fir }
            && !currentUser.HasAllDepartments
            && !currentUser.Firs.Contains(fir))
        {
            return false;
        }

        return true;
    }
}
