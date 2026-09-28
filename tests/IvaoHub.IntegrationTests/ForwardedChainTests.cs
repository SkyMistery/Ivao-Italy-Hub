using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
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
/// The address of the visitor behind more than one proxy (note 2026-09-28-la-catena-dei-proxy).
/// <para>The test installation showed the chain as it really arrives: <c>X-Forwarded-For</c> in two header lines, the
/// visitor and a Cloudflare node on the first, the web server in front of Passenger (<c>127.0.0.1</c>) on the second,
/// and <c>X-Forwarded-Proto</c> with two entries only. With the default limit of one hop the hub believed
/// <c>127.0.0.1</c> for everybody: one login counter for the whole site, and an audit log that said nothing. These tests
/// send that chain, with documentation addresses, from a neighbour the host believes.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ForwardedChainTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // Its own range, because the suite shares one database and a VID is a row.
    private const int SuperadminVid = 646001;

    /// <summary>The web server in front of Passenger, which is the hub's neighbour.</summary>
    private const string Loopback = "127.0.0.1";

    /// <summary>A node inside one of Cloudflare's published ranges, which the secrets file lists.</summary>
    private const string ProxyNode = "172.68.10.20";

    private const string Visitor = "198.51.100.7";

    private const string OtherVisitor = "198.51.100.8";

    /// <summary>Somebody who calls the origin directly, bypassing the proxies: no list trusts it.</summary>
    private const string DirectCaller = "192.0.2.50";

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheVisitorIsFoundBehindTheWholeChain()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedSuperadminAsync(token);

        await using var host = BehindTheProxies();
        using var client = await SignedInClientAsync(host, token);

        using var request = Request(HttpMethod.Get, RequestDiagnosticsEndpoints.Pattern, Chain(Visitor));
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(token);

        // Two lines and three addresses arrived, and nothing was skipped because the schemes are one fewer.
        var forwardedFor = Header(body, "X-Forwarded-For");
        Assert.Equal(2, forwardedFor.GetProperty("values").GetArrayLength());
        Assert.Equal(3, forwardedFor.GetProperty("entries").GetInt32());
        Assert.Equal(2, Header(body, "X-Forwarded-Proto").GetProperty("entries").GetInt32());

        // 127.0.0.1 is trusted, so is the node before it, and the visitor is the first address nobody vouches for.
        Assert.Equal(Loopback, body.GetProperty("neighbour").GetProperty("address").GetString());
        Assert.True(body.GetProperty("addressForwarded").GetBoolean());
        Assert.Equal(Visitor, body.GetProperty("believed").GetProperty("address").GetString());

        Assert.Equal("http", body.GetProperty("schemeReceived").GetString());
        Assert.Equal("https", body.GetProperty("scheme").GetString());
        Assert.True(body.GetProperty("isHttps").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("forwarding").GetProperty("forwardLimit").ValueKind);
    }

    [Fact]
    public async Task AForgedChainStopsAtTheCallerWhoSentIt()
    {
        // A caller that reaches the origin directly writes whatever it likes, a trusted looking node included; the web
        // server in front of Passenger then appends the caller's own address. The walk stops there, at the first address
        // the list does not trust, whatever stands to its left.
        var token = TestContext.Current.CancellationToken;
        await SeedSuperadminAsync(token);

        await using var host = BehindTheProxies();
        using var client = await SignedInClientAsync(host, token);

        using var request = Request(HttpMethod.Get, RequestDiagnosticsEndpoints.Pattern,
            ("X-Forwarded-For", [$"203.0.113.66, {ProxyNode}, {DirectCaller}", Loopback]),
            ("X-Forwarded-Proto", ["https"]));
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(token);

        Assert.Equal(DirectCaller, body.GetProperty("believed").GetProperty("address").GetString());
    }

    [Fact]
    public async Task TheLoginLimitCountsEachVisitorApart()
    {
        // Ten a minute per address. With one hop every visitor was 127.0.0.1, and the tenth login of the evening shut
        // the door on the whole division.
        var token = TestContext.Current.CancellationToken;

        await using var host = BehindTheProxies();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            using var allowed = await LoginAsync(client, Visitor, token);
            Assert.Equal(HttpStatusCode.Found, allowed.StatusCode);
        }

        using var refused = await LoginAsync(client, Visitor, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        using var other = await LoginAsync(client, OtherVisitor, token);
        Assert.Equal(HttpStatusCode.Found, other.StatusCode);
    }

    [Fact]
    public async Task TheAuditLogRecordsTheVisitor()
    {
        // The measure Carmine made on the test installation, as a test: a link created and deleted, and the ip column of
        // its audit rows.
        var token = TestContext.Current.CancellationToken;
        await SeedSuperadminAsync(token);

        await using var host = BehindTheProxies();
        using var client = await SignedInClientAsync(host, token);

        using var creating = Request(HttpMethod.Post, LinksEndpoints.Pattern, Chain(Visitor));
        creating.Content = JsonContent.Create(new
        {
            ownerDepartment = nameof(Department.WD),
            visibility = nameof(Visibility.Public),
            title = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Indirizzo", ["en"] = "Address" },
            url = "https://example.invalid/forwarded-chain",
            description = (Dictionary<string, string>?)null,
            category = (string?)null,
            sort = 0,
            isActive = true,
            rowVersion = "0001-01-01T00:00:00",
        });
        using var created = await client.SendAsync(creating, token);
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        using var deleting = Request(HttpMethod.Delete, $"{LinksEndpoints.Pattern}/{id}", Chain(Visitor));
        using var deleted = await client.SendAsync(deleting, token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var key = id.ToString(CultureInfo.InvariantCulture);

        var audits = await database.AuditLog.AsNoTracking()
            .Where(entry => entry.Entity == "cms_links" && entry.EntityId == key)
            .ToListAsync(token);

        Assert.Equal(["created", "deleted"], audits.Select(entry => entry.Action).Order(StringComparer.Ordinal));
        Assert.All(audits, entry => Assert.Equal(Visitor, entry.Ip));
    }

    // --- helpers ------------------------------------------------------------------------------

    /// <summary>
    /// A host that trusts what the test installation's secrets file lists: the loopback and the proxy's published
    /// ranges (one of them is enough here).
    /// </summary>
    private WebApplicationFactory<Program> BehindTheProxies() => _factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting($"{HubConfiguration.TrustedProxiesKey}:0", "127.0.0.1/32");
        builder.UseSetting($"{HubConfiguration.TrustedProxiesKey}:1", "::1/128");
        builder.UseSetting($"{HubConfiguration.TrustedProxiesKey}:2", "172.64.0.0/13");
        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, TestPeerStartupFilter>());
    });

    /// <summary>The chain as the test installation delivers it, for one visitor.</summary>
    private static (string Name, string[] Lines)[] Chain(string visitor) =>
    [
        ("X-Forwarded-For", [$"{visitor}, {ProxyNode}", Loopback]),
        ("X-Forwarded-Proto", ["https", "https"]),
    ];

    /// <summary>A request from the web server in front of Passenger, each header in as many lines as given.</summary>
    private static HttpRequestMessage Request(
        HttpMethod method,
        string address,
        params (string Name, string[] Lines)[] headers)
    {
        var request = new HttpRequestMessage(method, new Uri(address, UriKind.Relative));
        request.Headers.Add(TestPeerStartupFilter.Header, Loopback);
        request.Headers.Add("X-Requested-With", HubPipeline.RequestedWithValue);
        foreach (var (name, lines) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, lines);
        }

        return request;
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string visitor, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, "/auth/login", Chain(visitor));
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>The factory's own sign in, for a host derived from it with <c>WithWebHostBuilder</c>.</summary>
    private static async Task<HttpClient> SignedInClientAsync(WebApplicationFactory<Program> host, CancellationToken cancellationToken)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={SuperadminVid}", UriKind.Relative),
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return client;
    }

    private static JsonElement Header(JsonElement body, string name) =>
        body.GetProperty("headers").EnumerateArray()
            .Single(header => string.Equals(header.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase));

    private async Task SeedSuperadminAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == SuperadminVid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = SuperadminVid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "User";
        user.IsSuperadmin = true;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }
}
