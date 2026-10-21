using System.Security.Claims;
using IvaoHub.Core.Auth;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The lifetime a hub cookie is checked by (<see cref="HubClaims.IsWithinLifetime"/>), and the one number an installation
/// may set about it. The integration suite moves a real host's clock (<c>SessionLifetimeTests</c>); these are the edges.
/// </summary>
public sealed class SignInLifetimeTests
{
    private static readonly DateTime SignedIn = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Maximum = TimeSpan.FromDays(30);

    [Fact]
    public void ACookieIsWithinItsLifetimeUntilTheMaximumHasPassed()
    {
        var cookie = Cookie(SignedIn);

        Assert.True(HubClaims.IsWithinLifetime(cookie, SignedIn.AddDays(30).AddSeconds(-1), Maximum));
        Assert.False(HubClaims.IsWithinLifetime(cookie, SignedIn.AddDays(30), Maximum));
    }

    [Fact]
    public void ACookieIsWithinItsLifetimeUntilItsFirstGrantEnds()
    {
        var cookie = Cookie(SignedIn, permissionsUntil: SignedIn.AddHours(2));

        Assert.True(HubClaims.IsWithinLifetime(cookie, SignedIn.AddHours(2).AddSeconds(-1), Maximum));
        Assert.False(HubClaims.IsWithinLifetime(cookie, SignedIn.AddHours(2), Maximum));
    }

    [Fact]
    public void ACookieThatDoesNotSayWhenItWasSignedInIsRefused()
    {
        // A cookie written before the claim existed: one more sign-in, once, and never a cookie without a lifetime.
        var cookie = Cookie(signedInAt: null);

        Assert.False(HubClaims.IsWithinLifetime(cookie, SignedIn, Maximum));
    }

    [Fact]
    public void AnEndThatCannotBeReadIsNoPromiseThatNothingEnds()
    {
        var identity = Cookie(SignedIn).Identities.Single();
        identity.AddClaim(new Claim(HubClaims.PermissionsUntil, "soon"));

        Assert.False(HubClaims.IsWithinLifetime(new ClaimsPrincipal(identity), SignedIn.AddMinutes(1), Maximum));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(SignInLifetimeOptions.LongestMaximumDays, true)]
    [InlineData(0, false)]
    [InlineData(SignInLifetimeOptions.LongestMaximumDays + 1, false)]
    public void TheMaximumIsADayToAYear(int? days, bool valid)
    {
        var result = new SignInLifetimeOptionsValidator().Validate(name: null, new SignInLifetimeOptions { MaximumDays = days });

        Assert.Equal(valid, result.Succeeded);
        if (!valid)
        {
            Assert.Contains("SignIn:MaximumDays", result.FailureMessage, StringComparison.Ordinal);
        }
    }

    private static ClaimsPrincipal Cookie(DateTime? signedInAt, DateTime? permissionsUntil = null) =>
        new(HubClaims.BuildIdentity(
            vid: 791_101,
            firstName: "Test",
            lastName: "Cookie",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: false,
            positions: [],
            permissions: [],
            signedInAt,
            permissionsUntil));
}
