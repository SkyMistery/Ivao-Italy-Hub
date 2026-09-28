using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth.Permissions;

/// <summary>
/// The policies that are not a permission of the catalogue. There is exactly one, and there is
/// room for exactly one: "be signed in" is not a permission, it is the floor under all of them.
/// </summary>
public static class HubPolicies
{
    /// <summary>Any member with a valid application cookie. Nothing more is asked.</summary>
    public const string SignedIn = "SignedIn";
}

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
/// <remarks>
/// The catalogue is injected rather than read from a static list because what a fork installs is
/// not known at compile time: a module permission has to become a policy exactly like one of the
/// core, or the module's own endpoints would deny everybody.
/// </remarks>
public sealed class HubPolicyProvider(IOptions<AuthorizationOptions> options, PermissionCatalog catalogue, TokenAudienceCatalog audiences)
    : IAuthorizationPolicyProvider
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

        // An audience of personal tokens (M2, T19a): the token scheme, never the cookie.
        if (PersonalTokenAuthenticationExtensions.PolicyFor(policyName, audiences) is { } tokenPolicy)
        {
            return tokenPolicy;
        }

        if (catalogue.IsKnown(policyName))
        {
            return new AuthorizationPolicyBuilder()
                // The application cookie, not the round trip to IVAO. The default challenge scheme
                // is the identity provider, which is right for /auth/login and wrong for an API
                // call: a caller that is not signed in needs 401 and a status code it can act on,
                // never a redirect to a consent screen it cannot render.
                .AddAuthenticationSchemes(HubClaims.CookieScheme)
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
public sealed class DepartmentAuthorizationHandler(
    ICurrentUser currentUser,
    IOptions<DivisionOptions> division,
    PermissionCatalog catalogue)
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
        // First of all, and before anything anybody holds: whoever the row is about does not decide
        // it. A pilot does not validate their own report, and a super administrator who happens to
        // be that pilot does not either — this is the one place the role does not bypass a policy
        // (decision note of 15 September 2026, design M2 section 7.3).
        if (resource is IHasStakeholder { StakeholderVid: { } stakeholder }
            && stakeholder == currentUser.Vid
            && catalogue.IsDeniedToStakeholder(permission))
        {
            return false;
        }

        // Whoever takes part in a row reads it, whichever department owns it: the sender of a thread and whoever was
        // added to it (M2, T14). Only reading — which, for a thread, is also what answers it.
        if (resource is IHasParticipants participating
            && currentUser.IsAuthenticated
            && participating.ParticipantVids.Contains(currentUser.Vid)
            && IsRead(permission))
        {
            return true;
        }

        // Without a resource the question is "may they do this at all": holding the permission on
        // any department, or globally, is enough, and the department is checked row by row later.
        // Denying here would close the list of their own department to every coordinator. A permission
        // that reaches only the rows assigned to the asker answers the same (M3, A3b): there is no row
        // to be assigned yet, and that is what offers an examiner "new exam" and lists it in /api/me.
        if (resource is not IOwnedByDepartment owned)
        {
            return currentUser.HasAny(permission);
        }

        // A row the resource shares for reading is readable by whoever holds the read permission
        // anywhere, whichever department owns it (design M1 section 9.4). Only reading: the check
        // below is what a write still has to pass, and it is untouched.
        if (resource is ISharedForReading { IsSharedForReading: true } && IsRead(permission))
        {
            return currentUser.HasAny(permission);
        }

        // Held on one of the departments of the row is held on the row: a row of one department has
        // one, and a row of a module organised together with others has them all (M2, note
        // 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.3). One rule, not a branch.
        //
        // The scope of the row travels with the question. A permission held without one reaches
        // every row, as it always has; one granted on a single row reaches that row only, which is
        // how "this validator, on this tour" is said (M2, note of 15 September 2026).
        var scope = (resource as IHasResourceScope)?.ResourceScope;
        if (!owned.OwnerDepartments.Any(department => currentUser.Has(permission, department, scope)))
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

        // A permission that reaches only the rows assigned to whoever asks (M3, A3b, note
        // 2026-09-26-le-righe-affidate-a-chi-scrive): on any other row — assigned to somebody else, to
        // nobody, or of an entity that says nothing about it — it is worth what the area's Edit is worth
        // there, the permission the interceptor's guard falls back to as well. An examiner changes their
        // own exams, and whoever edits the area every exam.
        if (catalogue.IsOnlyForAssignee(permission)
            && (resource as IHasAssignee)?.AssigneeVid != currentUser.Vid)
        {
            return catalogue.EditOf(permission) is { } edit
                && !string.Equals(edit, permission, StringComparison.Ordinal)
                && IsAllowed(resource, edit);
        }

        return true;
    }

    /// <summary>
    /// Whether a permission is the one that reads. It is the <c>View</c> of its own area, and the
    /// catalogue is asked rather than the name inspected here: "the view permission of an area" is
    /// already decided in one place, the same one "Edit implies View" is decided in.
    /// </summary>
    private bool IsRead(string permission) =>
        string.Equals(catalogue.ViewOf(permission), permission, StringComparison.Ordinal);
}
