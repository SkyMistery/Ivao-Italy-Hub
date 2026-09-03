using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What a login writes into <c>hub_users</c>.
/// <para>The language is the part worth pinning: the rule that picks it lives in one place
/// (<c>LocalePreference</c>), and it only ever applies when the row is created — but for a while
/// the row was created with the default language already filled in, so the rule sat behind a
/// <c>??=</c> that could never fire and every member of every division silently got the division's
/// own language.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class UserSyncTests(MariaDbFixture database) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(database.ConnectionString, useIvaoFixtures: true);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Theory]
    // The division speaks it, so the member keeps the language IVAO has for them.
    [InlineData("en", "en")]
    [InlineData("it", "it")]
    // A regional tag still counts as the language.
    [InlineData("en-GB", "en")]
    // The division does not speak German: English, not Italian. A hub serves a German member the
    // language of IVAO and of the project, not the language of the division they happened to open.
    [InlineData("de", "en")]
    // IVAO says nothing about them: same answer, for the same reason.
    [InlineData(null, "en")]
    public async Task TheLanguageOfANewMemberComesFromIvaoAndFallsBackToEnglish(string? languageId, string expected)
    {
        var token = TestContext.Current.CancellationToken;
        var vid = NextVid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var sync = scope.ServiceProvider.GetRequiredService<UserSyncService>();

        var signedIn = await sync.UpsertAsync(Profile(vid, languageId), token);

        Assert.Equal(expected, signedIn.User.Locale);

        var stored = await scope.ServiceProvider.GetRequiredService<HubDbContext>()
            .Users.AsNoTracking().FirstAsync(user => user.Vid == vid, token);

        Assert.Equal(expected, stored.Locale);
    }

    [Fact]
    public async Task ALaterLoginNeverOverwritesTheLanguageTheMemberHas()
    {
        var token = TestContext.Current.CancellationToken;
        var vid = NextVid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var sync = scope.ServiceProvider.GetRequiredService<UserSyncService>();
        var context = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        await sync.UpsertAsync(Profile(vid, "en"), token);

        // The member picks Italian in the interface; the switcher of F6 writes exactly this.
        var user = await context.Users.FirstAsync(row => row.Vid == vid, token);
        user.Locale = "it";
        await context.SaveChangesAsync(token);

        // IVAO still says English. The choice of the member wins, every time.
        var signedIn = await sync.UpsertAsync(Profile(vid, "en"), token);

        Assert.Equal("it", signedIn.User.Locale);
    }

    [Fact]
    public void EnglishIsTheFallbackOfTheProjectAndNotOfTheDivision()
    {
        // Pinned next to the login it serves: the division is Italian, the fallback is not.
        var division = _factory.Services.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<Core.Division.DivisionOptions>>().Value;

        Assert.Equal("it", division.DefaultLocale);
        Assert.Equal("en", LocalePreference.Resolve("de", division));
    }

    private static IvaoUserProfile Profile(int vid, string? languageId) => new(
        Vid: vid,
        FirstName: "Test",
        LastName: "Member",
        PublicNickname: null,
        DivisionCode: "IT",
        CountryId: "IT",
        RatingAtc: null,
        RatingPilot: null,
        DiscordId: null,
        LanguageId: languageId,
        IvaoIsStaff: false,
        IvaoIsSupervisor: false,
        StaffPositions: []);

    /// <summary>A VID of its own per case, so the cases never share a row.</summary>
    private static int NextVid() => 810000 + Random.Shared.Next(1, 89999);
}
