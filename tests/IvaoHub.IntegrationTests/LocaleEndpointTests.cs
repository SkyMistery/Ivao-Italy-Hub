using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The language a member reads the hub in. Two things remember it and they are not the same thing:
/// the browser keeps <c>hub.lang</c>, the row keeps the member's own choice. This endpoint owns the
/// second one, and reissues the cookie so that the server starts answering in it straight away.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class LocaleEndpointTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

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
        user.LastName = "User";
        user.Locale = "en";
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }

    private static HttpRequestMessage Put(string locale)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, LocaleEndpoints.Pattern)
        {
            Content = JsonContent.Create(new { locale }),
        };

        // The guard demands it on anything that changes state.
        request.Headers.Add("X-Requested-With", "hub");
        return request;
    }

    [Fact]
    public async Task ChoosingALanguageStoresItAndAnswersInItFromThenOn()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(610001, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, 610001, token);

        using var request = Put("it");
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("it", body.GetProperty("locale").GetString());

        // Stored, so it follows the member to another browser.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var user = await database.Users.FirstAsync(row => row.Vid == 610001, token);
            Assert.Equal("it", user.Locale);
        }

        // And in the cookie, so the very next answer is already in the new language rather than
        // waiting for a sign out. This is what the reissue is for.
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Equal("it", me.GetProperty("user").GetProperty("locale").GetString());
    }

    [Fact]
    public async Task ARegionalTagIsStoredAsTheDivisionSpellsIt()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(610002, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, 610002, token);

        using var request = Put("it-IT");
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("it", body.GetProperty("locale").GetString());
    }

    [Fact]
    public async Task ALanguageTheDivisionDoesNotSpeakIsRefusedWithAKeyOnTheField()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(610003, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, 610003, token);

        using var request = Put("de");
        using var response = await client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        var keys = problem.GetProperty("errors").GetProperty("locale").EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        // An i18n key, never a sentence: the browser is what knows the language it is showing.
        Assert.Equal([LocaleEndpoints.UnsupportedKey], keys);
    }

    [Fact]
    public async Task AnonymousCannotSetALanguage()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = _factory.CreateApiClient();

        using var request = Put("it");
        using var response = await client.SendAsync(request, token);

        // 401 and not a redirect to the consent screen: the policy names the application cookie.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithoutTheHeaderItIsRefusedLikeEveryOtherWrite()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(610004, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, 610004, token);

        using var request = new HttpRequestMessage(HttpMethod.Put, LocaleEndpoints.Pattern)
        {
            Content = JsonContent.Create(new { locale = "it" }),
        };

        using var response = await client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
