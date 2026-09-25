using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The ATC positions of IVAO in the snapshot of the reference data, and the question a module asks of them (M3, A2, note
/// <c>decisions/2026-09-25-le-postazioni-atc-e-il-tipo-exam.md</c>): the world is copied, each of IVAO's two lists is
/// refreshed and pruned on its own and never on an empty answer, and the directory answers the positions of the division
/// a rating is trained on. The client reads the answers of the world recorded on 25 September 2026
/// (<c>tests/fixtures/ivao/atc-positions-world.json</c>, <c>subcenters-world.json</c>).
/// <para>No VID and no slug: the only rows written here are positions whose callsigns carry a <c>Q</c> no station of the
/// fixtures has, and every test takes its own back.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AtcPositionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    /// <summary>A tower and a sector IVAO does not list: what a decommissioned station looks like to the snapshot.</summary>
    private static readonly string[] Retired = ["LIRF_Q_TWR", "LIRR_Q_CTR"];

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        // The OAuth client of a division is not necessarily allowed the reference endpoints, so the tests read the same
        // files a developer without credentials does.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheSnapshotHoldsThePositionsOfTheWorld()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var positions = await database.IvaoAtcPositions.AsNoTracking().ToListAsync(token);

        var tower = Assert.Single(positions, position => position.Callsign == "LIRF_TWR");
        Assert.Equal("TWR", tower.PositionType);
        Assert.Equal("LIRF", tower.AirportIcao);
        Assert.Null(tower.CenterId);
        Assert.Equal("Fiume Tower", tower.Name);

        var sector = Assert.Single(positions, position => position.Callsign == "LIRR_NE_CTR");
        Assert.Equal("CTR", sector.PositionType);
        Assert.Null(sector.AirportIcao);
        Assert.Equal("LIRR", sector.CenterId);

        // The world, as the airports are: a French tower and a French sector are copied too, and what is the division's is
        // the directory's to say. A station IVAO lists twice is one row.
        Assert.Contains(positions, position => position.Callsign == "LFPG_TWR");
        Assert.Contains(positions, position => position.Callsign == "LFFF_E_CTR");
        Assert.Single(positions, position => position.Callsign == "LIBG_APP");

        // The whole row is kept, without its outline: a field nobody reads today does not have to be guessed at.
        using var raw = JsonDocument.Parse(tower.RawJson);
        Assert.Equal(118.7, raw.RootElement.GetProperty("frequency").GetDouble());

        var run = await LastRunAsync(database, token);
        Assert.Equal("succeeded", run.Status);
        Assert.Contains("ATC position", run.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARunRefreshesTheRowsAndDropsWhatIvaoNoLongerLists()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        try
        {
            await AddRetiredAsync(token);
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                var tower = await database.IvaoAtcPositions.SingleAsync(position => position.Callsign == "LIRF_TWR", token);
                tower.Name = "A name IVAO has since changed";
                await database.SaveChangesAsync(token);
            }

            await SyncAsync(token);

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

                // Both lists came back, so each dropped its own station IVAO no longer lists.
                Assert.False(await database.IvaoAtcPositions.AnyAsync(position => Retired.Contains(position.Callsign), token));

                var tower = Assert.Single(await database.IvaoAtcPositions.AsNoTracking()
                    .Where(position => position.Callsign == "LIRF_TWR")
                    .ToListAsync(token));
                Assert.Equal("Fiume Tower", tower.Name);
            }
        }
        finally
        {
            await RemoveRetiredAsync(token);
        }
    }

    [Fact]
    public async Task AListThatDoesNotComeBackLeavesItsRowsAsTheyWere()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        try
        {
            // The sectors do not come back, the airports' positions do: only the airports' list prunes.
            await AddRetiredAsync(token);
            Assert.Equal("partial", await RunWithAsync(fixtures => new HalfAnswer(fixtures, airports: true, sectors: false), token));

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                Assert.False(await database.IvaoAtcPositions.AnyAsync(position => position.Callsign == "LIRF_Q_TWR", token));
                Assert.True(await database.IvaoAtcPositions.AnyAsync(position => position.Callsign == "LIRR_Q_CTR", token));
                Assert.True(await database.IvaoAtcPositions.AnyAsync(position => position.Callsign == "LIRR_NE_CTR", token));
            }

            // A client written before A2 answers no position at all, through the interface: nothing moves, and the run
            // says it is partial rather than calling a stale snapshot a success.
            await AddRetiredAsync(token);
            Assert.Equal("partial", await RunWithAsync(fixtures => new FixturesBeforeA2(fixtures), token));

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                Assert.Equal(
                    Retired.Length,
                    await database.IvaoAtcPositions.CountAsync(position => Retired.Contains(position.Callsign), token));
                Assert.True(await database.IvaoAtcPositions.AnyAsync(position => position.Callsign == "LIRF_TWR", token));
            }
        }
        finally
        {
            await RemoveRetiredAsync(token);
        }
    }

    [Fact]
    public async Task TheDirectoryAnswersThePositionsOfTheDivisionARatingIsTrainedOn()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>();
        var ratings = scope.ServiceProvider.GetRequiredService<RatingVocabulary>();

        // An ADC is trained on the towers of the division, each with its airport and that airport's FIR. Paris is another
        // division's; ground and delivery are not what the vocabulary trains an ADC on.
        var towers = await directory.ForRatingAsync(Atc(ratings, "ADC"), token);
        Assert.Equal(["LIBD_TWR", "LIMC_E_TWR", "LIMC_TWR", "LIRF_E_TWR", "LIRF_TWR"], towers.Select(tower => tower.Callsign));
        Assert.Equal(new AtcPositionDto("LIRF_TWR", "Fiume Tower", "LIRF", "LIRR"), towers[^1]);
        Assert.Equal("LIMM", Assert.Single(towers, tower => tower.Callsign == "LIMC_TWR").Fir);

        // An APC on the approaches. Grottaglie is Italian, but the bench does not know its airport, and a position whose
        // airport the hub does not know belongs to nobody.
        var approaches = await directory.ForRatingAsync(Atc(ratings, "APC"), token);
        Assert.Contains(new AtcPositionDto("LIRF_AWL_APP", "Roma Radar", "LIRF", "LIRR"), approaches);
        Assert.All(approaches, approach => Assert.EndsWith("_APP", approach.Callsign, StringComparison.Ordinal));
        Assert.DoesNotContain(approaches, approach => approach.AirportIcao == "LFPG");
        Assert.DoesNotContain(approaches, approach => approach.Callsign == "LIBG_APP");

        // An ACC on the sectors of the division's FIRs, with no airport, and the military ones among them: the training
        // department uses some, and leaves out the rest with a setting of its module.
        var sectors = await directory.ForRatingAsync(Atc(ratings, "ACC"), token);
        Assert.Contains(new AtcPositionDto("LIRR_NE_CTR", "Roma Radar", null, "LIRR"), sectors);
        Assert.Contains(sectors, sector => sector.Callsign == "LIRR_MIL_CTR");
        Assert.All(sectors, sector => Assert.Null(sector.AirportIcao));
        Assert.DoesNotContain(sectors, sector => sector.Fir == "LFFF");
        Assert.DoesNotContain(sectors, sector => sector.Callsign.EndsWith("_FSS", StringComparison.Ordinal));

        // A rating trained on no position has none: a pilot's, and one above every rating trained.
        Assert.Empty(await directory.ForRatingAsync(ratings.Ladder(RatingKind.Pilot).Single(rating => rating.ShortName == "PP"), token));
        Assert.Empty(await directory.ForRatingAsync(Atc(ratings, "SEC"), token));
    }

    [Fact]
    public async Task TheDirectoryFollowsTheKindOfPositionAndNotTheNumbers()
    {
        // A rating of another network, trained on the approaches under a number IVAO does not have: the answer is the
        // approaches, because the directory asks the rating for its kind of position and knows nothing else about it.
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>();

        var elsewhere = await directory.ForRatingAsync(new Rating(RatingKind.Atc, 42, "XAP", true, "APP"), token);
        var approaches = await directory.ForRatingAsync(
            Atc(scope.ServiceProvider.GetRequiredService<RatingVocabulary>(), "APC"),
            token);

        Assert.NotEmpty(elsewhere);
        Assert.Equal(approaches, elsewhere);
    }

    [Fact]
    public async Task AnotherDivisionIsAnsweredItsOwnPositions()
    {
        // The division is configuration (plan §4): the same snapshot, read by a hub whose countryId is FR, answers the
        // towers of Paris and none of Italy's. Its sectors would be those of its own FIRs, which a snapshot taken for
        // Italy does not have: none, and none of Italy's either.
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        var division = _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value with { CountryId = "FR" };
        await using var french = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(Options.Create(division))));

        await using var scope = french.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>();
        var ratings = scope.ServiceProvider.GetRequiredService<RatingVocabulary>();

        var towers = await directory.ForRatingAsync(Atc(ratings, "ADC"), token);
        Assert.Contains(towers, tower => tower.Callsign == "LFPG_TWR" && tower.Fir == "LFFF");
        Assert.DoesNotContain(towers, tower => tower.AirportIcao != "LFPG");

        Assert.Empty(await directory.ForRatingAsync(Atc(ratings, "ACC"), token));
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static Rating Atc(RatingVocabulary ratings, string shortName) =>
        ratings.Ladder(RatingKind.Atc).Single(rating => rating.ShortName == shortName);

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RefDataSyncJob>().RunAsync(cancellationToken);
    }

    /// <summary>A run of the job with a client of the test's making, and the status it wrote.</summary>
    private async Task<string> RunWithAsync(
        Func<FixtureIvaoApiClient, IIvaoApiClient> client,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<HubDbContext>();

        var job = new RefDataSyncJob(
            client(services.GetRequiredService<FixtureIvaoApiClient>()),
            database,
            services.GetRequiredService<IFirDirectory>(),
            services.GetRequiredService<IOptions<DivisionOptions>>(),
            services.GetRequiredService<IClock>(),
            NullLogger<RefDataSyncJob>.Instance);

        await job.RunAsync(cancellationToken);
        return (await LastRunAsync(database, cancellationToken)).Status;
    }

    private static Task<JobLogEntry> LastRunAsync(HubDbContext database, CancellationToken cancellationToken) =>
        database.JobsLog.AsNoTracking()
            .Where(entry => entry.Job == RefDataSyncJob.JobName)
            .OrderByDescending(entry => entry.Id)
            .FirstAsync(cancellationToken);

    private async Task AddRetiredAsync(CancellationToken cancellationToken)
    {
        await RemoveRetiredAsync(cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        database.IvaoAtcPositions.AddRange(
            new IvaoAtcPosition
            {
                Callsign = "LIRF_Q_TWR",
                PositionType = "TWR",
                AirportIcao = "LIRF",
                Name = "A tower IVAO has since retired",
                SyncedAt = clock.UtcNow,
            },
            new IvaoAtcPosition
            {
                Callsign = "LIRR_Q_CTR",
                PositionType = "CTR",
                CenterId = "LIRR",
                Name = "A sector IVAO has since retired",
                SyncedAt = clock.UtcNow,
            });

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveRetiredAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.IvaoAtcPositions.RemoveRange(
            await database.IvaoAtcPositions.Where(position => Retired.Contains(position.Callsign)).ToListAsync(cancellationToken));
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Every call answered from the fixtures except the positions, which this client does not know — a client written
    /// before A2 — so the interface answers for it.
    /// </summary>
    private class FixturesBeforeA2(FixtureIvaoApiClient fixtures) : IIvaoApiClient
    {
        protected FixtureIvaoApiClient Fixtures => fixtures;

        public Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(string countryId, CancellationToken cancellationToken = default) =>
            fixtures.GetCentersAsync(countryId, cancellationToken);

        public Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(
            string? countryId,
            bool includeRunways = true,
            CancellationToken cancellationToken = default) =>
            fixtures.GetAirportsAsync(countryId, includeRunways, cancellationToken);

        public Task<IReadOnlyList<IvaoRunway>?> GetRunwaysAsync(string icao, CancellationToken cancellationToken = default) =>
            fixtures.GetRunwaysAsync(icao, cancellationToken);

        public Task<IReadOnlyList<IvaoAircraftType>> GetAircraftTypesAsync(CancellationToken cancellationToken = default) =>
            fixtures.GetAircraftTypesAsync(cancellationToken);

        public Task<(IReadOnlyList<IvaoAircraftEquipment> Equipments, IReadOnlyList<IvaoTransponderType> Transponders)>
            GetFlightPlanVocabulariesAsync(CancellationToken cancellationToken = default) =>
            fixtures.GetFlightPlanVocabulariesAsync(cancellationToken);

        public Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default) =>
            fixtures.GetMeAsync(accessToken, cancellationToken);

        public Task<IReadOnlyList<IvaoTrackerSessionDto>?> SearchSessionsAsync(
            IvaoSessionQuery query,
            CancellationToken cancellationToken = default) =>
            fixtures.SearchSessionsAsync(query, cancellationToken);

        public Task<IReadOnlyList<IvaoFlightPlanDto>?> GetFlightPlansAsync(
            long sessionId,
            CancellationToken cancellationToken = default) =>
            fixtures.GetFlightPlansAsync(sessionId, cancellationToken);

        public Task<IReadOnlyList<IvaoTrackPointDto>?> GetTracksAsync(long sessionId, CancellationToken cancellationToken = default) =>
            fixtures.GetTracksAsync(sessionId, cancellationToken);

        public Task<IvaoMetarDto?> GetMetarAsync(string icao, CancellationToken cancellationToken = default) =>
            fixtures.GetMetarAsync(icao, cancellationToken);

        public Task<IvaoNetworkStatus> GetNetworkStatusAsync(
            IvaoAirspace airspace,
            CancellationToken cancellationToken = default) =>
            fixtures.GetNetworkStatusAsync(airspace, cancellationToken);
    }

    /// <summary>The fixtures, with one of the two lists of positions not coming back, as when IVAO's gateway gives up.</summary>
    private sealed class HalfAnswer(FixtureIvaoApiClient fixtures, bool airports, bool sectors)
        : FixturesBeforeA2(fixtures), IIvaoApiClient
    {
        public async Task<(IReadOnlyList<IvaoAtcPositionDto> Airports, IReadOnlyList<IvaoAtcPositionDto> Sectors)>
            GetAtcPositionsAsync(CancellationToken cancellationToken = default)
        {
            var (positions, subcenters) = await Fixtures.GetAtcPositionsAsync(cancellationToken);
            return (airports ? positions : [], sectors ? subcenters : []);
        }
    }
}
