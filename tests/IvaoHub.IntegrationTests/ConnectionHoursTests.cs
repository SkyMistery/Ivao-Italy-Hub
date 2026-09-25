using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What a sign in writes of the connection hours into <c>hub_users</c> (M3, A1, decision note of 25 September 2026): the
/// last photograph IVAO gave, next to the ratings, and kept when a profile comes without it. The VIDs are the training's
/// (790001–790099), each case its own, and a later run finds its row and signs it in again — the way the other tests of
/// the sign in leave theirs.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ConnectionHoursTests(MariaDbFixture database) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(database.ConnectionString, useIvaoFixtures: true);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task ASignInWritesTheHoursWithTheRatingsAndTheNextOneUpdatesThem()
    {
        const int vid = 790002;
        var token = TestContext.Current.CancellationToken;

        await SignInAsync(Profile(vid, atc: 4, pilot: 4) with { HoursAtc = 120.5m, HoursPilot = 150m }, token);
        var first = await StoredAsync(vid, token);

        Assert.Equal((4, 4, 120.5m, 150m), (first.RatingAtc, first.RatingPilot, first.HoursAtc, first.HoursPilot));

        await SignInAsync(Profile(vid, atc: 5, pilot: 4) with { HoursAtc = 171.25m, HoursPilot = 150.75m }, token);
        var second = await StoredAsync(vid, token);

        Assert.Equal((5, 4, 171.25m, 150.75m), (second.RatingAtc, second.RatingPilot, second.HoursAtc, second.HoursPilot));
    }

    [Fact]
    public async Task ASignInWithoutHoursKeepsTheLastOnes()
    {
        const int vid = 790003;
        var token = TestContext.Current.CancellationToken;

        await SignInAsync(Profile(vid, atc: 4, pilot: 4) with { HoursAtc = 120m, HoursPilot = 150m }, token);

        // IVAO answered without them this time. The ratings are what the profile says; the hours stay, because an old
        // figure can only count fewer of them.
        await SignInAsync(Profile(vid, atc: 5, pilot: null), token);
        var stored = await StoredAsync(vid, token);

        Assert.Equal((5, (int?)null), (stored.RatingAtc, stored.RatingPilot));
        Assert.Equal((120m, 150m), (stored.HoursAtc, stored.HoursPilot));
    }

    [Fact]
    public async Task TheHoursOfTheRecordedProfileReachTheTable()
    {
        // From the answer IVAO gave a real sign in, through the reader and the sign in, to the column.
        const int vid = 790004;
        var token = TestContext.Current.CancellationToken;

        var path = Path.Combine(RepositoryRoot(), FixtureIvaoApiClient.Directory, "users-me-790001.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, token));
        var profile = IvaoUserProfileReader.Read(document.RootElement)! with { Vid = vid };

        await SignInAsync(profile, token);
        var stored = await StoredAsync(vid, token);

        Assert.Equal((6, 5, 120m, 150m), (stored.RatingAtc, stored.RatingPilot, stored.HoursAtc, stored.HoursPilot));
    }

    private async Task SignInAsync(IvaoUserProfile profile, CancellationToken token)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<UserSyncService>().UpsertAsync(profile, token);
    }

    private async Task<HubUser> StoredAsync(int vid, CancellationToken token)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>()
            .Users.AsNoTracking().SingleAsync(user => user.Vid == vid, token);
    }

    private static IvaoUserProfile Profile(int vid, int? atc, int? pilot) => new(
        Vid: vid,
        FirstName: "Test",
        LastName: "Trainee",
        PublicNickname: null,
        DivisionCode: "IT",
        CountryId: "IT",
        RatingAtc: atc,
        RatingPilot: pilot,
        DiscordId: null,
        Email: null,
        LanguageId: "en",
        IvaoIsStaff: false,
        IvaoIsSupervisor: false,
        StaffPositions: []);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
