using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Web.Endpoints;

/// <summary>
/// The three endpoints of the login round trip. They are Kestrel endpoints, not routes of the
/// single page application, which is why they are excluded from the SPA fallback.
/// </summary>
internal static class AuthEndpoints
{
    /// <summary>Name of the rate limiting policy that protects the login from being hammered.</summary>
    public const string RateLimitPolicy = "auth";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").RequireRateLimiting(RateLimitPolicy);

        // Starts the IVAO round trip. The return URL is treated as if a stranger had written it,
        // because one might have.
        group.MapGet("/login", (string? returnUrl) => Results.Challenge(
            new AuthenticationProperties { RedirectUri = IvaoAuthenticationExtensions.SafeReturnUrl(returnUrl) },
            [HubClaims.IvaoScheme]));

        // Starts the round trip that links a member's Discord account. A link and not a form: `form-action 'self'` refuses a
        // form whose redirect ends on another site. Another site linking here costs nothing: the account linked is the one the
        // member themselves authorises, and only on the VID that started the round trip, if the cookie still says that VID
        // when it comes back (note 2026-11-05-il-collegamento-con-discord §5).
        group.MapGet("/discord", (ICurrentUser user, IOptions<DiscordOAuthOptions> discord) =>
        {
            if (!discord.Value.IsConfigured)
            {
                return Results.NotFound();
            }

            return user.IsAuthenticated
                ? Results.Challenge(DiscordLinkAuthentication.StartedBy(user.Vid), [HubClaims.DiscordScheme])
                : Results.Redirect($"/auth/login?returnUrl={Uri.EscapeDataString("/me")}");
        });

        // A POST, and with the header the CSRF middleware demands: signing somebody out from a
        // third party page is a small nuisance, but it is still an action taken in their name.
        group.MapPost("/logout", async (
            HttpContext context,
            IvaoUserTokenStore tokens,
            HubDbContext database,
            ISecurityStampCache stamps,
            ICurrentUser user) =>
        {
            if (user.IsAuthenticated)
            {
                // The stored IVAO tokens belong to the session that is ending.
                await tokens.DeleteAsync(user.Vid, context.RequestAborted);

                // Logout ends the session everywhere: a new stamp, and every cookie of this person, on this device or
                // another, is refused on its next request. Signing in again is one click through IVAO (Carmine, 21 October
                // 2026).
                var row = await database.Users.FirstOrDefaultAsync(candidate => candidate.Vid == user.Vid, context.RequestAborted);
                if (row is not null)
                {
                    row.SecurityStamp = SuperadminService.NewStamp();
                    await database.SaveChangesAsync(context.RequestAborted);
                    stamps.Invalidate(user.Vid);
                }
            }

            await context.SignOutAsync(HubClaims.CookieScheme);
            return Results.NoContent();
        });

        // /auth/callback is handled by the OpenID Connect handler itself, through CallbackPath.
    }
}
