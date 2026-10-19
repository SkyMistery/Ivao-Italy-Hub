using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The core's limit on the writing verbs of members (note 2026-10-19-il-limite-dei-verbi-dei-membri), through a member group of a
/// module that applies it by name: past the limit of the window the next write is answered 429 with the core's title, a read still
/// passes, and another member is counted apart. The limit is lowered to three for the host of this class only. VIDs 763001–763002;
/// the writes go to a training that does not exist, so no row is written.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class MemberWriteLimitTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int MemberVid = 763001;
    private const int OtherMemberVid = 763002;

    private const int Limit = 3;

    /// <summary>The cancel of a training nobody has: a write of a member group that finds nothing to write, 404.</summary>
    private static readonly Uri Cancel = new($"{RequestEndpoints.Pattern}/999999999/cancel", UriKind.Relative);

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task TheWriteAfterTheLimitIsRefusedAndAnotherMemberIsCountedApart()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(MemberVid, token);
        await SeedUserAsync(OtherMemberVid, token);

        await using var host = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting($"{MemberWriteLimitOptions.SectionName}:PermitLimit", Limit.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        using var member = await SignedInAsync(host, MemberVid, token);
        for (var write = 0; write < Limit; write++)
        {
            using var allowed = await WriteAsync(member, token);
            Assert.Equal(HttpStatusCode.NotFound, allowed.StatusCode);
        }

        using var refused = await WriteAsync(member, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);
        Assert.True(refused.Headers.Contains("Retry-After"));

        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(token);
        var expected = _factory.Services.GetRequiredService<LocaleCatalog>().Resolve("en", HubRateLimits.TooManyRequestsTitleKey);
        Assert.NotEqual(HubRateLimits.TooManyRequestsTitleKey, expected);
        Assert.Equal(expected, problem.GetProperty("title").GetString());
        Assert.Equal(429, problem.GetProperty("status").GetInt32());

        // A read of the same group is never counted.
        using var read = await member.GetAsync(new Uri(RequestEndpoints.Pattern, UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // Another member, from the same address, has a window of their own.
        using var other = await SignedInAsync(host, OtherMemberVid, token);
        using var otherWrite = await WriteAsync(other, token);
        Assert.Equal(HttpStatusCode.NotFound, otherWrite.StatusCode);
    }

    /// <summary>
    /// Every writing verb of a member's own addresses (<c>/api/…/mine…</c>) carries the limit: a member group added later without it
    /// is caught here, not by a mail server.
    /// </summary>
    [Fact]
    public void EveryWritingVerbOfAMembersOwnAddressesCarriesTheLimit()
    {
        var writes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("/mine", StringComparison.Ordinal) == true)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                .Any(method => !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method)) == true)
            .ToList();

        Assert.NotEmpty(writes);
        Assert.All(writes, endpoint => Assert.True(
            endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == HubRateLimits.MemberWrites,
            $"{endpoint.RoutePattern.RawText} writes without the members' limit."));
    }

    /// <summary>
    /// The cancel with a body: an endpoint that takes JSON is not even matched by a request without one, and a request no endpoint
    /// matches has no limit to count against.
    /// </summary>
    private static Task<HttpResponseMessage> WriteAsync(HttpClient client, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(Cancel, new { }, cancellationToken);

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

    private async Task SeedUserAsync(int vid, CancellationToken cancellationToken)
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
        user.LastName = "Member";
        user.Locale = "en";
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }
}
