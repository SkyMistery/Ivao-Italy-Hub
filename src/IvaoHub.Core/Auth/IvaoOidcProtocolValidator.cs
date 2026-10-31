using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The OpenID Connect validator, adapted to IVAO. One deliberate departure from the specification:
/// the user info response is not validated, because IVAO's is <c>/v2/users/me</c> and not an OIDC
/// user info endpoint.
/// <para>The nonce <b>is</b> validated: it travels inside the IVAO id_token. The switch is a
/// parameter of the constructor, and the hub passes <c>true</c> and reads it from nowhere
/// (<c>IvaoAuthenticationExtensions</c>): turning the check off is a change of the code, reviewed
/// like one, never a line of configuration (issue #320, item 8). The one failure IVAO is known to
/// cause, its consent screen, is answered by a second round instead (note
/// 2026-09-28-il-nonce-e-il-consenso-di-ivao).</para>
/// <para>Inherited from the vIPI implementation, where all of this was measured against the real
/// flow rather than assumed; the official IVAO samples still turn nonce and state off and are older
/// than the change to their authentication system.</para>
/// </summary>
public sealed class IvaoOidcProtocolValidator : OpenIdConnectProtocolValidator
{
    public IvaoOidcProtocolValidator(bool validateNonce)
    {
        ShouldValidateNonce = validateNonce;

        // RequireState stays false, and it is not a concession to IVAO: ASP.NET Core never
        // populates OpenIdConnectProtocolValidationContext.State, so with true the validator
        // throws IDX21329 "State is null" against any identity provider whatsoever. The state is
        // checked all the same, by the handler, through the correlation cookie.
        RequireState = false;
        RequireNonce = validateNonce;
    }

    private bool ShouldValidateNonce { get; }

    /// <summary>
    /// Nothing is checked here. The access token used to call <c>/v2/users/me</c> comes out of the
    /// same code exchange, already bound by PKCE and by the nonce, so the standard check would add
    /// no defence while depending on how the handler passes the validated id_token down this path.
    /// </summary>
    public override void ValidateUserInfoResponse(OpenIdConnectProtocolValidationContext validationContext)
    {
    }

    protected override void ValidateNonce(OpenIdConnectProtocolValidationContext validationContext)
    {
        if (ShouldValidateNonce)
        {
            base.ValidateNonce(validationContext);
        }
    }
}
