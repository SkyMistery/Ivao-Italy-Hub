using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using IvaoHub.Web;
using IvaoHub.Web.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// How the hub sees a request, for a super administrator installing it behind proxies (note
/// 2026-09-28-l-indirizzo-del-visitatore-dietro-i-proxy, §8). The page has to tell two stories apart: the address never
/// arrived, or it arrived and was not believed. So the tests put a neighbour of their choosing under the request and read
/// back what the forwarded headers middleware made of it.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class RequestDiagnosticsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 645001;
    private const int DirectorVid = 645002;
    private const int MemberVid = 645003;

    /// <summary>The proxy in front of the test host, the only neighbour it believes.</summary>
    private const string TrustedPeer = "10.20.30.40";

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task OnlyASuperAdministratorReadsIt()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(DirectorVid, position: "IT-DIR", cancellationToken: token);
        await SeedUserAsync(MemberVid, cancellationToken: token);

        using var anonymous = _factory.CreateApiClient();
        using var nobody = await anonymous.GetAsync(Address, token);
        Assert.Equal(HttpStatusCode.Unauthorized, nobody.StatusCode);

        using var member = _factory.CreateApiClient();
        await _factory.SignInAsync(member, MemberVid, token);
        using var refusedMember = await member.GetAsync(Address, token);
        Assert.Equal(HttpStatusCode.Forbidden, refusedMember.StatusCode);

        // Every global permission of the catalogue, and still not enough: what the proxies pass on is the system's.
        using var director = _factory.CreateApiClient();
        await _factory.SignInAsync(director, DirectorVid, token);
        using var refusedDirector = await director.GetAsync(Address, token);
        Assert.Equal(HttpStatusCode.Forbidden, refusedDirector.StatusCode);
    }

    [Fact]
    public async Task WithoutTrustedNetworksNothingIsForwardedAndTheHeadersStillShow()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        using var request = new HttpRequestMessage(HttpMethod.Get, Address);
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");
        using var response = await client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(token);

        // The development host runs no forwarded headers middleware at all: the header arrives and nobody believes it.
        Assert.False(body.GetProperty("forwarding").GetProperty("inPipeline").GetBoolean());
        Assert.False(body.GetProperty("addressForwarded").GetBoolean());
        Assert.Equal(1, Header(body, "X-Forwarded-For").GetProperty("entries").GetInt32());

        // Every forwarding header is listed, the absent ones too: "not there" is the answer to half the question.
        foreach (var name in new[] { "X-Forwarded-Proto", "X-Forwarded-Host", "X-Real-IP", "Forwarded" })
        {
            Assert.Equal(0, Header(body, name).GetProperty("entries").GetInt32());
        }

        // Names, never values, for everything else: the cookie is listed by name, and only the five carry a value.
        Assert.Contains("Cookie", Names(body), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(5, body.GetProperty("headers").GetArrayLength());
    }

    [Fact]
    public async Task AChainFromATrustedPeerIsShownRawAndAsBelieved()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        await using var host = BehindAProxy();
        using var client = SignedInClient(host);
        await SignInAsync(client, SuperadminVid, token);

        var body = await ReadAsync(client, TrustedPeer, token, ("X-Forwarded-For", "203.0.113.7, 198.51.100.9"),
            ("X-Forwarded-Proto", "https"), ("X-Visitor-Address", "203.0.113.7"));

        var neighbour = body.GetProperty("neighbour");
        Assert.Equal(TrustedPeer, neighbour.GetProperty("address").GetString());
        Assert.Equal("IPv4", neighbour.GetProperty("family").GetString());

        // The walk stops at the rightmost entry, which is nobody the list trusts; the raw header still has both.
        Assert.True(body.GetProperty("addressForwarded").GetBoolean());
        Assert.Equal("198.51.100.9", body.GetProperty("believed").GetProperty("address").GetString());
        var forwardedFor = Header(body, "X-Forwarded-For");
        Assert.Equal(2, forwardedFor.GetProperty("entries").GetInt32());
        Assert.Equal("203.0.113.7, 198.51.100.9", forwardedFor.GetProperty("values")[0].GetString());

        Assert.True(body.GetProperty("schemeForwarded").GetBoolean());
        Assert.Equal("http", body.GetProperty("schemeReceived").GetString());
        Assert.Equal("https", body.GetProperty("scheme").GetString());
        Assert.True(body.GetProperty("isHttps").GetBoolean());

        // A header the installation named shows its value; one that carries a credential never does, whatever is named.
        Assert.Equal("203.0.113.7", Header(body, "X-Visitor-Address").GetProperty("values")[0].GetString());
        Assert.DoesNotContain(
            body.GetProperty("headers").EnumerateArray(),
            header => string.Equals(header.GetProperty("name").GetString(), "Cookie", StringComparison.OrdinalIgnoreCase));

        var forwarding = body.GetProperty("forwarding");
        Assert.True(forwarding.GetProperty("inPipeline").GetBoolean());
        Assert.Equal(JsonValueKind.Null, forwarding.GetProperty("forwardLimit").ValueKind);
        Assert.Equal($"{TrustedPeer}/32", Assert.Single(forwarding.GetProperty("trustedNetworks").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task AnUntrustedPeerIsNotBelievedAndSaysSo()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        await using var host = BehindAProxy();
        using var client = SignedInClient(host);
        await SignInAsync(client, SuperadminVid, token);

        var body = await ReadAsync(client, "10.20.30.41", token,
            ("X-Forwarded-For", "203.0.113.7"), ("X-Forwarded-Proto", "https"),
            ("X-Original-For", "192.0.2.1:1"));

        // An X-Original-For the caller made up does not pass for the middleware's own.
        Assert.False(body.GetProperty("addressForwarded").GetBoolean());
        Assert.False(body.GetProperty("schemeForwarded").GetBoolean());
        Assert.Equal("10.20.30.41", body.GetProperty("believed").GetProperty("address").GetString());
        Assert.Equal("http", body.GetProperty("scheme").GetString());
        Assert.Equal(1, Header(body, "X-Forwarded-For").GetProperty("entries").GetInt32());
    }

    [Fact]
    public async Task AnIPv4NeighbourWrittenAsIPv6IsNamedAndStillTrusted()
    {
        // The second hypothesis of the note: a neighbour seen as ::ffff:127.0.0.1 while the list says 127.0.0.1/32. The
        // page names the family, and the middleware of ASP.NET Core 10 maps it back before comparing — measured here.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        await using var host = BehindAProxy();
        using var client = SignedInClient(host);
        await SignInAsync(client, SuperadminVid, token);

        var body = await ReadAsync(client, $"::ffff:{TrustedPeer}", token, ("X-Forwarded-For", "203.0.113.7"));

        Assert.Equal("IPv4-mapped IPv6", body.GetProperty("neighbour").GetProperty("family").GetString());
        Assert.True(body.GetProperty("addressForwarded").GetBoolean());
        Assert.Equal("203.0.113.7", body.GetProperty("believed").GetProperty("address").GetString());
    }

    // --- helpers ------------------------------------------------------------------------------

    private static readonly Uri Address = new(RequestDiagnosticsEndpoints.Pattern, UriKind.Relative);

    /// <summary>A host that believes one proxy, and names one more header to show — plus one it must never show.</summary>
    private WebApplicationFactory<Program> BehindAProxy() => _factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting($"{HubConfiguration.TrustedProxiesKey}:0", $"{TrustedPeer}/32");
        builder.UseSetting($"{RequestDiagnosticsEndpoints.ExtraHeadersKey}:0", "X-Visitor-Address");
        builder.UseSetting($"{RequestDiagnosticsEndpoints.ExtraHeadersKey}:1", "Cookie");
        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, TestPeerStartupFilter>());
    });

    private static HttpClient SignedInClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>The factory's own sign in, for a host derived from it with <c>WithWebHostBuilder</c>.</summary>
    private static async Task SignInAsync(HttpClient client, int vid, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> ReadAsync(
        HttpClient client,
        string peer,
        CancellationToken cancellationToken,
        params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Address);
        request.Headers.Add(TestPeerStartupFilter.Header, peer);
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static JsonElement Header(JsonElement body, string name) =>
        body.GetProperty("headers").EnumerateArray()
            .Single(header => string.Equals(header.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase));

    private static string[] Names(JsonElement body) =>
        [.. body.GetProperty("headerNames").EnumerateArray().Select(name => name.GetString()!)];

    private async Task SeedUserAsync(
        int vid,
        bool isSuperadmin = false,
        string? position = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = vid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "User";
        user.IsSuperadmin = isSuperadmin;
        user.IsStaff = position is not null;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(
                row => row.Vid == vid && row.Position == position,
                cancellationToken))
        {
            var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = position,
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
