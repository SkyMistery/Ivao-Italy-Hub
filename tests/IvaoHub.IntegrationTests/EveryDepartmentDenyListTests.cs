using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Whoever reaches every department — the director — skipped every narrowing of the generated lists, even when a deny had
/// taken one department away from them (issue #319, item 6): the list showed rows whose detail the handler refused.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EveryDepartmentDenyListTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int DirectorVid = 931001;

    /// <summary>What every row and grant written here says, so that one left by an interrupted run is found and taken away.</summary>
    private const string Stem = "core-test-319-6";

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        await CleanAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(TestContext.Current.CancellationToken);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ADenyOnOneDepartmentTakesItsRowsOutOfTheDirectorsList()
    {
        var token = TestContext.Current.CancellationToken;
        var needle = $"bellagio{Guid.NewGuid():N}"[..20];

        var atc = await SeedLinkAsync(Department.AOD, $"{needle} atc", token);
        var flight = await SeedLinkAsync(Department.FOD, $"{needle} flight", token);
        await SeedDirectorAsync(token);

        using (var director = await SignedInAsync(token))
        {
            var links = await LinksAsync(director, needle, token);
            Assert.Contains(atc, links);
            Assert.Contains(flight, links);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            database.UserGrants.Add(new UserGrant
            {
                Vid = DirectorVid,
                Kind = GrantKind.Permission,
                Value = CorePermissions.LinksView,
                Department = Department.FOD,
                Effect = GrantEffect.Deny,
                Reason = Stem,
            });
            await database.SaveChangesAsync(token);
        }

        using var denied = await SignedInAsync(token);
        var left = await LinksAsync(denied, needle, token);
        Assert.Contains(atc, left);
        Assert.DoesNotContain(flight, left);

        // The list and the detail say the same.
        using var detail = await denied.GetAsync(new Uri($"{LinksEndpoints.Pattern}/{flight}", UriKind.Relative), token);
        Assert.False(detail.IsSuccessStatusCode);
    }

    private async Task<long> SeedLinkAsync(Department department, string title, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var link = new Link
        {
            OwnerDepartment = department,
            Visibility = Visibility.Department,
            Title = title.L(title),
            Url = $"https://example.org/{Stem}/{Guid.NewGuid():N}",
            IsActive = true,
        };

        database.Links.Add(link);
        await database.SaveChangesAsync(cancellationToken);
        return link.Id;
    }

    private async Task SeedDirectorAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        const string position = "IT-DIR";

        database.Users.Add(new HubUser
        {
            Vid = DirectorVid,
            FirstName = "Test",
            LastName = "Director",
            IsStaff = true,
            SecurityStamp = SuperadminService.NewStamp(),
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        });

        var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>())!;
        database.UserStaffPositions.Add(new UserStaffPosition
        {
            Vid = DirectorVid,
            Position = position,
            Department = parsed.Department,
            Level = parsed.Level,
            Fir = parsed.Fir,
            SyncedAt = clock.UtcNow,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<HttpClient> SignedInAsync(CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, DirectorVid, cancellationToken);
        return client;
    }

    private static async Task<long[]> LinksAsync(HttpClient client, string needle, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"{LinksEndpoints.Pattern}?q={needle}&pageSize=100", cancellationToken);
        return [.. page.GetProperty("items").EnumerateArray().Select(link => link.GetProperty("id").GetInt64())];
    }

    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        await hub.UserGrants.Where(grant => grant.Reason == Stem).ExecuteDeleteAsync(cancellationToken);
        await hub.UserStaffPositions.Where(position => position.Vid == DirectorVid).ExecuteDeleteAsync(cancellationToken);
        await hub.Users.Where(user => user.Vid == DirectorVid).ExecuteDeleteAsync(cancellationToken);

        // Through the context, so that the interceptor takes their rows of the search away with them.
        var links = await hub.Links.IgnoreQueryFilters()
            .Where(link => link.Url.StartsWith($"https://example.org/{Stem}/"))
            .ToListAsync(cancellationToken);
        hub.Links.RemoveRange(links);
        await hub.SaveChangesAsync(cancellationToken);
    }
}
