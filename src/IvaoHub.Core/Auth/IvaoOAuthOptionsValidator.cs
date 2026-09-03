using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// Fails the startup when the OAuth client is missing or inconsistent (design M0 section 2.2).
/// The messages say which field is wrong and how to fix it, and never contain the secret.
/// </summary>
public sealed class IvaoOAuthOptionsValidator : IValidateOptions<IvaoOAuthOptions>
{
    private const string CallbackPath = "/auth/callback";
    private const string LoginPath = "/auth/login";

    private const string HowToFix =
        "Copy config/ivao-oauth.example.json to config/ivao-oauth.json and fill it in, "
        + "or set the Ivao__* environment variables.";

    public ValidateOptionsResult Validate(string? name, IvaoOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        RequireValue(options.Authority, nameof(options.Authority), failures);
        RequireValue(options.ClientId, nameof(options.ClientId), failures);
        RequireValue(options.ClientSecret, nameof(options.ClientSecret), failures);

        var login = RequireAbsoluteUrl(options.LoginUrl, nameof(options.LoginUrl), failures);
        var redirect = RequireAbsoluteUrl(options.RedirectUri, nameof(options.RedirectUri), failures);

        if (redirect is not null && !redirect.AbsolutePath.Equals(CallbackPath, StringComparison.Ordinal))
        {
            failures.Add($"ivao-oauth.json: 'RedirectUri' must end with {CallbackPath}.");
        }

        if (login is not null && !login.AbsolutePath.Equals(LoginPath, StringComparison.Ordinal))
        {
            failures.Add($"ivao-oauth.json: 'LoginUrl' must end with {LoginPath}.");
        }

        if (login is not null && redirect is not null
            && !login.GetLeftPart(UriPartial.Authority).Equals(redirect.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("ivao-oauth.json: 'LoginUrl' and 'RedirectUri' must be on the same scheme, host and port.");
        }

        if (options.Scopes.Length > 0 && !options.Scopes.Contains("openid", StringComparer.Ordinal))
        {
            // Without it the answer carries no id token, and the flow then validates a nonce that
            // will never arrive: the login fails late, complaining about the nonce rather than
            // about the scope that is actually missing.
            failures.Add("ivao-oauth.json: 'Scopes' must contain \"openid\": the sign in is OpenID Connect.");
        }

        if (options.Scopes.Length == 0)
        {
            failures.Add("ivao-oauth.json: 'Scopes' must list at least one scope, for example \"openid\".");
        }

        if (failures.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        failures.Add(HowToFix);
        return ValidateOptionsResult.Fail(failures);
    }

    private static void RequireValue(string value, string field, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"ivao-oauth.json: '{field}' is required.");
        }
    }

    private static Uri? RequireAbsoluteUrl(string value, string field, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"ivao-oauth.json: '{field}' is required.");
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"ivao-oauth.json: '{field}' must be an absolute http or https URL.");
            return null;
        }

        return uri;
    }
}
