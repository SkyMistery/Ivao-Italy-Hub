using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The staff directory of G9 (design M1 §6.1). It is not a screen and not a table: it is the
/// <c>staffList</c> provider of G4 reading <c>hub_users</c> and <c>hub_user_staff_positions</c>,
/// which <c>UserSyncService</c> fills at every login — so what is worth testing is the three
/// promises the design makes about it, and none of them is "it draws a list".
/// <para>⚠️ VID range 700001+, which is this class's own. The suite shares one database across the
/// whole collection, so two classes on the same VID are one row and a position given here would
/// follow that VID into another class (handoff §23).</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class StaffDirectoryTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int CoordinatorVid = 700001;
    private const int AssistantVid = 700002;
    private const int AdvisorVid = 700003;
    private const int NeverSignedInVid = 700004;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task StaffDirectoryOrdersByStaffLevel()
    {
        // Seniority first, and the alphabet only to break a tie. A roster ordered by the accident of
        // who signed in first is a roster that reads as a mistake to the people on it — and the
        // order is the one thing about a directory that is not a matter of taste.
        var token = TestContext.Current.CancellationToken;

        // Written in the wrong order on purpose: the advisor first, and the two seniors with
        // surnames that sort against their rank, so neither insertion order nor the alphabet alone
        // could produce the expected answer.
        await SeedStaffAsync(AdvisorVid, "IT-PRA1", "Adams", token);
        await SeedStaffAsync(CoordinatorVid, "IT-PRC", "Young", token);
        await SeedStaffAsync(AssistantVid, "IT-PRAC", "Miller", token);

        var groups = await DirectoryAsync(Department.PRD, token);

        var group = Assert.Single(
            groups,
            entry => entry["department"]?.GetValue<string>() == nameof(Department.PRD));

        var names = (group["members"] as JsonArray)!
            .Select(member => member!["name"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(["Test Young", "Test Miller", "Test Adams"], names);
    }

    [Fact]
    public async Task StaffDirectoryExposesNoContactData()
    {
        // Plan §9.7: there is no public profile. A name, a position and the link to the official
        // IVAO profile — nothing else, and least of all the address the notification queue keeps
        // (`hub_users.email`, G7). The user seeded here **has** an address, so the absence below is
        // a decision of the provider and not an empty column.
        var token = TestContext.Current.CancellationToken;
        await SeedStaffAsync(CoordinatorVid, "IT-PRC", "Young", token, email: "somebody@example.org");

        var groups = await DirectoryAsync(Department.PRD, token);
        var payload = new JsonArray([.. groups.Select(group => group!.DeepClone())]).ToJsonString();

        Assert.DoesNotContain("example.org", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("discord", payload, StringComparison.OrdinalIgnoreCase);

        // What it does carry, so that "no contact data" is not passing by returning nothing at all.
        var member = ((groups.First()["members"] as JsonArray)!.First())!;
        Assert.Equal("Test Young", member["name"]!.GetValue<string>());
        Assert.Equal(CoordinatorVid, member["vid"]!.GetValue<int>());
        Assert.Equal("IT-PRC", member["position"]!.GetValue<string>());
    }

    [Fact]
    public async Task StaffDirectoryHoldsOnlyWhoHasSignedIn()
    {
        // The roster is "whoever has signed in at least once" (plan §16.13), because IVAO has no
        // endpoint for the roster of a division. It is not a limit to work around: it is the data
        // there is, and the page says so in a line of its own rather than pretending to be complete
        // (the client half of this is `StaffDirectorySaysWhoIsMissing`).
        //
        // ⚠️ And the guarantee is **stronger than the provider**, which is what this test found by
        // trying to set up the opposite: `hub_user_staff_positions.vid` is a foreign key to
        // `hub_users`, so a position for somebody with no row at all cannot be written in the first
        // place. The directory cannot show a person who has never signed in because that person
        // cannot have a position — not because a query filters them out afterwards.
        var token = TestContext.Current.CancellationToken;
        await SeedStaffAsync(CoordinatorVid, "IT-PRC", "Young", token);

        var refused = await Assert.ThrowsAsync<DbUpdateException>(
            () => SeedPositionWithoutUserAsync(NeverSignedInVid, "IT-PRAC", token));

        Assert.Contains("hub_users", refused.InnerException?.Message ?? string.Empty, StringComparison.Ordinal);

        // And the person who has signed in is there, so the sentence above is not true by the
        // directory being empty.
        var groups = await DirectoryAsync(Department.PRD, token);

        var vids = (groups.First()["members"] as JsonArray)!
            .Select(member => member!["vid"]!.GetValue<int>())
            .ToArray();

        Assert.Contains(CoordinatorVid, vids);
        Assert.DoesNotContain(NeverSignedInVid, vids);
    }

    // ---- helpers -------------------------------------------------------------------------------

    /// <summary>The directory of one department, asked of the provider the block and the page share.</summary>
    private async Task<IReadOnlyList<JsonNode>> DirectoryAsync(
        Department department,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var provider = scope.ServiceProvider
            .GetServices<IDataBlockProvider>()
            .First(candidate => candidate.Key == CoreBlocks.StaffList);

        var answer = await provider.ResolveAsync(
            new JsonObject { ["department"] = department.ToString() },
            DataBlockContext.Reader,
            cancellationToken);

        return [.. (answer["groups"] as JsonArray)!.Select(group => group!)];
    }

    private async Task SeedStaffAsync(
        int vid,
        string position,
        string lastName,
        CancellationToken cancellationToken,
        string? email = null)
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
        user.LastName = lastName;
        user.Email = email;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await AddPositionAsync(database, vid, position, clock.UtcNow, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A staff position of somebody who has never opened the hub: no row in <c>hub_users</c>.</summary>
    private async Task SeedPositionWithoutUserAsync(int vid, string position, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        await AddPositionAsync(database, vid, position, clock.UtcNow, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task AddPositionAsync(
        HubDbContext database,
        int vid,
        string position,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (await database.UserStaffPositions.AnyAsync(
            row => row.Vid == vid && row.Position == position,
            cancellationToken))
        {
            return;
        }

        var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>());

        database.UserStaffPositions.Add(new UserStaffPosition
        {
            Vid = vid,
            Position = position,
            Department = parsed?.Department,
            Level = parsed?.Level,
            Fir = parsed?.Fir,
            SyncedAt = now,
        });
    }
}
