using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IvaoHub.Core.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The application's own token, from <c>client_credentials</c>. It is not a user's token: it is
/// how the hub reads reference data that belongs to nobody in particular (plan section 6.2).
/// <para>Cached until shortly before it expires, because asking for a new one on every call would
/// be both slow and rude to IVAO.</para>
/// </summary>
public sealed class IvaoApiTokenProvider(
    HttpClient http,
    IOptions<IvaoOAuthOptions> options,
    IMemoryCache cache,
    ILogger<IvaoApiTokenProvider> logger)
{
    private const string CacheKey = "ivao-api:client-credentials-token";

    /// <summary>Renewed this long before it actually expires, so no call ever races the clock.</summary>
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(60);

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out string? cached) && cached is not null)
        {
            return cached;
        }

        var ivao = options.Value;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/oauth/token")
        {
            Content = new FormUrlEncodedContent(BuildForm(ivao)),
        };

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Never the secret, and never the body: it can echo what was sent.
            logger.LogWarning(
                "IVAO refused a client credentials token with status {Status}. "
                + "Check that the client is allowed the scopes in Ivao:ApiScopes.",
                (int)response.StatusCode);
            return null;
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (token?.AccessToken is not { Length: > 0 } value)
        {
            logger.LogWarning("IVAO answered the token request without an access token.");
            return null;
        }

        // A token worth less than the margin is used once and not kept: holding it would only mean
        // handing a dead token to the next call.
        var lifetime = TimeSpan.FromSeconds(token.ExpiresIn) - Margin;
        if (lifetime > TimeSpan.Zero)
        {
            cache.Set(CacheKey, value, lifetime);
        }

        return value;
    }

    private static Dictionary<string, string> BuildForm(IvaoOAuthOptions ivao)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = ivao.ClientId,
            ["client_secret"] = ivao.ClientSecret,
        };

        // The scopes of the application are not the scopes of a member: client_credentials has no
        // openid, profile or email to ask for.
        if (ivao.ApiScopes.Length > 0)
        {
            form["scope"] = string.Join(' ', ivao.ApiScopes);
        }

        return form;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{TokenType} token, {ExpiresIn}s");
    }
}
