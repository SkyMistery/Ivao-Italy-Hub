using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A member links their Discord account (note 2026-11-05-il-collegamento-con-discord): the real handler, with Discord answered
/// by a double at the far end of the backchannel. VIDs 764001–764004, and accounts of this class's own, so a class running
/// before or after finds nothing of theirs.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class DiscordLinkTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int MemberVid = 764001;
    private const int OtherVid = 764002;
    private const int SuperadminVid = 764003;

    private const string Account = "764001000000000001";
    private const string Username = "member.on.discord";
    private const string Redirect = "http://localhost/auth/discord/callback";

    private static readonly int[] Vids = [MemberVid, OtherVid, SuperadminVid];

    private static readonly Uri Start = new("/auth/discord", UriKind.Relative);
    private static readonly Uri Mine = new(DiscordLinkEndpoints.Pattern, UriKind.Relative);

    private readonly DiscordDouble _discord = new();
    private HubWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _host = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        _host = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Discord:ClientId", "discord-client");
            builder.UseSetting("Discord:ClientSecret", "discord-secret");
            builder.UseSetting("Discord:RedirectUri", Redirect);
            builder.ConfigureTestServices(services =>
                services.Configure<OAuthOptions>(HubClaims.DiscordScheme, options => options.Backchannel = new HttpClient(_discord)));
        });

        await CleanAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(TestContext.Current.CancellationToken);
        await _host.DisposeAsync();
        await _factory.DisposeAsync();
        _discord.Dispose();
    }

    [Fact]
    public async Task TheRoundTripProvesTheAccountAndTheMemberSeesItsName()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        using var member = await SignedInAsync(MemberVid, token);

        // Discord is asked for the name and nothing else, with the redirect that is registered and not one read off the request.
        var query = await LeaveForDiscordAsync(member, token);
        Assert.Equal("discord-client", query["client_id"]);
        Assert.Equal("identify", query["scope"]);
        Assert.Equal(Redirect, query["redirect_uri"]);
        Assert.Equal("S256", query["code_challenge_method"]);

        using var back = await member.GetAsync(Callback(query["state"]!), token);
        Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        Assert.Equal("/me", back.Headers.Location!.OriginalString);

        Assert.Contains($"redirect_uri={Uri.EscapeDataString(Redirect)}", _discord.TokenRequest, StringComparison.Ordinal);
        Assert.Contains("code_verifier=", _discord.TokenRequest, StringComparison.Ordinal);

        var link = await member.GetFromJsonAsync<JsonElement>(Mine, token);
        Assert.True(link.GetProperty("available").GetBoolean());
        Assert.Equal(Username, link.GetProperty("username").GetString());
        Assert.NotEqual(JsonValueKind.Null, link.GetProperty("linkedAt").ValueKind);

        // The member's own screen has no use for the id; the column is what the division's bot reads.
        Assert.False(link.TryGetProperty("discordId", out _));
        var row = await RowAsync(MemberVid, token);
        Assert.Equal(Account, row.DiscordId);

        // It is the member's datum, not a decision: nothing of it in the audit.
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var key = MemberVid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var audit = await database.AuditLog.AsNoTracking()
            .Where(entry => entry.Entity == "hub_users" && entry.EntityId == key)
            .Select(entry => (entry.BeforeJson ?? string.Empty) + (entry.AfterJson ?? string.Empty))
            .ToListAsync(token);
        Assert.DoesNotContain(audit, json => json.Contains(Username, StringComparison.Ordinal) || json.Contains(Account, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ASignInLeavesTheLinkAlone()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        using var member = await SignedInAsync(MemberVid, token);
        await LinkAsync(member, token);

        // ⚠️ It used to write what the IVAO profile said, and the IVAO profile says nothing: a link lasted until the next sign in.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<UserSyncService>().UpsertAsync(
                new IvaoUserProfile(MemberVid, "Discord", "Test", null, "IT", "IT", null, null, null, "en", false, false, []),
                token);
        }

        var row = await RowAsync(MemberVid, token);
        Assert.Equal(Account, row.DiscordId);
        Assert.Equal(Username, row.DiscordUsername);
    }

    [Fact]
    public async Task LinkingAgainBringsTheNameUpToDate()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        using var member = await SignedInAsync(MemberVid, token);
        await LinkAsync(member, token);

        _discord.Username = "a.new.name";
        await LinkAsync(member, token);

        Assert.Equal("a.new.name", (await RowAsync(MemberVid, token)).DiscordUsername);
    }

    [Fact]
    public async Task AnAccountLinkedToAnotherMemberIsRefusedAndNothingMoves()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        await SeedAsync(OtherVid, token);

        using var other = await SignedInAsync(OtherVid, token);
        await LinkAsync(other, token);

        using var member = await SignedInAsync(MemberVid, token);
        var query = await LeaveForDiscordAsync(member, token);
        using var back = await member.GetAsync(Callback(query["state"]!), token);

        Assert.Equal("/me?discord=taken", back.Headers.Location!.OriginalString);
        Assert.Null((await RowAsync(MemberVid, token)).DiscordId);
        Assert.Equal(Account, (await RowAsync(OtherVid, token)).DiscordId);
    }

    [Fact]
    public async Task TheDatabaseHoldsOneAccountToOneMemberEvenWhenTheCheckIsBeaten()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        await SeedAsync(OtherVid, token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var rows = await database.Users.Where(row => row.Vid == MemberVid || row.Vid == OtherVid).ToListAsync(token);
        rows[0].DiscordId = Account;
        rows[1].DiscordId = Account;

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => database.SaveChangesAsync(token));
    }

    [Fact]
    public async Task UnlinkingForgetsAllOfItAtOnce()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        using var member = await SignedInAsync(MemberVid, token);
        await LinkAsync(member, token);

        using var unlinked = await member.DeleteAsync(Mine, token);
        Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);

        var row = await RowAsync(MemberVid, token);
        Assert.Null(row.DiscordId);
        Assert.Null(row.DiscordUsername);
        Assert.Null(row.DiscordLinkedAt);

        var link = await member.GetFromJsonAsync<JsonElement>(Mine, token);
        Assert.True(link.GetProperty("available").GetBoolean());
        Assert.Equal(JsonValueKind.Null, link.GetProperty("username").ValueKind);
    }

    [Fact]
    public async Task ChangingOnesMindOnDiscordIsNotAFailure()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        using var member = await SignedInAsync(MemberVid, token);

        var query = await LeaveForDiscordAsync(member, token);
        using var back = await member.GetAsync(
            new Uri($"/auth/discord/callback?error=access_denied&state={Uri.EscapeDataString(query["state"]!)}", UriKind.Relative),
            token);

        Assert.Equal("/me", back.Headers.Location!.OriginalString);
        Assert.Null((await RowAsync(MemberVid, token)).DiscordId);
    }

    [Fact]
    public async Task DiscordRefusingEndsOnTheProfileWithAFailure()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        using var member = await SignedInAsync(MemberVid, token);

        _discord.Refuses = true;
        var query = await LeaveForDiscordAsync(member, token);
        using var back = await member.GetAsync(Callback(query["state"]!), token);

        Assert.Equal("/me?discord=failed", back.Headers.Location!.OriginalString);
        Assert.Null((await RowAsync(MemberVid, token)).DiscordId);
    }

    [Fact]
    public async Task ACallbackThatDidNotStartHereLinksNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        using var member = await SignedInAsync(MemberVid, token);

        // An attacker's own code, and a state that this browser's correlation cookie knows nothing about.
        using var back = await member.GetAsync(Callback("forged"), token);

        Assert.Equal("/me?discord=failed", back.Headers.Location!.OriginalString);
        Assert.Null(_discord.TokenRequest);
        Assert.Null((await RowAsync(MemberVid, token)).DiscordId);
    }

    [Fact]
    public async Task ASessionThatEndedOnDiscordsPageLinksNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        using var member = await SignedInAsync(MemberVid, token);

        var query = await LeaveForDiscordAsync(member, token);
        using (var loggedOut = await member.PostAsync(new Uri("/auth/logout", UriKind.Relative), content: null, token))
        {
            Assert.True(loggedOut.IsSuccessStatusCode);
        }

        using var back = await member.GetAsync(Callback(query["state"]!), token);

        Assert.Equal("/me?discord=failed", back.Headers.Location!.OriginalString);
        Assert.Null((await RowAsync(MemberVid, token)).DiscordId);
    }

    [Fact]
    public async Task SomebodyNotSignedInIsSentToSignInFirst()
    {
        var token = TestContext.Current.CancellationToken;
        using var anonymous = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await anonymous.GetAsync(Start, token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/auth/login", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInstallationWithoutDiscordOffersNothingAndStillLetsAMemberLeave()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        using var linked = await SignedInAsync(MemberVid, token);
        await LinkAsync(linked, token);

        // The same database, on a host with no section: the factory itself has none.
        using var member = await SignedInAsync(_factory, MemberVid, token);

        using var start = await member.GetAsync(Start, token);
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);

        var link = await member.GetFromJsonAsync<JsonElement>(Mine, token);
        Assert.False(link.GetProperty("available").GetBoolean());
        Assert.Equal(Username, link.GetProperty("username").GetString());

        using var unlinked = await member.DeleteAsync(Mine, token);
        Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);
        Assert.Null((await RowAsync(MemberVid, token)).DiscordId);

        // The handler answers nothing on this host: no round trip ends here.
        using var callback = await member.GetAsync(Callback("anything"), token);
        Assert.NotEqual("/me?discord=failed", callback.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AnErasureTakesTheLinkAwayAndTheAccountIsFreeAgain()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(MemberVid, token);
        await SeedAsync(SuperadminVid, token, superadmin: true);
        using (var member = await SignedInAsync(MemberVid, token))
        {
            await LinkAsync(member, token);
        }

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        using var erased = await superadmin.PostAsync(new Uri($"{ErasureEndpoints.Pattern}/{MemberVid}", UriKind.Relative), content: null, token);
        Assert.Equal(HttpStatusCode.OK, erased.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.False(await database.Users.AnyAsync(row => row.DiscordId == Account, token));
        Assert.False(await database.AuditLog.AnyAsync(
            entry => (entry.BeforeJson != null && entry.BeforeJson.Contains(Username)) || (entry.AfterJson != null && entry.AfterJson.Contains(Username)),
            token));
    }

    private static Uri Callback(string state) =>
        new($"/auth/discord/callback?code=a-code&state={Uri.EscapeDataString(state)}", UriKind.Relative);

    private static async Task<System.Collections.Specialized.NameValueCollection> LeaveForDiscordAsync(HttpClient member, CancellationToken cancellationToken)
    {
        using var response = await member.GetAsync(Start, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var target = response.Headers.Location!;
        Assert.Equal("discord.com", target.Host);
        return HttpUtility.ParseQueryString(target.Query);
    }

    private static async Task LinkAsync(HttpClient member, CancellationToken cancellationToken)
    {
        var query = await LeaveForDiscordAsync(member, cancellationToken);
        using var back = await member.GetAsync(Callback(query["state"]!), cancellationToken);
        Assert.Equal("/me", back.Headers.Location!.OriginalString);
    }

    private Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken) => SignedInAsync(_host, vid, cancellationToken);

    private static async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> host, int vid, CancellationToken cancellationToken)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        using var signIn = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);
        signIn.EnsureSuccessStatusCode();
        return client;
    }

    private async Task<HubUser> RowAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Users.AsNoTracking().FirstAsync(row => row.Vid == vid, cancellationToken);
    }

    private async Task SeedAsync(int vid, CancellationToken cancellationToken, bool superadmin = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        database.Users.Add(new HubUser
        {
            Vid = vid,
            FirstName = "Discord",
            LastName = "Test",
            IsSuperadmin = superadmin,
            SecurityStamp = SuperadminService.NewStamp(),
            Locale = "en",
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await database.Users.Where(row => Vids.Contains(row.Vid)).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Discord at the far end of the backchannel: the token endpoint and the profile, and what it was asked.</summary>
    private sealed class DiscordDouble : HttpMessageHandler
    {
        public string Username { get; set; } = DiscordLinkTests.Username;

        public bool Refuses { get; set; }

        public string? TokenRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Refuses)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (request.Method == HttpMethod.Post)
            {
                TokenRequest = await request.Content!.ReadAsStringAsync(cancellationToken);
                return Json("""{"access_token":"a-token","token_type":"Bearer","expires_in":604800,"scope":"identify"}""");
            }

            Assert.Equal("a-token", request.Headers.Authorization?.Parameter);
            return Json(JsonSerializer.Serialize(new { id = Account, username = Username, discriminator = "0", global_name = "Somebody" }));
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }
}
