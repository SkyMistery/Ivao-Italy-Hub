namespace IvaoHub.Core.Auth;

/// <summary>
/// The IVAO access and refresh tokens of a user, encrypted with Data Protection. If the keys in
/// <c>hub-keys/</c> are lost the values become unreadable and are treated as absent, never as an
/// error (plan section 16.14).
/// </summary>
public sealed class UserToken
{
    public int Vid { get; set; }

    public string AccessTokenEnc { get; set; } = string.Empty;

    public string? RefreshTokenEnc { get; set; }

    public DateTime ExpiresAt { get; set; }

    public string? Scopes { get; set; }

    public DateTime UpdatedAt { get; set; }

    public HubUser? User { get; set; }
}
