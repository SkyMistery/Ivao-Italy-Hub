using IvaoHub.Core.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The application must refuse to start rather than send people to a login that cannot work, and
/// the message must never contain the secret (design M0 section 2.2).
/// </summary>
public sealed class IvaoOAuthOptionsValidationTests
{
    private const string Secret = "super-secret-value-that-must-never-be-printed";

    private static IvaoOAuthOptions Valid() => new()
    {
        Authority = "https://api.ivao.aero",
        ClientId = "client",
        ClientSecret = Secret,
        LoginUrl = "https://example.ivao.aero/auth/login",
        RedirectUri = "https://example.ivao.aero/auth/callback",
        PostLogoutRedirectUri = "https://example.ivao.aero/",
        Scopes = ["openid", "profile"],
    };

    private static ValidateOptionsResult Validate(IvaoOAuthOptions options) =>
        new IvaoOAuthOptionsValidator().Validate(null, options);

    [Fact]
    public void AcceptsACompleteConfiguration()
    {
        Assert.True(Validate(Valid()).Succeeded);
    }

    [Fact]
    public void RejectsAnEmptyConfigurationAndSaysHowToFixIt()
    {
        var result = Validate(new IvaoOAuthOptions());

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'ClientId'", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("ivao-oauth.example.json", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsARedirectUriThatDoesNotEndWithTheCallbackPath()
    {
        var result = Validate(Valid() with { RedirectUri = "https://example.ivao.aero/signin" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("/auth/callback", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsScopesWithoutOpenId()
    {
        // Without it IVAO answers with no id token, and the flow then fails validating a nonce that
        // was never going to arrive: the message talks about the nonce, and the missing scope is
        // the last thing anybody thinks to look at.
        var result = Validate(Valid() with { Scopes = ["profile", "email"] });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("openid", StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptsAMissingPostLogoutRedirectUri()
    {
        // Nothing reads it: signing out is local, because IVAO has no end session endpoint to come
        // back from. Demanding it would only teach a division that the file asks for things that
        // do not matter.
        Assert.True(Validate(Valid() with { PostLogoutRedirectUri = "" }).Succeeded);
    }

    [Fact]
    public void RejectsALoginUrlThatDoesNotEndWithTheLoginPath()
    {
        var result = Validate(Valid() with { LoginUrl = "https://example.ivao.aero/signin" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("/auth/login", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsALoginUrlAndARedirectUriOnDifferentHosts()
    {
        var result = Validate(Valid() with { LoginUrl = "https://other.ivao.aero/auth/login" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("same scheme, host and port", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("example.ivao.aero/auth/callback")]
    [InlineData("ftp://example.ivao.aero/auth/callback")]
    public void RejectsARedirectUriThatIsNotAnAbsoluteHttpUrl(string redirectUri)
    {
        var result = Validate(Valid() with { RedirectUri = redirectUri });

        Assert.True(result.Failed);
    }

    [Fact]
    public void RejectsAnEmptyScopeList()
    {
        var result = Validate(Valid() with { Scopes = [] });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'Scopes'", StringComparison.Ordinal));
    }

    [Fact]
    public void NeverPutsTheSecretInAFailureMessage()
    {
        var result = Validate(Valid() with { RedirectUri = string.Empty });

        Assert.True(result.Failed);
        Assert.DoesNotContain(result.Failures, failure => failure.Contains(Secret, StringComparison.Ordinal));
    }
}
