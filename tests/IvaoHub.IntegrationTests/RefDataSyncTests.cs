using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The airspace of a division is read from IVAO, never configured. This proves the snapshot is
/// taken, refreshed rather than duplicated, recorded in the job log, and that it is what makes a
/// FIR staff position mean something.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class RefDataSyncTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        // The OAuth client of a division is not necessarily allowed the reference endpoints, so the
        // tests read the same fixtures a developer without credentials would.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<(int Centers, int Airports)> SyncAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RefDataSyncJob>().RunAsync(cancellationToken);
    }

    [Fact]
    public async Task TakesTheSnapshotOfTheFirsAndAirportsOfTheDivision()
    {
        var token = TestContext.Current.CancellationToken;

        // The host already synchronised on start up, because the tables were empty.
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var centers = await database.IvaoCenters.AsNoTracking().ToListAsync(token);
        var airports = await database.IvaoAirports.AsNoTracking().ToListAsync(token);

        Assert.Contains(centers, center => center.Id == "LIRR");
        Assert.Contains(airports, airport => airport.Icao == "LIRF" && airport.CenterId == "LIRR");

        // The whole payload is kept, so a field nobody reads today does not have to be guessed at.
        Assert.All(centers, center => Assert.Contains("centerId", center.RawJson, StringComparison.Ordinal));
        Assert.All(airports, airport => Assert.False(string.IsNullOrWhiteSpace(airport.RunwaysJson)));
    }

    [Fact]
    public async Task RefreshesInsteadOfDuplicating()
    {
        var token = TestContext.Current.CancellationToken;

        await SyncAsync(token);
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var duplicated = await database.IvaoCenters
            .AsNoTracking()
            .GroupBy(center => center.Id)
            .AnyAsync(group => group.Count() > 1, token);

        Assert.False(duplicated);
        Assert.Equal(3, await database.IvaoCenters.CountAsync(token));
    }

    [Fact]
    public async Task RecordsEveryRunInTheJobLog()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var last = await database.JobsLog
            .AsNoTracking()
            .Where(entry => entry.Job == RefDataSyncJob.JobName)
            .OrderByDescending(entry => entry.StartedAt)
            .FirstAsync(token);

        Assert.Equal("succeeded", last.Status);
        Assert.NotNull(last.FinishedAt);
        Assert.Contains("centre", last.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FirPositionsAreRecognisedOnceTheSnapshotExists()
    {
        // The point of the whole phase: before the snapshot, LIRR-CH is an unrecognised string that
        // is kept but worth nothing; after it, it is the chief of a FIR of this division.
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var sync = scope.ServiceProvider.GetRequiredService<UserSyncService>();

        var profile = new IvaoUserProfile(
            Vid: 700001,
            FirstName: "Fir",
            LastName: "Chief",
            PublicNickname: null,
            DivisionCode: "IT",
            CountryId: "IT",
            RatingAtc: 5,
            RatingPilot: 5,
            DiscordId: null,
            LanguageId: "it",
            IvaoIsStaff: true,
            IvaoIsSupervisor: false,
            StaffPositions: ["LIRR-CH"]);

        var signedIn = await sync.UpsertAsync(profile, token);

        var position = Assert.Single(signedIn.Positions);
        Assert.Equal(StaffRole.FirChief, position.Role);
        Assert.Equal("LIRR", position.Fir);
        Assert.Null(position.Department);
        Assert.True(signedIn.User.IsStaff);
    }

    [Fact]
    public async Task AFirOfAnotherCountryIsStillNotRecognised()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var sync = scope.ServiceProvider.GetRequiredService<UserSyncService>();

        var profile = new IvaoUserProfile(
            Vid: 700002,
            FirstName: "Somebody",
            LastName: "Else",
            PublicNickname: null,
            DivisionCode: "FR",
            CountryId: "FR",
            RatingAtc: null,
            RatingPilot: null,
            DiscordId: null,
            LanguageId: "fr",
            IvaoIsStaff: true,
            IvaoIsSupervisor: false,
            StaffPositions: ["LFFF-CH"]);

        var signedIn = await sync.UpsertAsync(profile, token);

        Assert.Empty(signedIn.Positions);
        Assert.False(signedIn.User.IsStaff);

        // Not recognised is not the same as lost: the raw position is still there.
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.True(await database.UserStaffPositions.AnyAsync(
            row => row.Vid == 700002 && row.Position == "LFFF-CH",
            token));
    }

    [Fact]
    public async Task WhatIvaoNoLongerListsLeavesTheSnapshot()
    {
        // The endpoints answer with the whole set for a country, so a row missing from a non empty
        // answer has been decommissioned. Keeping it would go on making a staff position of a FIR
        // that no longer exists look like one of ours.
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            database.IvaoCenters.Add(new IvaoCenter
            {
                Id = "LIZZ",
                Name = "A centre IVAO has since retired",
                CountryId = "IT",
                RawJson = "{}",
            });
            await database.SaveChangesAsync(token);
        }

        await SyncAsync(token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

            Assert.False(await database.IvaoCenters.AnyAsync(center => center.Id == "LIZZ", token));
            Assert.True(await database.IvaoCenters.AnyAsync(center => center.Id == "LIRR", token));
        }
    }

    [Fact]
    public async Task AFailedRunWritesTheJobRowAndNoneOfTheSnapshotItHadStaged()
    {
        // The failure path used to save the job row with everything the run had already tracked
        // still in the change tracker: the row said "failed" over a snapshot that was half written.
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var namesBefore = await database.IvaoCenters.AsNoTracking()
            .ToDictionaryAsync(center => center.Id, center => center.Name, token);

        var job = new RefDataSyncJob(
            new HalfBrokenIvaoApiClient(),
            database,
            scope.ServiceProvider.GetRequiredService<IFirDirectory>(),
            scope.ServiceProvider.GetRequiredService<IOptions<DivisionOptions>>(),
            scope.ServiceProvider.GetRequiredService<IClock>(),
            NullLogger<RefDataSyncJob>.Instance);

        // It never throws: a synchronisation that could have waited until tomorrow must not be the
        // thing that takes the site down.
        Assert.Equal((0, 0), await job.RunAsync(token));

        var last = await database.JobsLog.AsNoTracking()
            .Where(entry => entry.Job == RefDataSyncJob.JobName)
            .OrderByDescending(entry => entry.Id)
            .FirstAsync(token);

        Assert.Equal("failed", last.Status);
        Assert.NotNull(last.FinishedAt);

        // The centres the broken run had already staged did not reach the database with it.
        var namesAfter = await database.IvaoCenters.AsNoTracking()
            .ToDictionaryAsync(center => center.Id, center => center.Name, token);

        Assert.Equal(namesBefore, namesAfter);
    }

    /// <summary>Answers for the centres, then falls over on the airports, halfway through a run.</summary>
    private sealed class HalfBrokenIvaoApiClient : IIvaoApiClient
    {
        public Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(
            string countryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoCenterDto>>(
            [
                new("LIRR", "Renamed by a run that will not finish", countryId, "{}"),
            ]);

        public Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(
            string countryId,
            bool includeRunways = true,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("IVAO fell over halfway through.");

        public Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default) =>
            Task.FromResult<JsonElement?>(null);
    }

    [Fact]
    public async Task TheFirDirectoryAnswersFromTheSnapshot()
    {
        var token = TestContext.Current.CancellationToken;
        await SyncAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var firs = await scope.ServiceProvider.GetRequiredService<IFirDirectory>().GetFirIdsAsync(token);

        Assert.Contains("LIRR", firs);
        Assert.Contains("LIMM", firs);
        Assert.DoesNotContain("LFFF", firs);
    }
}
