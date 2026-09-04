using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The identity of the hub end to end: the bootstrap endpoint, the super administrator rules, the
/// security stamp that makes a cookie stale, and the header that has to be there before anything
/// can change state.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AuthenticationTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<int> SeedUserAsync(
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

        if (position is not null)
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
        return vid;
    }

    [Fact]
    public async Task ApiMeAnswersAnonymouslyWithTheDivisionAndNoUser()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = _factory.CreateApiClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/me", token);

        Assert.Equal(JsonValueKind.Null, body.GetProperty("user").ValueKind);
        Assert.Empty(body.GetProperty("permissions").EnumerateArray());
        Assert.Equal("IT", body.GetProperty("division").GetProperty("code").GetString());
        Assert.NotEmpty(body.GetProperty("navigation").GetProperty("public").EnumerateArray());
    }

    [Fact]
    public async Task ApiMeAnswersWithTheIdentityAndTheEffectivePermissionsOnceSignedIn()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(600001, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, 600001, token);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/me", token);
        var user = body.GetProperty("user");

        Assert.Equal(600001, user.GetProperty("vid").GetInt32());
        Assert.True(user.GetProperty("isStaff").GetBoolean());
        Assert.False(user.GetProperty("isSuperadmin").GetBoolean());
        Assert.Contains("ED", user.GetProperty("departments").EnumerateArray().Select(value => value.GetString()));

        var permissions = body.GetProperty("permissions").EnumerateArray()
            .Select(permission => (
                Name: permission.GetProperty("name").GetString(),
                Department: permission.GetProperty("department").GetString()))
            .ToArray();

        Assert.Contains((CorePermissions.ContentPublish, "ED"), permissions);
        Assert.DoesNotContain((CorePermissions.ContentPublish, "FOD"), permissions);
        Assert.DoesNotContain(permissions, permission => permission.Name == CorePermissions.PermissionsManage);
    }

    [Fact]
    public async Task ApiMeSaysWhetherTheUserReachesEveryDepartment()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(600011, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(600012, position: "IT-DIR", cancellationToken: token);

        // A coordinator has one department, and the staff sidebar has to show only that one.
        using var coordinator = _factory.CreateApiClient();
        await _factory.SignInAsync(coordinator, 600011, token);
        var asCoordinator = await coordinator.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.False(asCoordinator.GetProperty("user").GetProperty("hasAllDepartments").GetBoolean());

        // The director reaches all of them, and it is stated rather than left to be worked out from
        // the shape of the permission list, for the same reason the server does not work it out
        // that way (docs/internal/decisions/2026-09-03-reaches-every-department.md).
        using var director = _factory.CreateApiClient();
        await _factory.SignInAsync(director, 600012, token);
        var asDirector = await director.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.True(asDirector.GetProperty("user").GetProperty("hasAllDepartments").GetBoolean());
    }

    [Fact]
    public async Task SecurityStampInvalidatesTheCookie()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(600002, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, 600002, token);

        var signedIn = await client.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Equal(600002, signedIn.GetProperty("user").GetProperty("vid").GetInt32());

        // A grant changed, so the stamp moved: the cookie in the browser is now out of date.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var user = await database.Users.FirstAsync(row => row.Vid == 600002, token);
            user.SecurityStamp = SuperadminService.NewStamp();
            await database.SaveChangesAsync(token);

            scope.ServiceProvider.GetRequiredService<ISecurityStampCache>().Invalidate(600002);
        }

        var afterwards = await client.GetFromJsonAsync<JsonElement>("/api/me", token);

        Assert.Equal(JsonValueKind.Null, afterwards.GetProperty("user").ValueKind);
    }

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/auth/logout")]
    public async Task CsrfHeaderIsRequiredOnAnythingThatChangesState(string path)
    {
        var token = TestContext.Current.CancellationToken;
        using var client = _factory.CreateApiClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        using var refused = await client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task LogoutSucceedsWithTheHeaderAndDropsTheStoredTokens()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(600003, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, 600003, token);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add("X-Requested-With", "hub");
        using var response = await client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var afterwards = await client.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Equal(JsonValueKind.Null, afterwards.GetProperty("user").ValueKind);
    }

    [Fact]
    public async Task LoginRedirectsToIvaoWithTheConfiguredRedirectUri()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = _factory.CreateApiClient();

        using var response = await client.GetAsync("/auth/login?returnUrl=/me", token);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("redirect_uri=http%3A%2F%2Flocalhost%2Fauth%2Fcallback", response.Headers.Location.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRoundTripCookiesAreAcceptableToABrowser()
    {
        // A cookie declared SameSite=None without Secure is rejected outright by every current
        // browser, and the failure surfaces much later as "Correlation failed", which reads like a
        // network problem. Over plain http the two round trip cookies must therefore be Lax.
        var token = TestContext.Current.CancellationToken;
        using var client = _factory.CreateApiClient();

        using var response = await client.GetAsync("/auth/login", token);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];

        Assert.NotEmpty(cookies);
        Assert.DoesNotContain(cookies, cookie =>
            cookie.Contains("samesite=none", StringComparison.OrdinalIgnoreCase)
            && !cookie.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TheApplicationCookieSpellsOutAllThreeOfItsProtections()
    {
        // HttpOnly and SameSite were written out on purpose, "because a default is something that
        // changes with the version of the framework, and this cookie is the only credential the
        // site issues" — and Secure, the third of the three, was the one left to the default.
        // It now follows the scheme of the callback, like the cookies of the OIDC round trip: over
        // https it is Always, and over the plain http a developer signs in on it must not be, or
        // the browser would simply never send it back.
        var token = TestContext.Current.CancellationToken;
        var vid = await SeedUserAsync(700411, cancellationToken: token);

        using var client = _factory.CreateApiClient();
        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            token);

        response.EnsureSuccessStatusCode();

        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("hub.auth=", StringComparison.Ordinal));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);

        // The callback of the test host is http://localhost/auth/callback.
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData("/staff/links", "/staff/links")]
    [InlineData(null, "/")]
    public void OnlyLocalReturnUrlsSurvive(string? returnUrl, string expected)
    {
        Assert.Equal(expected, IvaoAuthenticationExtensions.SafeReturnUrl(returnUrl));
    }

    [Fact]
    public async Task SuperadminIsBootstrappedFromTheDivisionFileOnlyWhenThereIsNoneAtAll()
    {
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var superadmins = scope.ServiceProvider.GetRequiredService<SuperadminService>();

        // The host bootstrapped on start up: the VID of division.json is a super administrator.
        var afterStartup = await superadmins.ListAsync(token);
        Assert.NotEmpty(afterStartup);

        // A second run must change nothing, even if the file were to list somebody else.
        await superadmins.BootstrapAsync(token);
        Assert.Equal(afterStartup, await superadmins.ListAsync(token));

        var hash = await database.DivisionSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.Key == SuperadminService.HashSettingKey, token);
        Assert.NotNull(hash);
    }

    [Fact]
    public async Task TheLastSuperAdministratorCannotBeRemoved()
    {
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var superadmins = scope.ServiceProvider.GetRequiredService<SuperadminService>();

        var all = await superadmins.ListAsync(token);
        var last = all[^1];

        // Take away every other one first, so that the one left really is the last.
        foreach (var vid in all.Where(vid => vid != last))
        {
            await superadmins.RemoveAsync(vid, token);
        }

        // A refusal of the domain, carrying the i18n key of the reason rather than a sentence: the
        // exception handler turns it into the same 400 with a key per field that a validator
        // produces, so the screen shows the reason in the language it is drawing.
        var refused = await Assert.ThrowsAsync<DomainRefusalException>(
            () => superadmins.RemoveAsync(last, token));

        Assert.Equal("vid", refused.Field);
        Assert.Equal("errors.superadmin.lastOne", refused.MessageKey);
        Assert.Contains(last, await superadmins.ListAsync(token));
    }

    [Fact]
    public async Task ASuperAdministratorHoldsTheWholeCatalogueThroughApiMe()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(600004, isSuperadmin: true, cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, 600004, token);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/me", token);

        Assert.True(body.GetProperty("user").GetProperty("isSuperadmin").GetBoolean());
        Assert.Equal(CorePermissions.All.Count, body.GetProperty("permissions").GetArrayLength());
    }
}
