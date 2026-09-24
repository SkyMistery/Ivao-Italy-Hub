using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Personal tokens (M2, T19a, note 2026-09-15-token-personali-e-agente-del-validatore §6), through the real host and the
/// audience of the test module — the shape the tours' agent takes in T19b:
/// <list type="bullet">
/// <item>a token is created from <c>/me/tokens</c> only for an audience whose permission the member holds, and shown once;</item>
/// <item>it opens the endpoints of its audience and nothing else — not the bootstrap, not the back office;</item>
/// <item>revoked, expired, unknown, or with its member's last login beyond 30 days, it is 401 with a word saying why;</item>
/// <item>it carries no permission: a grant taken away stops counting on the next request;</item>
/// <item>the row is still the single handler's answer, and what the program writes is audited with the token's id.</item>
/// </list>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class PersonalTokenTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13).
    private const int ValidatorVid = 780091;
    private const int PilotVid = 780092;
    private const int MemberVid = 780093;
    private const int SuperadminVid = 780094;

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(ValidatorVid, "IT-WMA1", superadmin: false, token);
        await SeedUserAsync(MemberVid, position: null, superadmin: false, token);
        await SeedUserAsync(SuperadminVid, position: null, superadmin: true, token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ATokenIsShownOnceAndOnlyForAnAudienceTheMemberMayUse()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var item = await CreateItemAsync(superadmin, "fo-test-token-audience", token);
        var grant = await GrantDecideAsync(ValidatorVid, item, token);

        try
        {
            using var validator = await SignedInAsync(ValidatorVid, token);
            var me = await validator.GetFromJsonAsync<JsonElement>("/api/me", token);
            Assert.Contains(SampleModule.AgentAudience, me.GetProperty("user").GetProperty("tokenAudiences").EnumerateArray().Select(value => value.GetString()));

            var (id, text) = await CreateTokenAsync(validator, "home PC", token);
            Assert.StartsWith(PersonalTokens.TokenPrefix, text, StringComparison.Ordinal);

            // The list has the row and never the token, nor its hash.
            using var listed = await validator.GetAsync(new Uri(PersonalTokenEndpoints.Pattern, UriKind.Relative), token);
            var raw = await listed.Content.ReadAsStringAsync(token);
            Assert.DoesNotContain(text, raw, StringComparison.Ordinal);
            Assert.DoesNotContain(PersonalTokens.Hash(text), raw, StringComparison.Ordinal);
            var row = Assert.Single(
                JsonDocument.Parse(raw).RootElement.GetProperty("items").EnumerateArray(),
                entry => entry.GetProperty("id").GetInt64() == id);
            Assert.Equal(text[..11], row.GetProperty("prefix").GetString());
            Assert.Equal(nameof(PersonalTokenStatus.Active), row.GetProperty("status").GetString());

            // A member holding nothing is offered nothing, and asking anyway is refused on the field.
            using var member = await SignedInAsync(MemberVid, token);
            var bare = await member.GetFromJsonAsync<JsonElement>("/api/me", token);
            Assert.Empty(bare.GetProperty("user").GetProperty("tokenAudiences").EnumerateArray());
            using var refused = await member.PostAsJsonAsync(
                PersonalTokenEndpoints.Pattern,
                new { name = "sneaky", audience = SampleModule.AgentAudience, days = 30 },
                token);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.True((await refused.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors").TryGetProperty("audience", out _));

            // Longer than 90 days is refused too.
            using var tooLong = await validator.PostAsJsonAsync(
                PersonalTokenEndpoints.Pattern,
                new { name = "forever", audience = SampleModule.AgentAudience, days = PersonalTokens.MaxDays + 1 },
                token);
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        }
        finally
        {
            await CleanUpAsync(grant, token);
        }
    }

    [Fact]
    public async Task ATokenOpensItsAudienceAndNothingElse()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var item = await CreateItemAsync(superadmin, "fo-test-token-only", token);
        var grant = await GrantDecideAsync(ValidatorVid, item, token);

        try
        {
            using var validator = await SignedInAsync(ValidatorVid, token);
            var (_, text) = await CreateTokenAsync(validator, "only here", token);
            using var program = Program(text);

            var who = await program.GetFromJsonAsync<JsonElement>(SampleModule.AgentWhoAmIPattern, token);
            Assert.Equal(ValidatorVid, who.GetProperty("vid").GetInt32());

            // Not signed in anywhere else: the bootstrap sees a visitor, the back office answers 401.
            var me = await program.GetFromJsonAsync<JsonElement>("/api/me", token);
            Assert.Equal(JsonValueKind.Null, me.GetProperty("user").ValueKind);

            foreach (var elsewhere in new[] { SampleModule.ItemsPattern, "/api/admin/grants", PersonalTokenEndpoints.Pattern })
            {
                using var closed = await program.GetAsync(new Uri(elsewhere, UriKind.Relative), token);
                Assert.Equal(HttpStatusCode.Unauthorized, closed.StatusCode);
            }

            // Nor can a token make a token.
            using var made = await program.PostAsJsonAsync(
                PersonalTokenEndpoints.Pattern,
                new { name = "child", audience = SampleModule.AgentAudience, days = 1 },
                token);
            Assert.Equal(HttpStatusCode.Unauthorized, made.StatusCode);

            // And the cookie does not open the program's endpoints: they are the token's.
            using var withCookie = await validator.GetAsync(new Uri(SampleModule.AgentWhoAmIPattern, UriKind.Relative), token);
            Assert.Equal(HttpStatusCode.Unauthorized, withCookie.StatusCode);
        }
        finally
        {
            await CleanUpAsync(grant, token);
        }
    }

    [Fact]
    public async Task ARefusedTokenSaysWhy()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var item = await CreateItemAsync(superadmin, "fo-test-token-refused", token);
        var grant = await GrantDecideAsync(ValidatorVid, item, token);

        try
        {
            using var validator = await SignedInAsync(ValidatorVid, token);

            Assert.Equal("unknown", await RefusalAsync(PersonalTokens.TokenPrefix + "nothing-like-this", token));
            Assert.Equal("unknown", await RefusalAsync("not even ours", token));

            var (revokedId, revoked) = await CreateTokenAsync(validator, "revoked", token);
            using (var revoke = await validator.PostAsync(new Uri($"{PersonalTokenEndpoints.Pattern}/{revokedId}/revoke", UriKind.Relative), null, token))
            {
                Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
            }

            Assert.Equal("revoked", await RefusalAsync(revoked, token));

            var (expiredId, expired) = await CreateTokenAsync(validator, "expired", token);
            await WithTokenRowAsync(expiredId, row => row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1), token);
            Assert.Equal("expired", await RefusalAsync(expired, token));

            // Positions are refreshed at the login: a member away for longer than 30 days signs in again first.
            var (_, stale) = await CreateTokenAsync(validator, "stale", token);
            await SetLastLoginAsync(ValidatorVid, DateTime.UtcNow.AddDays(-PersonalTokens.SignedInWithinDays - 1), token);
            Assert.Equal("signInAgain", await RefusalAsync(stale, token));

            await SetLastLoginAsync(ValidatorVid, DateTime.UtcNow, token);
            using var again = Program(stale);
            using var back = await again.GetAsync(new Uri(SampleModule.AgentWhoAmIPattern, UriKind.Relative), token);
            Assert.Equal(HttpStatusCode.OK, back.StatusCode);

            // Somebody else's token is not there to revoke.
            using var member = await SignedInAsync(MemberVid, token);
            using var foreign = await member.PostAsync(new Uri($"{PersonalTokenEndpoints.Pattern}/{expiredId}/revoke", UriKind.Relative), null, token);
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        }
        finally
        {
            await SetLastLoginAsync(ValidatorVid, DateTime.UtcNow, token);
            await CleanUpAsync(grant, token);
        }
    }

    [Fact]
    public async Task AGrantTakenAwayStopsCountingOnTheNextRequest()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var item = await CreateItemAsync(superadmin, "fo-test-token-grant", token);
        var grant = await GrantDecideAsync(ValidatorVid, item, token);

        try
        {
            using var validator = await SignedInAsync(ValidatorVid, token);
            var (_, text) = await CreateTokenAsync(validator, "until the grant goes", token);
            using var program = Program(text);

            using (var before = await program.GetAsync(new Uri(SampleModule.AgentWhoAmIPattern, UriKind.Relative), token))
            {
                Assert.Equal(HttpStatusCode.OK, before.StatusCode);
            }

            await CleanUpAsync(grant, token, keepTokens: true);
            grant = 0;

            using var after = await program.GetAsync(new Uri(SampleModule.AgentWhoAmIPattern, UriKind.Relative), token);
            Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        }
        finally
        {
            await CleanUpAsync(grant, token);
        }
    }

    [Fact]
    public async Task TheRowIsTheHandlersAnswerAndTheWriteIsAuditedWithTheToken()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var enabled = await CreateItemAsync(superadmin, "fo-test-token-row", token);
        var other = await CreateItemAsync(superadmin, "fo-test-token-other", token);
        var own = await CreateItemAsync(superadmin, "fo-test-token-own", token, stakeholder: ValidatorVid);
        var grant = await GrantDecideAsync(ValidatorVid, enabled, token);
        var grantOnOwn = await GrantDecideAsync(ValidatorVid, own, token);

        try
        {
            using var validator = await SignedInAsync(ValidatorVid, token);
            var (id, text) = await CreateTokenAsync(validator, "writes", token);

            // No X-Requested-With: a token is not a cookie, and the cross site guard lets it through.
            using var program = Program(text);

            using (var decided = await program.PostAsync(AgentDecideUri(enabled), null, token))
            {
                Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
            }

            using (var notEnabled = await program.PostAsync(AgentDecideUri(other), null, token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, notEnabled.StatusCode);
            }

            using (var aboutThemselves = await program.PostAsync(AgentDecideUri(own), null, token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, aboutThemselves.StatusCode);
            }

            await using var scope = _factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

            // The module's own row, audited in the module's context — which until T19a wrote no audit at all.
            var audit = Assert.Single(await database.AuditLog.AsNoTracking()
                .Where(entry => entry.Entity == "smp_items" && entry.EntityId == enabled.ToString(CultureInfo.InvariantCulture) && entry.Action == "updated")
                .ToListAsync(token));
            Assert.Equal(ValidatorVid, audit.Vid);
            Assert.Equal(id, audit.TokenId);

            // The use is recorded on the token, and is not a change of it.
            var row = await database.PersonalTokens.AsNoTracking().SingleAsync(entry => entry.Id == id, token);
            Assert.NotNull(row.LastUsedAt);
            Assert.False(await database.AuditLog.AnyAsync(
                entry => entry.Entity == "hub_personal_tokens" && entry.EntityId == id.ToString(CultureInfo.InvariantCulture) && entry.Action == "updated",
                token));
        }
        finally
        {
            await CleanUpAsync(grant, token);
            await CleanUpAsync(grantOnOwn, token);
        }
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private HttpClient Program(string text)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", text);
        return client;
    }

    private async Task<string?> RefusalAsync(string text, CancellationToken cancellationToken)
    {
        using var program = Program(text);
        using var response = await program.GetAsync(new Uri(SampleModule.AgentWhoAmIPattern, UriKind.Relative), cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("code").GetString();
    }

    private static Uri AgentDecideUri(long id) =>
        new(SampleModule.AgentDecidePattern.Replace("{id:long}", id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal), UriKind.Relative);

    private static async Task<(long Id, string Token)> CreateTokenAsync(HttpClient client, string name, CancellationToken cancellationToken)
    {
        using var created = await client.PostAsJsonAsync(
            PersonalTokenEndpoints.Pattern,
            new { name, audience = SampleModule.AgentAudience, days = 30 },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return (body.GetProperty("row").GetProperty("id").GetInt64(), body.GetProperty("token").GetString()!);
    }

    private static async Task<long> CreateItemAsync(HttpClient client, string title, CancellationToken cancellationToken, int stakeholder = PilotVid)
    {
        using var created = await client.PostAsJsonAsync(
            SampleModule.ItemsPattern,
            new
            {
                title,
                visibility = nameof(Visibility.Department),
                ownerDepartments = new[] { nameof(Department.ED) },
                stakeholderVid = stakeholder,
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    /// <summary>What "add validator" does: a grant to one member, on one row.</summary>
    private async Task<long> GrantDecideAsync(int vid, long item, CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        var grant = new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = SampleModule.DecidePermission,
            Department = Department.ED,
            ResourceScope = $"{SampleModule.ModuleKey}:item:{item}",
            Effect = GrantEffect.Grant,
            Reason = "test",
        };

        database.UserGrants.Add(grant);
        await database.SaveChangesAsync(cancellationToken);
        return grant.Id;
    }

    /// <summary>Takes a grant back, and the member's tokens with it, so no class after this one finds them.</summary>
    private async Task CleanUpAsync(long grant, CancellationToken cancellationToken, bool keepTokens = false)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        if (grant != 0 && await database.UserGrants.FirstOrDefaultAsync(row => row.Id == grant, cancellationToken) is { } row)
        {
            database.UserGrants.Remove(row);
        }

        // Ten live tokens at most: a class run twice on the same bench must not find the first run's.
        if (!keepTokens)
        {
            database.PersonalTokens.RemoveRange(await database.PersonalTokens.Where(entry => entry.Vid == ValidatorVid).ToListAsync(cancellationToken));
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task WithTokenRowAsync(long id, Action<PersonalToken> change, CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();
        change(await database.PersonalTokens.SingleAsync(row => row.Id == id, cancellationToken));
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task SetLastLoginAsync(int vid, DateTime at, CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();
        var user = await database.Users.SingleAsync(row => row.Vid == vid, cancellationToken);
        user.LastLoginAt = at;
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task SeedUserAsync(int vid, string? position, bool superadmin, CancellationToken cancellationToken)
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
        user.LastName = "Token";
        user.IsStaff = position is not null;
        user.IsSuperadmin = superadmin;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
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
