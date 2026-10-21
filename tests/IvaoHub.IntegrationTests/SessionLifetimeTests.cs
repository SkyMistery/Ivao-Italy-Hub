using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A session has a maximum lifetime, and a permission that ends stops counting: a cookie older than the maximum is refused
/// however it was used, a cookie computed with a grant that has since ended is refused, and a sign-in that finds other
/// positions refuses the cookies of the person's other devices. Each time the next sign-in computes the permissions again.
/// And logout ends the session everywhere: no cookie of that person, a copy of the one that signed out included, opens anything
/// afterwards.
/// <para>The host's clock is moved and the cookie's own twelve sliding hours are not: what refuses the cookie here is the
/// hub's check, never the framework's expiry.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class SessionLifetimeTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int LongSessionVid = 791_001;
    private const int GrantedVid = 791_002;
    private const int TwoDevicesVid = 791_003;
    private const int LogoutVid = 791_004;

    private readonly MovableClock _clock = new();
    private HubWebApplicationFactory _base = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public ValueTask InitializeAsync()
    {
        _base = new HubWebApplicationFactory(mariaDb.ConnectionString);
        _factory = _base.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IClock>(_clock)));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _base.DisposeAsync();
    }

    [Fact]
    public async Task ACookieIsRefusedOnceItsSignInIsOlderThanTheMaximum()
    {
        var token = TestContext.Current.CancellationToken;
        await SignInWithIvaoAsync(LongSessionVid, [$"{Code}-EC"], token);

        using var client = CreateClient();
        await SignInAsync(client, LongSessionVid, token);
        Assert.Equal(LongSessionVid, await SignedInVidAsync(client, token));

        // Used every day for a month, it is still the same sign-in: one day short of the maximum it is believed...
        var maximum = SignInLifetimeOptions.DefaultMaximumDays;
        _clock.Offset = TimeSpan.FromDays(maximum - 1);
        Assert.Equal(LongSessionVid, await SignedInVidAsync(client, token));

        // ...and past it, refused.
        _clock.Offset = TimeSpan.FromDays(maximum) + TimeSpan.FromMinutes(1);
        Assert.Null(await SignedInVidAsync(client, token));

        // A new sign-in starts the count again.
        await SignInAsync(client, LongSessionVid, token);
        Assert.Equal(LongSessionVid, await SignedInVidAsync(client, token));
    }

    [Fact]
    public async Task TheMaximumIsReadFromTheConfiguration()
    {
        await using var configured = _base.WithWebHostBuilder(builder =>
            builder.UseSetting($"{SignInLifetimeOptions.SectionName}:{nameof(SignInLifetimeOptions.MaximumDays)}", "7"));

        var options = configured.Services.GetRequiredService<IOptions<SignInLifetimeOptions>>().Value;

        Assert.Equal(TimeSpan.FromDays(7), options.MaximumLifetime);
        Assert.Equal(
            TimeSpan.FromDays(SignInLifetimeOptions.DefaultMaximumDays),
            _factory.Services.GetRequiredService<IOptions<SignInLifetimeOptions>>().Value.MaximumLifetime);
    }

    [Fact]
    public async Task AGrantThatEndsNoLongerCountsForACookieSignedInBefore()
    {
        var token = TestContext.Current.CancellationToken;
        await SignInWithIvaoAsync(GrantedVid, [$"{Code}-EC"], token);
        await GrantUntilAsync(GrantedVid, _clock.UtcNow.AddHours(2), token);

        using var client = CreateClient();
        await SignInAsync(client, GrantedVid, token);
        Assert.Contains((CorePermissions.LinksEdit, nameof(Department.FOD)), await PermissionsAsync(client, token));

        _clock.Offset = TimeSpan.FromHours(1);
        Assert.Contains((CorePermissions.LinksEdit, nameof(Department.FOD)), await PermissionsAsync(client, token));

        // The grant has ended: the cookie that carries it is refused...
        _clock.Offset = TimeSpan.FromHours(3);
        Assert.Null(await SignedInVidAsync(client, token));

        // ...and the next sign-in computes the permissions without it.
        await SignInAsync(client, GrantedVid, token);
        Assert.Equal(GrantedVid, await SignedInVidAsync(client, token));
        Assert.DoesNotContain((CorePermissions.LinksEdit, nameof(Department.FOD)), await PermissionsAsync(client, token));
    }

    [Fact]
    public async Task ASignInWithOtherPositionsRefusesTheCookiesOfTheOtherDevices()
    {
        var token = TestContext.Current.CancellationToken;
        await SignInWithIvaoAsync(TwoDevicesVid, [$"{Code}-EC"], token);

        using var other = CreateClient();
        await SignInAsync(other, TwoDevicesVid, token);
        Assert.Equal(TwoDevicesVid, await SignedInVidAsync(other, token));

        // The same positions: the other device is left alone, a sign-in is not a sign-out.
        await SignInWithIvaoAsync(TwoDevicesVid, [$"{Code}-EC"], token);
        Assert.Equal(TwoDevicesVid, await SignedInVidAsync(other, token));

        // Other positions: what the other device's cookie says is no longer true, and it is refused.
        await SignInWithIvaoAsync(TwoDevicesVid, [$"{Code}-EAC"], token);
        Assert.Null(await SignedInVidAsync(other, token));
    }

    [Fact]
    public async Task LogoutEndsTheSessionOnEveryDevice()
    {
        var token = TestContext.Current.CancellationToken;
        await SignInWithIvaoAsync(LogoutVid, [$"{Code}-EC"], token);

        // The cookie this device holds, kept aside as it was before the logout...
        using var device = CreateClient(handleCookies: false);
        var copy = await SignInForCookieAsync(device, LogoutVid, token);
        Assert.Equal(LogoutVid, await SignedInVidAsync(device, token, copy));

        // ...and another device of the same person.
        using var other = CreateClient();
        await SignInAsync(other, LogoutVid, token);
        Assert.Equal(LogoutVid, await SignedInVidAsync(other, token));

        using (var logout = new HttpRequestMessage(HttpMethod.Post, "/auth/logout"))
        {
            logout.Headers.Add("Cookie", copy);
            logout.Headers.Add("X-Requested-With", "hub");
            using var response = await device.SendAsync(logout, token);
            Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Null(await SignedInVidAsync(device, token, copy));
        Assert.Null(await SignedInVidAsync(other, token));

        // Signing in again works as ever.
        await SignInAsync(other, LogoutVid, token);
        Assert.Equal(LogoutVid, await SignedInVidAsync(other, token));
    }

    private string Code => _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value.Code;

    private HttpClient CreateClient(bool handleCookies = true) => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = handleCookies,
    });

    /// <summary>Signs in on a client that keeps no cookies, and answers the application cookie as a Cookie header.</summary>
    private static async Task<string> SignInForCookieAsync(HttpClient client, int vid, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        // Every piece of it, should the framework ever split it into chunks.
        var pieces = response.Headers.GetValues("Set-Cookie")
            .Select(header => header.Split(';')[0])
            .Where(pair => pair.StartsWith("hub.auth", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(pieces);
        return string.Join("; ", pieces);
    }

    /// <summary>A real cookie for an existing VID, through the suite's sign-in (<see cref="TestSignInStartupFilter"/>).</summary>
    private static async Task SignInAsync(HttpClient client, int vid, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>What the end of the IVAO round trip does with this profile, on some other device.</summary>
    private async Task SignInWithIvaoAsync(int vid, string[] positions, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var outcome = await scope.ServiceProvider.GetRequiredService<IvaoSignIn>().CompleteAsync(
            new IvaoUserProfile(
                Vid: vid,
                FirstName: "Test",
                LastName: "Session",
                PublicNickname: null,
                DivisionCode: Code,
                CountryId: null,
                RatingAtc: null,
                RatingPilot: null,
                DiscordId: null,
                Email: null,
                LanguageId: "en",
                IvaoIsStaff: positions.Length > 0,
                IvaoIsSupervisor: false,
                StaffPositions: positions),
            tokens: null,
            cancellationToken);

        Assert.NotNull(outcome.Principal);
    }

    /// <summary>The one grant of the test, written afresh: a reused database may still hold the last run's.</summary>
    private async Task GrantUntilAsync(int vid, DateTime expiresAt, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.UserGrants.RemoveRange(await database.UserGrants.Where(grant => grant.Vid == vid).ToListAsync(cancellationToken));
        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = CorePermissions.LinksEdit,
            Department = Department.FOD,
            Effect = GrantEffect.Grant,
            ExpiresAt = expiresAt,
            Reason = "test",
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task<int?> SignedInVidAsync(HttpClient client, CancellationToken cancellationToken, string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var user = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("user");
        return user.ValueKind == JsonValueKind.Null ? null : user.GetProperty("vid").GetInt32();
    }

    private static async Task<(string? Name, string? Department)[]> PermissionsAsync(HttpClient client, CancellationToken cancellationToken) =>
        [.. (await client.GetFromJsonAsync<JsonElement>("/api/me", cancellationToken)).GetProperty("permissions").EnumerateArray()
            .Select(permission => (
                permission.GetProperty("name").GetString(),
                permission.GetProperty("department").GetString()))];

    /// <summary>The clock of the host, which a test moves forward: the real time, plus how far it was moved.</summary>
    private sealed class MovableClock : IClock
    {
        public TimeSpan Offset { get; set; }

        public DateTime UtcNow => DateTime.UtcNow + Offset;
    }
}
