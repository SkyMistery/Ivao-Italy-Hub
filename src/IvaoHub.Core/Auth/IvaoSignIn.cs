using System.Security.Claims;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>How a completed IVAO round trip ends: the principal of the cookie, or the one word that says why not.</summary>
public sealed record IvaoSignInOutcome(ClaimsPrincipal? Principal, string? Refusal);

/// <summary>
/// The end of the IVAO round trip, where the IVAO identity is exchanged for the application's: the row in
/// <c>hub_users</c> is written, the IVAO tokens are stored, the effective permissions are computed and the compact
/// principal that will live in the cookie is built. The large IVAO claims are dropped.
/// <para>A service of its own rather than the body of the OpenID Connect event, so that what a sign in writes — and that a
/// refused one writes nothing — is tested without an identity provider (note 2026-09-27-l-installazione-di-prova).</para>
/// </summary>
public sealed class IvaoSignIn(
    UserSyncService users,
    IvaoUserTokenStore tokenStore,
    ISecurityStampCache stamps,
    IOptions<DivisionOptions> division,
    IOptions<InstallationOptions> installation,
    ILogger<IvaoSignIn> logger)
{
    /// <summary>IVAO did not say who the person is.</summary>
    public const string NoProfile = "profile";

    /// <summary>A private installation, and the person is neither its staff nor a super administrator.</summary>
    public const string StaffOnly = "staffOnly";

    /// <summary>
    /// Completes the sign in of <paramref name="profile"/>, or refuses it. The refusal codes are words of a closed set,
    /// which the page of a failed login translates.
    /// </summary>
    public async Task<IvaoSignInOutcome> CompleteAsync(
        IvaoUserProfile? profile,
        IvaoUserTokens? tokens,
        CancellationToken cancellationToken = default)
    {
        if (profile is null)
        {
            logger.LogError("The IVAO user info did not contain a VID; the login cannot be completed.");
            return new IvaoSignInOutcome(null, NoProfile);
        }

        // Before a single line is written: a member who may not enter a private installation leaves no row in hub_users,
        // no staff position and no stored token — nothing a later erasure would have to find.
        if (installation.Value.Preview && !await users.IsStaffOrSuperadminAsync(profile, cancellationToken))
        {
            logger.LogInformation(
                "VID {Vid} was turned away: this installation is private and open to its staff only.",
                profile.Vid);

            return new IvaoSignInOutcome(null, StaffOnly);
        }

        var signedIn = await users.UpsertAsync(profile, cancellationToken);

        if (tokens is not null)
        {
            await tokenStore.SaveAsync(profile.Vid, tokens, cancellationToken);
        }

        var identity = HubClaims.BuildIdentity(
            signedIn.User.Vid,
            signedIn.User.FirstName,
            signedIn.User.LastName,
            signedIn.User.Locale ?? division.Value.DefaultLocale,
            signedIn.User.SecurityStamp,
            signedIn.User.IsSuperadmin,
            signedIn.User.IsStaff,
            signedIn.Positions,
            signedIn.Permissions);

        stamps.Invalidate(signedIn.User.Vid);

        if (signedIn.User.IsSuperadmin)
        {
            // Every login taken as a super administrator is worth a line: the role bypasses every
            // policy, so it must never be invisible (plan section 6.3).
            logger.LogWarning("VID {Vid} signed in as a super administrator.", signedIn.User.Vid);
        }

        return new IvaoSignInOutcome(new ClaimsPrincipal(identity), null);
    }
}
