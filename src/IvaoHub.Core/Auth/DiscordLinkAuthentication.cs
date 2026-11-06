using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The round trip a member makes to Discord to prove an account is theirs. It signs nobody in: the identity is the hub's
/// cookie, and what Discord says ends in three columns of <c>hub_users</c> (<see cref="DiscordLinks"/>). The account goes to
/// the VID that started the round trip and only if the cookie still says that VID when it comes back: on a shared browser, a
/// session that changed in between must not receive somebody else's account. The access token reads one profile and is thrown
/// away.
/// <para>State, correlation cookie and PKCE are the framework's, as for the sign in. The protected state travels in the address
/// and the correlation nonce in a cookie, so a host that stops the process between the two hops loses nothing (note
/// 2026-11-05-il-collegamento-con-discord §5).</para>
/// </summary>
public static class DiscordLinkAuthentication
{
    /// <summary>Where Discord sends the browser back; handled by the scheme, through <c>CallbackPath</c>.</summary>
    public const string CallbackPath = "/auth/discord/callback";

    /// <summary>
    /// The framework refuses an OAuth scheme with no client on every request, because every request passes through a remote
    /// handler. Which schemes exist is decided before a test host's or an installation's settings are final, so the scheme is
    /// always there, with this in place of the client and <see cref="DiscordLinkHandler"/> answering nothing.
    /// </summary>
    private const string NotConfigured = "not-configured";

    private const string AccountIdClaim = "discord:id";

    private const string UsernameClaim = "discord:username";

    /// <summary>Who started the round trip, inside the protected state, so nobody can write it from outside.</summary>
    private const string StartedByItem = "discord.started-by";

    /// <summary>Where a round trip ends. A failure says which one through the search, and the page draws it.</summary>
    private const string Profile = "/me";

    /// <summary>What the round trip of this member carries with it: see <see cref="StartedByItem"/>.</summary>
    public static AuthenticationProperties StartedBy(int vid) =>
        new(new Dictionary<string, string?> { [StartedByItem] = vid.ToString(CultureInfo.InvariantCulture) });

    public static IServiceCollection AddDiscordLink(this IServiceCollection services)
    {
        services.AddOptions<DiscordOAuthOptions>().BindConfiguration(DiscordOAuthOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<DiscordOAuthOptions>, DiscordOAuthOptionsValidator>();
        services.AddScoped<DiscordLinks>();

        services.AddAuthentication().AddOAuth<OAuthOptions, DiscordLinkHandler>(HubClaims.DiscordScheme, "Discord", _ => { });

        // As for the sign in: the options need the configuration, so they go through the options pipeline.
        services.AddOptions<OAuthOptions>(HubClaims.DiscordScheme)
            .Configure<IOptions<DiscordOAuthOptions>>((options, discord) => Configure(options, discord.Value));

        return services;
    }

    private static void Configure(OAuthOptions options, DiscordOAuthOptions discord)
    {
        options.ClientId = discord.IsConfigured ? discord.ClientId : NotConfigured;
        options.ClientSecret = discord.IsConfigured ? discord.ClientSecret : NotConfigured;
        options.CallbackPath = CallbackPath;

        options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
        options.TokenEndpoint = "https://discord.com/api/oauth2/token";
        options.UserInformationEndpoint = "https://discord.com/api/v10/users/@me";

        // The name and the id, and nothing that lets the hub act on the account.
        options.Scope.Add("identify");
        options.UsePkce = true;

        // The event below always answers, so nobody is ever signed in with what Discord said; this only names a scheme that
        // is not this one, which the framework asks for.
        options.SignInScheme = HubClaims.CookieScheme;

        // Same reasoning as the sign in's two cookies: over http SameSite=None is rejected by the browser, and Lax is enough
        // for the top level GET that comes back from Discord.
        var overHttps = discord.RedirectUri.StartsWith("https:", StringComparison.OrdinalIgnoreCase);
        options.CorrelationCookie.SecurePolicy = overHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.CorrelationCookie.SameSite = overHttps ? SameSiteMode.None : SameSiteMode.Lax;

        options.Events = new OAuthEvents
        {
            OnCreatingTicket = ReadAccountAsync,
            OnTicketReceived = LinkAsync,

            // Changing one's mind on Discord's page is not a failure: back to the profile, with nothing to say.
            OnAccessDenied = context =>
            {
                context.Response.Redirect(Profile);
                context.HandleResponse();
                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(DiscordLinkAuthentication))
                    .LogWarning(context.Failure, "A Discord round trip could not be completed.");
                context.Response.Redirect(Ended(DiscordLinkOutcome.Failed));
                context.HandleResponse();
                return Task.CompletedTask;
            },
        };
    }

