using IvaoHub.Core.Auth;
using Microsoft.AspNetCore.Authentication;

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

        // A POST, and with the header the CSRF middleware demands: signing somebody out from a
        // third party page is a small nuisance, but it is still an action taken in their name.
        group.MapPost("/logout", async (HttpContext context, IvaoUserTokenStore tokens, ICurrentUser user) =>
        {
            if (user.IsAuthenticated)
            {
                // The stored IVAO tokens belong to the session that is ending.
                await tokens.DeleteAsync(user.Vid, context.RequestAborted);
            }

            await context.SignOutAsync(HubClaims.CookieScheme);
            return Results.NoContent();
        });

        // /auth/callback is handled by the OpenID Connect handler itself, through CallbackPath.
    }
}
