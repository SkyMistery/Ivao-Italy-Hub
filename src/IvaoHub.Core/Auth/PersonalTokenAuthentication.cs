using System.Security.Claims;
using System.Text.Encodings.Web;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The policy an endpoint of an audience asks for (M2, T19a): <c>RequireAuthorization(PersonalTokenPolicy.For("flightops.agent"))</c>.
/// The policy provider turns it into "a personal token of that audience, whose member holds the audience's permission
/// somewhere"; what they may do on a row is still the single handler's answer, with the row in hand.
/// </summary>
public static class PersonalTokenPolicy
{
    public const string Prefix = "Token:";

    public static string For(string audience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        return Prefix + audience;
    }
}

/// <summary>
/// The second way of saying who is asking, for a member's own program (note 2026-09-15-token-personali-e-agente-del-validatore
/// §3.1). The token carries nothing but who: the identity is rebuilt on every request exactly as a login builds it
/// (<see cref="HubClaims.BuildIdentity"/>), from the positions and the grants of now, so a grant taken away stops counting on
/// the next request. It is never the default scheme — a request with a token and nothing else is anonymous to every endpoint
/// that did not ask for its audience, and the back office is not one of them.
/// </summary>
public sealed class PersonalTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    private const string BearerPrefix = "Bearer ";

    private const string RefusalItemKey = "hub.token.refusal";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var services = Context.RequestServices;
        var (token, refusal) = await services.GetRequiredService<PersonalTokens>()
            .RecogniseAsync(header[BearerPrefix.Length..].Trim(), Context.RequestAborted);

        var signedIn = token is null
            ? null
            : await services.GetRequiredService<UserSyncService>().LoadAsync(token.Vid, Context.RequestAborted);

        if (token is null || signedIn is null)
        {
            var why = refusal ?? TokenRefusal.Unknown;
            Context.Items[RefusalItemKey] = why;
            return AuthenticateResult.Fail($"Personal token refused: {why}.");
        }

        var division = services.GetRequiredService<IOptions<DivisionOptions>>().Value;
        var built = HubClaims.BuildIdentity(
            signedIn.User.Vid,
            signedIn.User.FirstName,
            signedIn.User.LastName,
            signedIn.User.Locale ?? division.DefaultLocale,
            signedIn.User.SecurityStamp,
            signedIn.User.IsSuperadmin,
            signedIn.User.IsStaff,
            signedIn.Positions,
            signedIn.Permissions);

        var identity = new ClaimsIdentity(built.Claims, HubClaims.TokenScheme, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        identity.AddClaim(new Claim(HubClaims.Audience, token.Audience));
        identity.AddClaim(new Claim(HubClaims.PersonalToken, token.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        // A line of log for every use, and not an audit row: reading a hundred reports is not a hundred changes (note §3.1).
        Logger.LogInformation(
            "Personal token {Token} of VID {Vid} used for {Method} {Path}",
            token.Id,
            token.Vid,
            Request.Method,
            Request.Path);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), HubClaims.TokenScheme));
    }

    /// <summary>
    /// 401 with a word the program can act on (<c>code</c>: unknown, revoked, expired, signInAgain) and the sentence for a
    /// person in the division's language. Never a redirect: nobody is there to follow it.
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var refusal = Context.Items[RefusalItemKey] as TokenRefusal? ?? TokenRefusal.Unknown;
        var code = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(refusal.ToString());

        var locale = Context.RequestServices.GetRequiredService<IOptions<DivisionOptions>>().Value.DefaultLocale;
        var catalog = Context.RequestServices.GetRequiredService<LocaleCatalog>();

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
        await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: catalog.Resolve(locale, $"errors.token.{code}"),
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code })
            .ExecuteAsync(Context);
    }
}

/// <summary>Registration of the scheme and of its services, next to the cookie's.</summary>
public static class PersonalTokenAuthenticationExtensions
{
    public static AuthenticationBuilder AddPersonalTokens(this AuthenticationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<PersonalTokens>();
        return builder.AddScheme<AuthenticationSchemeOptions, PersonalTokenAuthenticationHandler>(HubClaims.TokenScheme, _ => { });
    }

    /// <summary>The policy of an audience: the token scheme only, the audience's claim, and its permission held somewhere.</summary>
    internal static AuthorizationPolicy? PolicyFor(string policyName, TokenAudienceCatalog audiences)
    {
        if (!policyName.StartsWith(PersonalTokenPolicy.Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var audience = audiences.Find(policyName[PersonalTokenPolicy.Prefix.Length..])
            ?? throw new InvalidOperationException(
                $"'{policyName}' names a token audience no module declares. Add it to the module's TokenAudiences.");

        return new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(HubClaims.TokenScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(HubClaims.Audience, audience.Key)
            .AddRequirements(new PermissionRequirement(audience.RequiredPermission))
            .Build();
    }
}
