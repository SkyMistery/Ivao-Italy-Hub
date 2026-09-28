using System.Net;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What one installation says about itself, apart from the division (note 2026-09-27-l-installazione-di-prova): a host of
/// its own, which every absolute link is built on, and whether it is private — not indexed, and open to the staff of the
/// division only, with a member turned away before anything about them is written.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class InstallationTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const string TestDomain = "test.hub.example.org";

    // VIDs no other test uses: a refused member is proven by the absence of their rows.
    private const int MemberVid = 927001;
    private const int StaffVid = 927002;
    private const int BootstrapVid = 927003;
    private const int PublicMemberVid = 927004;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private WebApplicationFactory<Program> Private() =>
        _factory.WithWebHostBuilder(builder => builder.UseSetting("Installation:Preview", "true"));

    [Fact]
    public async Task TheInstallationsDomainIsTheOneEveryLinkIsBuiltOn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = _factory.WithWebHostBuilder(builder => builder.UseSetting(InstallationOptions.DomainKey, TestDomain));

        // One place resolves it: the division's domain is the installation's, so a mail, the sitemap and robots.txt all
        // read the same value and none of them knows there were two.
        Assert.Equal(TestDomain, test.Services.GetRequiredService<IOptions<DivisionOptions>>().Value.Domain);

        using var client = test.CreateClient();
        var robots = await client.GetStringAsync(new Uri("/robots.txt", UriKind.Relative), token);
        Assert.Contains($"Sitemap: https://{TestDomain}/sitemap.xml", robots, StringComparison.Ordinal);

        var sitemap = await client.GetStringAsync(new Uri("/sitemap.xml", UriKind.Relative), token);
        Assert.Contains($"<loc>https://{TestDomain}/</loc>", sitemap, StringComparison.Ordinal);

        // Without it, the division file's, exactly as before.
        var fromFile = HubConfiguration.DivisionFile(_factory.Paths)["domain"];
        Assert.Equal(fromFile, _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value.Domain);
    }

    [Fact]
    public async Task APrivateInstallationAsksNotToBeIndexed()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = Private();
        using var client = test.CreateClient();

        var robots = await client.GetStringAsync(new Uri("/robots.txt", UriKind.Relative), token);
        Assert.Equal("User-agent: *\nDisallow: /\n", robots);

        using (var sitemap = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, sitemap.StatusCode);
        }

        // On every answer, not only in robots.txt: a page, the API, a file, an address that does not exist.
        foreach (var path in new[] { "/", "/api/version", "/api/me", "/robots.txt", "/no-such-page" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), token);
            Assert.True(response.Headers.TryGetValues("X-Robots-Tag", out var values), path);
            Assert.Equal("noindex, nofollow", Assert.Single(values));
        }

        // A public installation says nothing of the kind.
        using var publicClient = _factory.CreateClient();
        using var publicAnswer = await publicClient.GetAsync(new Uri("/api/version", UriKind.Relative), token);
        Assert.False(publicAnswer.Headers.Contains("X-Robots-Tag"));

        var publicRobots = await publicClient.GetStringAsync(new Uri("/robots.txt", UriKind.Relative), token);
        Assert.Contains("Sitemap: https://", publicRobots, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APrivateInstallationTurnsAMemberAwayBeforeWritingAnything()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = Private();
        await using var scope = test.Services.CreateAsyncScope();

        var outcome = await scope.ServiceProvider.GetRequiredService<IvaoSignIn>()
            .CompleteAsync(Profile(MemberVid), Tokens(), token);

        Assert.Null(outcome.Principal);
        Assert.Equal(IvaoSignIn.StaffOnly, outcome.Refusal);

        // Nothing about them: no user, no position, no stored token.
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.False(await database.Users.AnyAsync(user => user.Vid == MemberVid, token));
        Assert.False(await database.UserStaffPositions.AnyAsync(position => position.Vid == MemberVid, token));
        Assert.False(await database.UserTokens.AnyAsync(stored => stored.Vid == MemberVid, token));
    }

    [Fact]
    public async Task APrivateInstallationLetsItsStaffIn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = Private();
        await using var scope = test.Services.CreateAsyncScope();
        var code = scope.ServiceProvider.GetRequiredService<IOptions<DivisionOptions>>().Value.Code;

        var outcome = await scope.ServiceProvider.GetRequiredService<IvaoSignIn>()
            .CompleteAsync(Profile(StaffVid, $"{code}-WM"), Tokens(), token);

        Assert.NotNull(outcome.Principal);
        Assert.Null(outcome.Refusal);

        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var user = await database.Users.AsNoTracking().SingleAsync(row => row.Vid == StaffVid, token);
        Assert.True(user.IsStaff);
        Assert.True(await database.UserTokens.AnyAsync(stored => stored.Vid == StaffVid, token));
    }

    [Fact]
    public async Task TheBootstrapSuperAdministratorPassesThroughTheColumnNotTheFile()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = Private();
        await using var scope = test.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        // What the first start writes for a VID of division.json → superAdmins: a placeholder with the flag, and no name
        // until the person shows up (SuperadminService.BootstrapAsync).
        database.Users.Add(new HubUser
        {
            Vid = BootstrapVid,
            FirstName = string.Empty,
            LastName = string.Empty,
            IsSuperadmin = true,
            SecurityStamp = SuperadminService.NewStamp(),
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        });
        await database.SaveChangesAsync(token);

        try
        {
            // No position of the division at all, and still in.
            var outcome = await scope.ServiceProvider.GetRequiredService<IvaoSignIn>()
                .CompleteAsync(Profile(BootstrapVid), tokens: null, token);

            Assert.NotNull(outcome.Principal);

            var user = await database.Users.AsNoTracking().SingleAsync(row => row.Vid == BootstrapVid, token);
            Assert.True(user.IsSuperadmin);
            Assert.False(user.IsStaff);
            Assert.Equal("Test", user.FirstName);
        }
        finally
        {
            // A super administrator left behind would be one more recipient of what other tests count.
            await database.Users.Where(row => row.Vid == BootstrapVid)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.IsSuperadmin, false), token);
        }
    }

    [Fact]
    public async Task APublicInstallationLetsAMemberIn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();

        var outcome = await scope.ServiceProvider.GetRequiredService<IvaoSignIn>()
            .CompleteAsync(Profile(PublicMemberVid), Tokens(), token);

        Assert.NotNull(outcome.Principal);
        Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>()
            .Users.AnyAsync(user => user.Vid == PublicMemberVid, token));
    }

    [Fact]
    public async Task NoProfileIsStillRefusedAsBefore()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();

        var outcome = await scope.ServiceProvider.GetRequiredService<IvaoSignIn>()
            .CompleteAsync(profile: null, Tokens(), token);

        Assert.Null(outcome.Principal);
        Assert.Equal(IvaoSignIn.NoProfile, outcome.Refusal);
    }

    private static IvaoUserProfile Profile(int vid, params string[] positions) => new(
        Vid: vid,
        FirstName: "Test",
        LastName: "Member",
        PublicNickname: null,
        DivisionCode: null,
        CountryId: null,
        RatingAtc: null,
        RatingPilot: null,
        DiscordId: null,
        Email: "member@example.org",
        LanguageId: "en",
        IvaoIsStaff: positions.Length > 0,
        IvaoIsSupervisor: false,
        StaffPositions: positions);

    private static IvaoUserTokens Tokens() =>
        new("access-token", "refresh-token", DateTime.UtcNow.AddHours(1), "openid");
}
