using System.Security.Claims;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The language a signed in member reads the hub in.
/// <para>Two things remember it, and they are deliberately not the same thing. The browser keeps
/// the <c>hub.lang</c> cookie, which is what the language switcher writes and what an anonymous
/// visitor is left with; the row in <c>hub_users</c> keeps the choice of a member, so that it
/// follows them to another browser. This endpoint owns the second one — the client owns the first,
/// and neither writes the other's (design M0 section 7.6).</para>
/// <para>The application cookie is reissued with the new language, because
/// <see cref="ICurrentUser.Locale"/> is read out of it: without that the titles of the problem
/// details answers would keep arriving in the previous language until the next sign in.</para>
/// </summary>
public static class LocaleEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/me/locale";

    /// <summary>The i18n key the client shows when the division does not speak that language.</summary>
    public const string UnsupportedKey = "errors.locale.unsupported";

    /// <summary>The field the error is reported on, so the form generator can place it.</summary>
    private const string Field = "locale";

    public static IEndpointRouteBuilder MapLocaleEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Pattern, async Task<Results<Ok<LocaleResponse>, ValidationProblem>> (
            HttpContext http,
            LocaleWriteDto body,
            HubDbContext database,
            ICurrentUser currentUser,
            LocaleCatalog catalog,
            IOptions<DivisionOptions> division) =>
        {
            ArgumentNullException.ThrowIfNull(body);

            // One rule decides which languages exist, and it is the same one the login used.
            if (LocalePreference.Spoken(division.Value, body.Locale) is not { } locale)
            {
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        [Field] = [UnsupportedKey],
                    },
                    title: catalog.Resolve(currentUser.Locale, CrudProblems.ValidationTitleKey));
            }

            var user = await database.Users.FirstAsync(row => row.Vid == currentUser.Vid, http.RequestAborted);
            user.Locale = locale;
            await database.SaveChangesAsync(http.RequestAborted);

            if (http.User.Identity is ClaimsIdentity identity)
            {
                await http.SignInAsync(
                    HubClaims.CookieScheme,
                    new ClaimsPrincipal(HubClaims.WithLocale(identity, locale)));
            }

            return TypedResults.Ok(new LocaleResponse(locale));
        })
        .WithName("MeSetLocale")
        .RequireAuthorization(HubPolicies.SignedIn);

        return app;
    }
}

/// <summary>The language a member is choosing, as they spell it: <c>it</c>, or <c>it-IT</c>.</summary>
public sealed record LocaleWriteDto(string Locale);

/// <summary>The language as the division spells it, which is what the client should settle on.</summary>
public sealed record LocaleResponse(string Locale);
