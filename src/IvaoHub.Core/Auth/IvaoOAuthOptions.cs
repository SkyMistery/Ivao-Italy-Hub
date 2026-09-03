using System.ComponentModel.DataAnnotations;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The OAuth client of the division, from <c>config/ivao-oauth.json</c> (never in the repository)
/// or from <c>Ivao__*</c> environment variables, which win (plan section 6.1).
/// </summary>
public sealed record IvaoOAuthOptions
{
    /// <summary>Configuration section that holds these values.</summary>
    public const string SectionName = "Ivao";

    [Required]
    public string Authority { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Never logged, never returned by an endpoint, never written to the diagnostics file.</summary>
    [Required]
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>Must match, character for character, the login URL registered with IVAO.</summary>
    [Required]
    public string LoginUrl { get; init; } = string.Empty;

    /// <summary>Must match the redirect URL registered with IVAO and end with <c>/auth/callback</c>.</summary>
    [Required]
    public string RedirectUri { get; init; } = string.Empty;

    [Required]
    public string PostLogoutRedirectUri { get; init; } = string.Empty;

    /// <summary>The scopes asked of a member when they sign in.</summary>
    public string[] Scopes { get; init; } = [];

    /// <summary>
    /// The scopes the application asks for itself, with <c>client_credentials</c>, to read data
    /// that belongs to nobody in particular. Empty means a token with no scope at all, which is
    /// what the reference endpoints need today.
    /// </summary>
    public string[] ApiScopes { get; init; } = [];
}
