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
/// The ATC positions of IVAO in the snapshot of the reference data, and the questions a module asks of them (M3, A2, note
/// <c>decisions/2026-09-25-le-postazioni-atc-e-il-tipo-exam.md</c>): the world is copied, each of IVAO's two lists is
/// refreshed and pruned on its own and never on an empty answer, and the directory answers the positions of the division
/// a rating is trained on — and, since M4's E10c (note <c>decisions/2026-09-30-il-rating-preferito-e-il-minimo-di-una-postazione.md</c>),
/// every position of the division and the ones among some callsigns, each with its kind and FIR, and the minimum of a
/// position for a shift, from IVAO's FRAs, which the night copies too. The client reads the answers of the world recorded on
/// 25 September 2026 (<c>tests/fixtures/ivao/atc-positions-world.json</c>, <c>subcenters-world.json</c>) and the FRAs of the
/// bench's positions recorded on 30 September 2026 (<c>fras-IT.json</c>).
/// <para>No VID and no slug: the only rows written here are positions whose callsigns carry a <c>Q</c> no station of the
/// fixtures has, and FRAs with identifiers far above IVAO's, and every test takes its own back.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AtcPositionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    /// <summary>A tower and a sector IVAO does not list: what a decommissioned station looks like to the snapshot.</summary>
    private static readonly string[] Retired = ["LIRF_Q_TWR", "LIRR_Q_CTR"];

    /// <summary>FRAs of the tests' own: one IVAO no longer lists, and one switched off. IVAO's identifiers were five digits.</summary>
    private static readonly long[] OwnFras = [900_000_001, 900_000_002];

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
        Assert.Equal(new AtcPositionDto("LIRF_TWR", "TWR", "Fiume Tower", "LIRF", "LIRR"), towers[^1]);
        Assert.Equal("LIMM", Assert.Single(towers, tower => tower.Callsign == "LIMC_TWR").Fir);

        // An APC on the approaches. Grottaglie is Italian, but the bench does not know its airport, and a position whose
        // airport the hub does not know belongs to nobody.
        var approaches = await directory.ForRatingAsync(Atc(ratings, "APC"), token);
        Assert.Contains(new AtcPositionDto("LIRF_AWL_APP", "APP", "Roma Radar", "LIRF", "LIRR"), approaches);
        Assert.All(approaches, approach => Assert.EndsWith("_APP", approach.Callsign, StringComparison.Ordinal));
        Assert.DoesNotContain(approaches, approach => approach.AirportIcao == "LFPG");
        Assert.DoesNotContain(approaches, approach => approach.Callsign == "LIBG_APP");

        // An ACC on the sectors of the division's FIRs, with no airport, and the military ones among them: the training
        // department uses some, and leaves out the rest with a setting of its module.
        var sectors = await directory.ForRatingAsync(Atc(ratings, "ACC"), token);
        Assert.Contains(new AtcPositionDto("LIRR_NE_CTR", "CTR", "Roma Radar", null, "LIRR"), sectors);
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

    [Fact]
    public async Task TheDirectoryAnswersEveryPositionOfTheDivisionWithItsKindAndFir()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>();
        var positions = await directory.OfDivisionAsync(token);

        // The positions of the bench's three Italian airports and the sectors of their three FIRs, of every kind IVAO lists
        // — delivery, ground, ATIS, FSS and the military ones too —: what an event's positions are chosen from (design M4
        // §4.1). Paris is another division's; Grottaglie is Italian, but the bench does not know its airport.
        Assert.Equal(
            [
                "LIBB_ES_CTR", "LIBB_EU_CTR", "LIBB_FSS", "LIBB_MIL_CTR", "LIBD_ATIS", "LIBD_CS0_APP", "LIBD_TWR",
                "LIMC_ANE_APP", "LIMC_ANW_APP", "LIMC_ASW_APP", "LIMC_ATIS", "LIMC_DEL", "LIMC_E_TWR", "LIMC_MAR_APP",
                "LIMC_N_GND", "LIMC_TWR", "LIMC_W_GND", "LIMM_ES2_CTR", "LIMM_ES5_CTR", "LIMM_FSS", "LIMM_MIL_CTR",
                "LIMM_WS2_CTR", "LIMM_WS5_CTR", "LIRF_AEM_APP", "LIRF_AET_APP", "LIRF_ATIS", "LIRF_AWL_APP", "LIRF_DEL",
                "LIRF_E_TWR", "LIRF_GND", "LIRF_PN1_APP", "LIRF_PS1_APP", "LIRF_TW1_APP", "LIRF_TWR", "LIRF_W_GND",
                "LIRR_ES_CTR", "LIRR_EW_CTR", "LIRR_FSS", "LIRR_MIL_CTR", "LIRR_NC_CTR", "LIRR_NE1_CTR", "LIRR_NE_CTR",
                "LIRR_NW_CTR", "LIRR_OV_CTR", "LIRR_PLN_FSS", "LIRR_SU_CTR", "LIRR_TS_CTR", "LIRR_US_CTR",
            ],
            positions.Select(position => position.Callsign).Order(StringComparer.Ordinal));

        // Each with its kind, which a module hands to the vocabulary, and its FIR, which an event's row copies (IHasFir).
        Assert.Contains(new AtcPositionDto("LIRF_DEL", "DEL", "Fiume Delivery", "LIRF", "LIRR"), positions);
        Assert.Contains(new AtcPositionDto("LIMC_N_GND", "GND", "Malpensa Ground North", "LIMC", "LIMM"), positions);
        Assert.Contains(new AtcPositionDto("LIBD_ATIS", "ATIS", "Bari ATIS", "LIBD", "LIBB"), positions);
        Assert.Contains(new AtcPositionDto("LIRR_FSS", "FSS", "Roma Information", null, "LIRR"), positions);
        Assert.Contains(new AtcPositionDto("LIMM_MIL_CTR", "CTR", "Milano Military", null, "LIMM"), positions);

        // What the directory answered for a rating before is a part of this answer, row for row and in the same order.
        foreach (var rating in scope.ServiceProvider.GetRequiredService<RatingVocabulary>().Ladder(RatingKind.Atc))
        {
            Assert.Equal(
                positions.Where(position => position.Type == rating.PositionType),
                await directory.ForRatingAsync(rating, token));
        }
    }

    [Fact]
    public async Task TheDirectoryFindsThePositionsOfTheDivisionByTheirCallsigns()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>();

        // A tower written the way a form may send it, and a sector: each comes with its kind and its FIR, and is found by
        // its callsign in any case.
        var found = await directory.FindAsync([" lirf_twr ", "LIRR_NE_CTR", "LIRR_NE_CTR"], token);
        Assert.Equal(["LIRF_TWR", "LIRR_NE_CTR"], found.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new AtcPositionDto("LIRF_TWR", "TWR", "Fiume Tower", "LIRF", "LIRR"), found["LIRF_TWR"]);
        Assert.Equal(new AtcPositionDto("LIRR_NE_CTR", "CTR", "Roma Radar", null, "LIRR"), found["lirr_ne_ctr"]);

        // Another division's tower and sector, a position whose airport the hub does not know, a callsign IVAO does not
        // list, and nothing at all: absent, never an error.
        Assert.Empty(await directory.FindAsync(["LFPG_TWR", "LFFF_E_CTR", "LIBG_APP", "LIRF_ZZ_TWR", "", "  "], token));
        Assert.Empty(await directory.FindAsync([], token));
    }

    [Fact]
    public async Task AnotherDivisionFindsItsOwnPositionsOfEveryKind()
    {
        // The same snapshot, read by a hub whose countryId is FR: every position of Paris, its departure among them — a
        // kind no Italian airport of the bench has —, and none of Italy's, by the list or by the callsign.
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        var division = _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value with { CountryId = "FR" };
        await using var french = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(Options.Create(division))));

        await using var scope = french.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>();

        var positions = await directory.OfDivisionAsync(token);
        Assert.Equal(17, positions.Count);
        Assert.All(positions, position =>
        {
            Assert.Equal("LFPG", position.AirportIcao);
            Assert.Equal("LFFF", position.Fir);
        });
        Assert.Contains(new AtcPositionDto("LFPG_DEP", "DEP", "De Gaulle Departure", "LFPG", "LFFF"), positions);

        var found = await directory.FindAsync(["lfpg_dep", "LIRF_TWR"], token);
        Assert.Equal("LFPG_DEP", Assert.Single(found).Key);
    }

    [Fact]
    public async Task TheSnapshotHoldsTheFrasOfTheDivision()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        // The FRAs of the bench's positions IVAO had for Italy, each with the callsign of the position it names.
        Assert.Equal(94, await database.IvaoFras.CountAsync(fra => fra.Id < OwnFras[0], token));

        var tower = await database.IvaoFras.AsNoTracking()
            .Where(fra => fra.Callsign == "LIBD_TWR")
            .OrderBy(fra => fra.Id)
            .ToListAsync(token);
        Assert.Equal(
            [(785L, 3, new TimeOnly(8, 0), new TimeOnly(23, 0)), (39880L, 5, new TimeOnly(23, 0), new TimeOnly(8, 0))],
            tower.Select(fra => (fra.Id, fra.MinimumRating, fra.StartsAt, fra.EndsAt)));
        Assert.All(tower, fra => Assert.True(fra.IsActive && fra.OnDate is null));

        var run = await LastRunAsync(database, token);
        Assert.Equal("succeeded", run.Status);
        Assert.Contains("94 FRA(s) for IT", run.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARunRefreshesTheFrasAndDropsWhatIvaoNoLongerLists()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        try
        {
            await AddOwnFrasAsync(token);
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                var day = await database.IvaoFras.SingleAsync(fra => fra.Id == 785, token);
                day.MinimumRating = 9;
                await database.SaveChangesAsync(token);
            }

            await SyncAsync(token);

            // A full answer: the FRAs IVAO no longer lists go, and the ones it lists are as it says.
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                Assert.False(await database.IvaoFras.AnyAsync(fra => OwnFras.Contains(fra.Id), token));
                Assert.Equal(3, (await database.IvaoFras.AsNoTracking().SingleAsync(fra => fra.Id == 785, token)).MinimumRating);
            }
        }
        finally
        {
            await RemoveOwnFrasAsync(token);
        }
    }

    [Fact]
    public async Task ANightIvaoDoesNotAnswerLeavesThemAsTheyWere()
    {
        // A client written before E10c cannot be asked through the interface, as when IVAO does not answer: a bad night must
        // never read as "the division has lifted every minimum".
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        try
        {
            await AddOwnFrasAsync(token);
            await RunWithAsync(fixtures => new FixturesBeforeA2(fixtures), token);

            await using var scope = _factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            Assert.Equal(OwnFras.Length, await database.IvaoFras.CountAsync(fra => OwnFras.Contains(fra.Id), token));
            Assert.Equal(94, await database.IvaoFras.CountAsync(fra => fra.Id < OwnFras[0], token));
            Assert.Contains("no answer on the FRAs of IT", (await LastRunAsync(database, token)).Message, StringComparison.Ordinal);
        }
        finally
        {
            await RemoveOwnFrasAsync(token);
        }
    }

    [Fact]
    public async Task ADivisionThatLiftsEveryFraHasNone()
    {
        // The reviewer's point 2 on #204: IVAO answers, and with none — the division lifted all of its FRAs. The snapshot keeps
        // none either, or the old ones would go on keeping controllers off positions that are open to them.
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        try
        {
            await AddOwnFrasAsync(token);
            Assert.Equal("succeeded", await RunWithAsync(fixtures => new NoFra(fixtures), token));

            await using var scope = _factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            Assert.False(await database.IvaoFras.AnyAsync(token));
            Assert.Contains("0 FRA(s) for IT", (await LastRunAsync(database, token)).Message, StringComparison.Ordinal);
        }
        finally
        {
            // The FRAs of the fixtures come back for the classes and the tests after this one.
            await RemoveOwnFrasAsync(token);
            await SyncAsync(token);
        }
    }

    [Fact]
    public async Task TheHubsVocabularySaysTheDivisionsPreferredRatings()
    {
        // Moved into division.json on the maintainer's answer on #204: the vocabulary the core registers carries Italy's rule,
        // and a module asks it without naming a rating.
        await using var scope = _factory.Services.CreateAsyncScope();
        var ratings = scope.ServiceProvider.GetRequiredService<RatingVocabulary>();

        Assert.Equal(
            ["APP APC", "ATIS -", "CTR ACC", "DEL AS3", "DEP APC", "FSS ADC", "GND ADC", "TWR ADC"],
            IvaoAtcPosition.Kinds.Select(kind => $"{kind} {ratings.PreferredFor(kind)?.ShortName ?? "-"}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AKindOrARatingTheCoreDoesNotKnowStopsTheStart()
    {
        // Italy's file with a kind IVAO does not list and a rating it does not have, written into the options the way the
        // file is read: the host does not come up, and says which key and why.
        await using var broken = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<DivisionOptions>(division =>
            {
                division.PreferredAtcRatings["TOWER"] = "ADC";
                division.PreferredAtcRatings["GND"] = "XYZ";
            })));

        var refused = Assert.ThrowsAny<Exception>(() => broken.Services.GetRequiredService<IOptions<DivisionOptions>>().Value);
        var failures = ValidationFailures(refused);

        Assert.Contains(failures, failure => failure.Contains("'preferredAtcRatings' has an entry for 'TOWER'", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("'preferredAtcRatings.GND' (XYZ) is not an ATC rating", StringComparison.Ordinal));
    }

    /// <summary>The failures of the validation the start ran into, however deep the host wrapped them.</summary>
    private static List<string> ValidationFailures(Exception exception) =>
        exception switch
        {
            OptionsValidationException validation => [.. validation.Failures],
            AggregateException aggregate => [.. aggregate.InnerExceptions.SelectMany(ValidationFailures)],
            { InnerException: { } inner } => ValidationFailures(inner),
            _ => [],
        };

    [Fact]
    public async Task TheDirectoryAnswersTheMinimumOfAPositionForAShift()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        try
        {
            // One switched off, on a position with no FRA of its own: it holds nowhere.
            await AddOwnFrasAsync(token);

            await using var scope = _factory.Services.CreateAsyncScope();
            var directory = scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>();
            var minima = await directory.MinimaAsync(["libd_twr", "LIMC_ANE_APP", "LIRF_AWL_APP", "LIMC_MAR_APP", "LFPG_TWR"], token);

            // Every callsign asked has one, found in any case.
            Assert.Equal(5, minima.Count);

            // Bari's tower: AS2 by day, ADC by night, and ADC for a shift across the change (7 October 2026 is a Wednesday).
            Assert.Equal(3, minima["LIBD_TWR"].Over(At(2026, 10, 7, 18), At(2026, 10, 7, 19)));
            Assert.Equal(5, minima["LIBD_TWR"].Over(At(2026, 10, 7, 23), At(2026, 10, 8, 0)));
            Assert.Equal(5, minima["libd_twr"].Over(At(2026, 10, 7, 22), At(2026, 10, 8, 0)));

            // Malpensa's approach asks APC from noon on a Saturday; one of Rome's is closed to anyone but a CAI.
            Assert.Equal(6, minima["LIMC_ANE_APP"].Over(At(2026, 10, 10, 12), At(2026, 10, 10, 13)));
            Assert.Equal(10, minima["LIRF_AWL_APP"].Over(At(2026, 10, 7, 18), At(2026, 10, 7, 19)));

            // No FRA that holds: a position without one, and another division's.
            Assert.Null(minima["LIMC_MAR_APP"].Over(At(2026, 10, 7, 18), At(2026, 10, 7, 19)));
            Assert.Null(minima["LFPG_TWR"].Over(At(2026, 10, 7, 18), At(2026, 10, 7, 19)));

            Assert.Empty(await directory.MinimaAsync([" ", ""], token));
        }
        finally
        {
            await RemoveOwnFrasAsync(token);
        }
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static DateTime At(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// An FRA of a tower IVAO no longer lists, and one switched off on an approach with none of its own — both the whole
    /// day, every day, closed to anyone but a CAI.
    /// </summary>
    private async Task AddOwnFrasAsync(CancellationToken cancellationToken)
    {
        await RemoveOwnFrasAsync(cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var everyDay = Enum.GetValues<DayOfWeek>().Sum(IvaoFraReader.DayBit);

        database.IvaoFras.AddRange(
            new IvaoFra { Id = OwnFras[0], Callsign = "LIRF_Q_TWR", MinimumRating = 10, Days = everyDay, IsActive = true, SyncedAt = clock.UtcNow },
            new IvaoFra { Id = OwnFras[1], Callsign = "LIMC_MAR_APP", MinimumRating = 10, Days = everyDay, IsActive = false, SyncedAt = clock.UtcNow });

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveOwnFrasAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().IvaoFras
            .Where(fra => OwnFras.Contains(fra.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

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

    /// <summary>The fixtures, with an answer on the FRAs that says the division has none left.</summary>
    private sealed class NoFra(FixtureIvaoApiClient fixtures) : FixturesBeforeA2(fixtures), IIvaoApiClient
    {
        public Task<(IReadOnlyList<IvaoAtcPositionDto> Airports, IReadOnlyList<IvaoAtcPositionDto> Sectors)>
            GetAtcPositionsAsync(CancellationToken cancellationToken = default) =>
            Fixtures.GetAtcPositionsAsync(cancellationToken);

        public Task<IReadOnlyList<IvaoFraDto>?> GetFrasAsync(string countryId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoFraDto>?>([]);
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
