using System.Globalization;
using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Issues a real application cookie for a VID that already exists in the database, without going
/// to IVAO. It deliberately does not bypass authentication: the cookie it writes is the same one a
/// real login produces, so the cookie middleware, the security stamp check and the permission
/// claims are all exercised for real.
/// </summary>
internal sealed class TestSignInStartupFilter : IStartupFilter
{
    public const string Path = "/test-auth/signin";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => builder =>
    {
        builder.Use(async (context, continuation) =>
        {
            if (!context.Request.Path.StartsWithSegments(Path, StringComparison.Ordinal))
            {
                await continuation();
                return;
            }

            if (!int.TryParse(context.Request.Query["vid"], CultureInfo.InvariantCulture, out var vid))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var sync = context.RequestServices.GetRequiredService<UserSyncService>();
            var signedIn = await sync.LoadAsync(vid, context.RequestAborted);
            if (signedIn is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var division = context.RequestServices.GetRequiredService<IOptions<DivisionOptions>>().Value;

            var identity = HubClaims.BuildIdentity(
                signedIn.User.Vid,
                signedIn.User.FirstName,
                signedIn.User.LastName,
                signedIn.User.Locale ?? division.DefaultLocale,
                signedIn.User.SecurityStamp,
                signedIn.User.IsSuperadmin,
                signedIn.User.IsStaff,
                signedIn.Positions,
                signedIn.Permissions);

            await context.SignInAsync(HubClaims.CookieScheme, new ClaimsPrincipal(identity));
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });

        next(builder);
    };
}
