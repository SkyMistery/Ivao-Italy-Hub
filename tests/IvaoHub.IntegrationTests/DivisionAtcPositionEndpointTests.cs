using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// `GET /api/atc/positions` (design of the editor's phase 3, §5.8): the ATC positions of the division with their frequencies,
/// for whoever writes content — what the frequency table of a page offers and compares its rows with. The snapshot is the one
/// the fixtures of the world give (<c>tests/fixtures/ivao/atc-positions-world.json</c>, <c>subcenters-world.json</c>), read
/// by the night's job as <see cref="AtcPositionTests"/> reads it. VIDs 786101–786103; nothing else is written.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class DivisionAtcPositionEndpointTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 786101;
    private const int EventsCoordinatorVid = 786102;
    private const int MemberVid = 786103;

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RefDataSyncJob>().RunAsync(token);
        }

        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(MemberVid, cancellationToken: token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task WhoeverWritesContentReadsThePositionsOfTheDivisionWithTheirFrequencies()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(EventsCoordinatorVid, token);

        var positions = await coordinator.GetFromJsonAsync<JsonElement>(DivisionAtcPositionEndpoints.Pattern, token);
        var byCallsign = positions.EnumerateArray().ToDictionary(position => position.GetProperty("callsign").GetString()!);

        // The division's, as the directory answers them: a tower and a sector, each with its name, kind and frequency
        // written out as a controller reads it. Paris is another division's.
        var tower = byCallsign["LIRF_TWR"];
        Assert.Equal("TWR", tower.GetProperty("type").GetString());
        Assert.Equal("Fiume Tower", tower.GetProperty("name").GetString());
        Assert.Equal("118.700", tower.GetProperty("frequency").GetString());
        Assert.Equal("124.200", byCallsign["LIRR_NE_CTR"].GetProperty("frequency").GetString());
        Assert.DoesNotContain("LFPG_TWR", byCallsign.Keys);

        // The directory's answer, row for row and in its order — the callsigns' order of the database's collation, where
        // `LIRR_NE_CTR` comes before `LIRR_NE1_CTR`.
        await using var scope = _factory.Services.CreateAsyncScope();
        var division = await scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>().OfDivisionAsync(token);
        Assert.Equal(
            division.Select(position => position.Callsign),
            positions.EnumerateArray().Select(position => position.GetProperty("callsign").GetString()));
    }

    [Fact]
    public async Task TheListIsNarrowedByCallsignOrNameInAnyCase()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var byCallsign = await CallsignsAsync(superadmin, "lirf_t", token);
        Assert.Equal(["LIRF_TW1_APP", "LIRF_TWR"], byCallsign);

        var byName = await CallsignsAsync(superadmin, "  malpensa ground ", token);
        Assert.Equal(["LIMC_N_GND", "LIMC_W_GND"], byName);

        Assert.Empty(await CallsignsAsync(superadmin, "LFPG", token));
    }

    [Fact]
    public async Task AMemberWithoutContentEditIsNotAnswered()
    {
        var token = TestContext.Current.CancellationToken;
        using var member = await SignedInAsync(MemberVid, token);

        using var refused = await member.GetAsync(new Uri(DivisionAtcPositionEndpoints.Pattern, UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var anonymous = _factory.CreateApiClient();
        using var unknown = await anonymous.GetAsync(new Uri(DivisionAtcPositionEndpoints.Pattern, UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    private static async Task<string[]> CallsignsAsync(HttpClient client, string q, CancellationToken cancellationToken)
    {
        var positions = await client.GetFromJsonAsync<JsonElement>(
            $"{DivisionAtcPositionEndpoints.Pattern}?q={Uri.EscapeDataString(q)}",
            cancellationToken);
        return [.. positions.EnumerateArray().Select(position => position.GetProperty("callsign").GetString()!)];
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

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
