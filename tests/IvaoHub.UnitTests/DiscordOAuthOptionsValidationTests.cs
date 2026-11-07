using IvaoHub.Core.Auth;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// A division without Discord starts as it always did; one that filled the section in halfway is told so at the start, and
/// never with the secret in the message.
/// </summary>
public sealed class DiscordOAuthOptionsValidationTests
{
    private const string Secret = "super-secret-value-that-must-never-be-printed";

    private static DiscordOAuthOptions Valid() => new()
    {
        ClientId = "client",
        ClientSecret = Secret,
        RedirectUri = "https://example.org/auth/discord/callback",
    };

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(DiscordOAuthOptions options) =>
        new DiscordOAuthOptionsValidator().Validate(name: null, options);

    [Fact]
    public void NoSectionAtAllIsAHubWithoutDiscord()
    {
        var options = new DiscordOAuthOptions();

        Assert.True(Validate(options).Succeeded);
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void AWholeSectionOffersTheLink()
    {
        Assert.True(Validate(Valid()).Succeeded);
        Assert.True(Valid().IsConfigured);
    }

    [Fact]
    public void ASectionFilledInHalfwayIsRefusedWithoutPrintingTheSecret()
    {
        var result = Validate(Valid() with { ClientId = string.Empty });

        Assert.True(result.Failed);
        Assert.Contains("ClientId", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/auth/discord/callback")]
    [InlineData("https://example.org/auth/callback")]
    [InlineData("ftp://example.org/auth/discord/callback")]
    public void TheRedirectMustBeTheCallbackOfThisHub(string redirect)
    {
        var result = Validate(Valid() with { RedirectUri = redirect });

        Assert.True(result.Failed);
        Assert.Contains("RedirectUri", result.FailureMessage, StringComparison.Ordinal);
    }
}
