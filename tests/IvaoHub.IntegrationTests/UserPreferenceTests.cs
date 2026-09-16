using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Preferences;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A member's preferences (M2, T4b): chosen on one computer and found on another — another cookie of
/// the same member — and never by somebody else; a key no module declares and a value the module
/// would not read back are refused.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class UserPreferenceTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13).
    private const int MemberVid = 780046;
    private const int OtherMemberVid = 780047;

    private HubWebApplicationFactory _factory = null!;

    private static readonly Uri OrderUri = new(
        UserPreferenceEndpoints.Pattern.Replace("{key}", SampleModule.OrderPreference, StringComparison.Ordinal),
        UriKind.Relative);

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedMemberAsync(MemberVid, token);
        await SeedMemberAsync(OtherMemberVid, token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task APreferenceIsFoundFromAnotherCookieOfTheSameMemberAndNotBySomebodyElse()
    {
        var token = TestContext.Current.CancellationToken;

        using var here = await SignedInAsync(MemberVid, token);
        using var elsewhere = await SignedInAsync(MemberVid, token);
        using var somebodyElse = await SignedInAsync(OtherMemberVid, token);

        using (var saved = await here.PutAsJsonAsync(OrderUri, new { value = "tour" }, token))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        }

        Assert.Equal("tour", (await ValueAsync(elsewhere, token)).GetString());

        // Somebody who never chose gets no value, and not a 404: the module has its default.
        Assert.Equal(JsonValueKind.Null, (await ValueAsync(somebodyElse, token)).ValueKind);

        // Changed again, rewritten in place.
        using (var changed = await elsewhere.PutAsJsonAsync(OrderUri, new { value = "date" }, token))
        {
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        }

        Assert.Equal("date", (await ValueAsync(here, token)).GetString());
    }

    [Fact]
    public async Task AnUndeclaredKeyAndAValueTheModuleCannotReadAreRefused()
    {
        var token = TestContext.Current.CancellationToken;
        using var member = await SignedInAsync(MemberVid, token);

        using (var unknown = await member.GetAsync(new Uri("/api/me/preferences/sample.nothing", UriKind.Relative), token))
        {
            await AssertRefusedAsync(unknown, "key", UserPreferenceEndpoints.UnknownKeyKey, token);
        }

        using (var invalid = await member.PutAsJsonAsync(OrderUri, new { value = "alphabetical" }, token))
        {
            await AssertRefusedAsync(invalid, "value", UserPreferenceEndpoints.InvalidValueKey, token);
        }

        using (var wrongShape = await member.PutAsJsonAsync(OrderUri, new { value = new[] { "date" } }, token))
        {
            await AssertRefusedAsync(wrongShape, "value", UserPreferenceEndpoints.InvalidValueKey, token);
        }

        using var anonymous = _factory.CreateApiClient();
        using var refused = await anonymous.GetAsync(OrderUri, token);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    private static async Task<JsonElement> ValueAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var preference = await client.GetFromJsonAsync<JsonElement>(OrderUri, cancellationToken);
        Assert.Equal(SampleModule.OrderPreference, preference.GetProperty("key").GetString());
        return preference.GetProperty("value");
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Contains(key, problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(error => error.GetString()));
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task SeedMemberAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = vid, CreatedAt = clock.UtcNow, SecurityStamp = SuperadminService.NewStamp() };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "Preferences";
        user.UpdatedAt = clock.UtcNow;

        // A member who chose before, in an earlier run against the same database, starts from nothing.
        await database.UserPreferences.Where(row => row.Vid == vid).ExecuteDeleteAsync(cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }
}