    private static async Task ReadAccountAsync(OAuthCreatingTicketContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

        // A refusal here throws, and the framework turns it into OnRemoteFailure.
        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();

        using var account = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
        var identity = context.Identity!;
        identity.AddClaim(new Claim(AccountIdClaim, account.RootElement.GetProperty("id").GetString() ?? string.Empty));
        identity.AddClaim(new Claim(UsernameClaim, account.RootElement.GetProperty("username").GetString() ?? string.Empty));
    }

    private static async Task LinkAsync(TicketReceivedContext context)
    {
        // Answered here, always: the framework would otherwise sign the principal in.
        context.HandleResponse();
        context.Response.Redirect(Ended(await TryLinkAsync(context)));
    }

    private static async Task<DiscordLinkOutcome> TryLinkAsync(TicketReceivedContext context)
    {
        var http = context.HttpContext;
        try
        {
            var signedIn = await http.AuthenticateAsync(HubClaims.CookieScheme);
            if (!signedIn.Succeeded)
            {
                // The session ended while the member was on Discord's page; the profile's guard sends them to sign in again.
                return DiscordLinkOutcome.Failed;
            }

            // The remote handlers run before the middleware has set the identity, and whoever writes below asks for it.
            http.User = signedIn.Principal;
            var vid = http.RequestServices.GetRequiredService<ICurrentUser>().Vid;

            string? startedBy = null;
            if (context.Properties?.Items.TryGetValue(StartedByItem, out startedBy) != true
                || startedBy != vid.ToString(CultureInfo.InvariantCulture))
            {
                return DiscordLinkOutcome.Failed;
            }

            return await http.RequestServices.GetRequiredService<DiscordLinks>().LinkAsync(
                vid,
                context.Principal?.FindFirstValue(AccountIdClaim) ?? string.Empty,
                context.Principal?.FindFirstValue(UsernameClaim) ?? string.Empty,
                http.RequestAborted);
        }
#pragma warning disable CA1031 // Whatever failed, the member gets the profile that says so, and the log the exception.
        catch (Exception exception) when (!http.RequestAborted.IsCancellationRequested)
#pragma warning restore CA1031
        {
            // ⚠️ The framework raises this event outside the try that leads to OnRemoteFailure (RemoteAuthenticationHandler,
            // read in its source for .NET 10): a database that fails here would end the member's navigation on a page of JSON.
            http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(DiscordLinkAuthentication))
                .LogWarning(exception, "A Discord link could not be written.");
            return DiscordLinkOutcome.Failed;
        }
    }

    private static string Ended(DiscordLinkOutcome outcome) => outcome switch
    {
        DiscordLinkOutcome.Linked => Profile,
        DiscordLinkOutcome.Taken => $"{Profile}?discord=taken",
        _ => $"{Profile}?discord=failed",
    };
}

/// <summary>
/// The framework's OAuth handler, with two differences. The redirect is the one registered with Discord and never rebuilt
/// from the request, like the sign in's. And with no Discord application it answers nothing, so the callback path is one more
/// address nobody knows.
/// </summary>
internal sealed class DiscordLinkHandler(
    IOptionsMonitor<OAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<DiscordOAuthOptions> discord)
    : OAuthHandler<OAuthOptions>(options, logger, encoder)
{
    public override Task<bool> ShouldHandleRequestAsync() =>
        discord.Value.IsConfigured ? base.ShouldHandleRequestAsync() : Task.FromResult(false);

    protected override string BuildChallengeUrl(AuthenticationProperties properties, string redirectUri) =>
        base.BuildChallengeUrl(properties, discord.Value.RedirectUri);

    protected override Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context) =>
        base.ExchangeCodeAsync(new OAuthCodeExchangeContext(context.Properties, context.Code, discord.Value.RedirectUri));
}
