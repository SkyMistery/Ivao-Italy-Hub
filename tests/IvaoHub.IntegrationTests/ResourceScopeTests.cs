using System.Net;
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
/// The backbone again, with the two relations a row can have with a person (decision note of
/// 15 September 2026, phase T3), proved through the real host, the real cookie and the table of the
/// test module — before the first PIREP exists:
/// <list type="bullet">
/// <item>a grant on one row reaches that row and not its neighbour, and arrives in <c>/api/me</c>
/// with its scope, so that the screen can offer the action there only;</item>
/// <item>whoever a row is about may not decide it, and a super administrator is no exception.</item>
/// </list>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ResourceScopeTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13).
    private const int ValidatorVid = 780021;
    private const int SuperadminVid = 780022;
    private const int PilotVid = 780023;

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        // An advisor of a department that holds nothing of the test module by role: whatever they
        // can decide, they can decide because of the grant this test gives them.
        await SeedUserAsync(ValidatorVid, "IT-WMA1", superadmin: false, token);
        await SeedUserAsync(SuperadminVid, position: null, superadmin: true, token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task AGrantOnOneRowReachesThatRowAndNotItsNeighbour()
    {
        var token = TestContext.Current.CancellationToken;

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var enabled = await CreateAsync(superadmin, "fo-test-scope-enabled", stakeholder: PilotVid, token);
        var other = await CreateAsync(superadmin, "fo-test-scope-other", stakeholder: PilotVid, token);

        await GrantDecideAsync(ValidatorVid, $"{SampleModule.ModuleKey}:item:{enabled}", token);

        // Signed in after the grant: the effective permissions are computed at login.
        using var validator = await SignedInAsync(ValidatorVid, token);

        using (var allowed = await validator.PostAsync(DecideUri(enabled), content: null, token))
        {
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using (var refused = await validator.PostAsync(DecideUri(other), content: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // The bootstrap says where the permission holds, so the screen offers "Take" there only.
        var me = await validator.GetFromJsonAsync<JsonElement>("/api/me", token);
        var decide = Assert.Single(
            me.GetProperty("permissions").EnumerateArray(),
            permission => permission.GetProperty("name").GetString() == SampleModule.DecidePermission);
        Assert.Equal($"{SampleModule.ModuleKey}:item:{enabled}", decide.GetProperty("resourceScope").GetString());
    }

    [Fact]
    public async Task NotEvenASuperAdministratorDecidesARowAboutThemselves()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var aboutThemselves = await CreateAsync(superadmin, "fo-test-scope-own", stakeholder: SuperadminVid, token);
        var aboutSomebodyElse = await CreateAsync(superadmin, "fo-test-scope-else", stakeholder: PilotVid, token);

        using (var refused = await superadmin.PostAsync(DecideUri(aboutThemselves), content: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using (var allowed = await superadmin.PostAsync(DecideUri(aboutSomebodyElse), content: null, token))
        {
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        // Reading their own is not refused: only the permissions the catalogue marks are.
        using var read = await superadmin.GetAsync(new Uri($"{SampleModule.ItemsPattern}/{aboutThemselves}", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static Uri DecideUri(long id) =>
        new(SampleModule.DecidePattern.Replace("{id:long}", id.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), UriKind.Relative);

    private static async Task<long> CreateAsync(HttpClient client, string title, int stakeholder, CancellationToken cancellationToken)
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

    /// <summary>
    /// What "add validator" will do in the tours module: a grant to one member, on one row. Written
    /// straight to the table because the generic permissions screen does not write scopes, on purpose.
    /// </summary>
    private async Task GrantDecideAsync(int vid, string scope, CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = SampleModule.DecidePermission,
            Department = Department.ED,
            ResourceScope = scope,
            Effect = GrantEffect.Grant,
            Reason = "test",
        });

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
        user.LastName = "Scope";
        user.IsStaff = true;
        user.IsSuperadmin = superadmin;
        user.SecurityStamp = SuperadminService.NewStamp();
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
