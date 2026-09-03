using System.Globalization;
using System.Security.Claims;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The back end for front end: the browser only ever holds the application cookie, the IVAO tokens
/// stay on the server, and the single page application never sees a token (plan section 6.1).
/// <para>The settings below are inherited from the vIPI implementation, which has been running
/// against the IVAO identity provider in production: they are measurements, not guesses.</para>
/// </summary>
public static class IvaoAuthenticationExtensions
{
    /// <summary>Where a login that could not be completed sends the browser. A translated SPA route.</summary>
    public const string LoginErrorPath = "/login-error";

    /// <summary>Carries the tokens from the token exchange to the sign in, without touching the cookie.</summary>
    private const string TokensItemKey = "ivao.tokens";

    private const string ProfileItemKey = "ivao.profile";

    public static IServiceCollection AddIvaoAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<UserSyncService>();
        services.AddScoped<SuperadminService>();
        services.AddScoped<IvaoUserTokenStore>();
        // Scoped, because it reads through the request context; the cache behind it is a singleton.
        services.AddScoped<ISecurityStampCache, SecurityStampCache>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = HubClaims.CookieScheme;
                options.DefaultChallengeScheme = HubClaims.IvaoScheme;
            })
            .AddCookie(HubClaims.CookieScheme, ConfigureCookie)
            .AddOpenIdConnect(HubClaims.IvaoScheme, "IVAO Single Sign-On", _ => { });

        // The OpenID Connect options need the OAuth client and the environment, so they are
        // configured through the options pipeline rather than in the AddOpenIdConnect callback.
        services.AddOptions<OpenIdConnectOptions>(HubClaims.IvaoScheme)
            .Configure<IOptions<IvaoOAuthOptions>>((options, ivao) => ConfigureOpenIdConnect(options, ivao.Value));

        // The one setting of the application cookie that needs the OAuth client to be decided.
        services.AddOptions<CookieAuthenticationOptions>(HubClaims.CookieScheme)
            .Configure<IOptions<IvaoOAuthOptions>>((options, ivao) =>
                options.Cookie.SecurePolicy = SecurePolicyFor(ivao.Value));

        AddHubAuthorization(services);
        return services;
    }

    /// <summary>
    /// Every permission of the catalogue becomes a policy, and one handler answers all of them.
    /// It is registered here, next to the authentication, so that a host cannot come up with the
    /// identity in place and the authorization missing.
    /// </summary>
    private static void AddHubAuthorization(IServiceCollection services)
    {
        services.AddAuthorization();
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, HubPolicyProvider>());
        services.AddScoped<IAuthorizationHandler, DepartmentAuthorizationHandler>();
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = "hub.auth";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;

        // Written out rather than inherited: a default is something that changes with the version of
        // the framework, and this cookie is the only credential the site issues.
        options.Cookie.HttpOnly = true;

        // Secure is the third of the three and is decided next to the other two, not left to the
        // default: it depends on the scheme of the callback, so the value itself is set through the
        // options pipeline in AddIvaoAuthentication. Anything but "Always" outside development is a
        // credential that a downgraded hop can read.
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        // Lax and not Strict: coming back from IVAO is a cross site navigation, and Strict would not
        // send the cookie on the first hop after the login.
        options.Cookie.SameSite = SameSiteMode.Lax;

        // Never redirect an API call to a login page: the SPA needs the status code.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };

        // The stamp says whether the cookie still reflects the truth. It changes when grants or the
        // super administrator flag change, so a revoked permission bites on the next request rather
        // than in twelve hours.
        options.Events.OnValidatePrincipal = async context =>
        {
            var stampInCookie = context.Principal?.FindFirstValue(HubClaims.SecurityStamp);
            var vidClaim = context.Principal?.FindFirstValue(HubClaims.Vid);

            if (stampInCookie is null || !int.TryParse(vidClaim, CultureInfo.InvariantCulture, out var vid))
            {
                // Sign out as well, exactly as the branch below does: rejecting alone leaves the
                // unusable cookie in the browser, and it comes back on every single request.
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(HubClaims.CookieScheme);
                return;
            }

            var cache = context.HttpContext.RequestServices.GetRequiredService<ISecurityStampCache>();
            var current = await cache.GetAsync(vid, context.HttpContext.RequestAborted);

            if (current is null || !string.Equals(current, stampInCookie, StringComparison.Ordinal))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(HubClaims.CookieScheme);
            }
        };
    }

    /// <summary>
    /// Whether a cookie of this installation may only travel over https. It follows the scheme of
    /// the callback, which is what the browser actually judges the round trip by: https everywhere
    /// in production, and "as the request came" in development, where the login is exercised over
    /// http on localhost and an Always cookie would simply never be sent back.
    /// </summary>
    private static CookieSecurePolicy SecurePolicyFor(IvaoOAuthOptions ivao) =>
        CallbackIsHttps(ivao) ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

    private static bool CallbackIsHttps(IvaoOAuthOptions ivao) =>
        Uri.TryCreate(ivao.RedirectUri, UriKind.Absolute, out var redirect)
        && string.Equals(redirect.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static void ConfigureOpenIdConnect(OpenIdConnectOptions options, IvaoOAuthOptions ivao)
    {
        // The two cookies that carry the round trip. Their settings follow the scheme of the
        // callback, not the environment, because that is what the browser actually judges them by.
        //
        // Over https the framework default is right: SameSite=None marked Secure.
        //
        // Over http, which is how the login is exercised in development, that same default is
        // silently fatal: a cookie declared SameSite=None without Secure is rejected outright by
        // every current browser, so nothing comes back and the callback fails with "Correlation
        // failed" while looking like a network problem. Lax is both accepted and sufficient here:
        // the return from IVAO is a top level GET navigation, which Lax cookies are sent on.
        var callbackIsHttps = CallbackIsHttps(ivao);

        options.CorrelationCookie.SecurePolicy =
            callbackIsHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.CorrelationCookie.SameSite = callbackIsHttps ? SameSiteMode.None : SameSiteMode.Lax;
        options.NonceCookie.SecurePolicy = options.CorrelationCookie.SecurePolicy;
        options.NonceCookie.SameSite = options.CorrelationCookie.SameSite;

        options.Authority = ivao.Authority;
        options.ClientId = ivao.ClientId;
        options.ClientSecret = ivao.ClientSecret;
        options.SignInScheme = HubClaims.CookieScheme;

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        // The tokens are read from the token endpoint response and stored encrypted in the
        // database, so they never travel inside the authentication cookie.
        options.SaveTokens = false;

        options.CallbackPath = new PathString(new Uri(ivao.RedirectUri).AbsolutePath);

        options.Scope.Clear();
        foreach (var scope in ivao.Scopes)
        {
            options.Scope.Add(scope);
        }

        options.ProtocolValidator = new IvaoOidcProtocolValidator(validateNonce: true);

        // The redirect URI is taken from configuration and never rebuilt from the Host header. It
        // has to match, character for character, the one registered with IVAO, and behind the Vite
        // proxy in development and Cloudflare in production the header is not something to trust.
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.RedirectUri = ivao.RedirectUri;
            return Task.CompletedTask;
        };

        options.Events.OnTokenValidated = context =>
        {
            var response = context.TokenEndpointResponse;
            if (response?.AccessToken is { Length: > 0 } accessToken)
            {
                var lifetime = int.TryParse(response.ExpiresIn, CultureInfo.InvariantCulture, out var seconds)
                    ? TimeSpan.FromSeconds(seconds)
                    : TimeSpan.FromHours(1);

                // HttpContext.Items and not AuthenticationProperties: the latter is serialised into
                // the cookie, and these are credentials.
                var clock = context.HttpContext.RequestServices.GetRequiredService<IClock>();

                context.HttpContext.Items[TokensItemKey] = new IvaoUserTokens(
                    accessToken,
                    response.RefreshToken,
                    clock.UtcNow.Add(lifetime),
                    response.Scope);
            }

            return Task.CompletedTask;
        };

        options.Events.OnUserInformationReceived = context =>
        {
            var profile = IvaoUserProfileReader.Read(context.User.RootElement);
            if (profile is not null)
            {
                context.HttpContext.Items[ProfileItemKey] = profile;
            }

            return Task.CompletedTask;
        };

        options.Events.OnTicketReceived = OnTicketReceived;
        options.Events.OnRemoteFailure = OnRemoteFailure;
    }

    /// <summary>
    /// The IVAO identity is exchanged for the application identity here: the row in
    /// <c>hub_users</c> is written, the effective permissions are computed and the compact
    /// principal that will live in the cookie is built. The large IVAO claims are dropped.
    /// </summary>
    private static async Task OnTicketReceived(TicketReceivedContext context)
    {
        var services = context.HttpContext.RequestServices;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IvaoAuthenticationExtensions));

        if (context.HttpContext.Items[ProfileItemKey] is not IvaoUserProfile profile)
        {
            logger.LogError("The IVAO user info did not contain a VID; the login cannot be completed.");
            context.HandleResponse();
            context.Response.Redirect($"{LoginErrorPath}?code=profile");
            return;
        }

        var sync = services.GetRequiredService<UserSyncService>();
        var signedIn = await sync.UpsertAsync(profile, context.HttpContext.RequestAborted);

        if (context.HttpContext.Items[TokensItemKey] is IvaoUserTokens tokens)
        {
            var store = services.GetRequiredService<IvaoUserTokenStore>();
            await store.SaveAsync(profile.Vid, tokens, context.HttpContext.RequestAborted);
            context.HttpContext.Items.Remove(TokensItemKey);
        }

        var division = services.GetRequiredService<IOptions<DivisionOptions>>().Value;

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

        context.Principal = new ClaimsPrincipal(identity);

        services.GetRequiredService<ISecurityStampCache>().Invalidate(signedIn.User.Vid);

        if (signedIn.User.IsSuperadmin)
        {
            // Every login taken as a super administrator is worth a line: the role bypasses every
            // policy, so it must never be invisible (plan section 6.3).
            logger.LogWarning("VID {Vid} signed in as a super administrator.", signedIn.User.Vid);
        }
    }

    /// <summary>
    /// Every failure of the IVAO round trip goes through here. Without this the handler rethrows and
    /// the failure comes out as an unhandled exception: a page that tells the user nothing and
    /// leaves us nothing either.
    /// <para>It deliberately does not bounce back to the login: if the fault is stable, IVAO still
    /// has its session open and sends the browser straight back, which is an infinite loop.</para>
    /// </summary>
    private static Task OnRemoteFailure(RemoteFailureContext context)
    {
        var code = Classify(context.Failure, context.Request.Query["error"]);
        context.HandleResponse();

        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(IvaoAuthenticationExtensions));

        // Never the code or a token: those are credentials.
        logger.LogWarning(
            context.Failure,
            "IVAO login failed, classified as {Code}. Error reported by the portal: {PortalError}.",
            code,
            string.IsNullOrWhiteSpace(context.Request.Query["error"]) ? "none" : context.Request.Query["error"].ToString());

        context.Response.Redirect($"{LoginErrorPath}?code={code}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Which way the round trip went wrong, in one word out of a closed set: it ends up in the log
    /// and picks the sentence shown to the user, so text coming from outside is never reflected.
    /// </summary>
    internal static string Classify(Exception? failure, string? portalError)
    {
        if (!string.IsNullOrWhiteSpace(portalError))
        {
            return "portal";
        }

        var message = failure?.ToString() ?? string.Empty;

        // "Unable to unprotect" is also the symptom of a lost key ring: if every login on the server
        // comes out with this, look at hub-keys/ before looking at the browsers.
        if (message.Contains("Correlation failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("oauth state was missing", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unable to unprotect", StringComparison.OrdinalIgnoreCase))
        {
            return "correlation";
        }

        if (message.Contains("IDX21323", StringComparison.Ordinal)
            || message.Contains("IDX21320", StringComparison.Ordinal)
            || message.Contains("nonce", StringComparison.OrdinalIgnoreCase))
        {
            return "nonce";
        }

        return "unknown";
    }

    /// <summary>
    /// Only redirects that are unmistakably a path of this site.
    /// <para>Checking that it starts with a slash and not with two is not enough: browsers turn a
    /// backslash into a slash <b>before</b> resolving the URL, so <c>/\evil.com</c> becomes
    /// <c>//evil.com</c> and leads outside. A hop like that makes an excellent phishing tool
    /// precisely because the first step, the login, is genuine.</para>
    /// </summary>
    public static string SafeReturnUrl(string? returnUrl)
    {
        const string fallback = "/";

        if (string.IsNullOrEmpty(returnUrl) || returnUrl[0] != '/')
        {
            return fallback;
        }

        if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\'))
        {
            return fallback;
        }

        // A carriage return in a Location header is response splitting, and no legitimate path
        // contains one.
        return returnUrl.Any(character => char.IsControl(character)) ? fallback : returnUrl;
    }
}
