using IvaoHub.Core.Atc;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The archive of ATC sessions asked about one controller (M4, E10b, note
/// <c>decisions/2026-09-30-le-sessioni-condivise-per-vid.md</c>): every connection says who had the position, and the core
/// answers the connections of one controller in an interval — a year of them for the events' roster (design M4 §4.3), the
/// hour of a shift for its attendance (§4.5).
/// <para>On a fake of vIPI's archive in <b>a database of its own</b>, as the real one is, with the view written as vIPI's
/// migration writes it: the hub reaches it only through <c>ConnectionStrings:AtcData</c>, as a user that may only
/// <c>SELECT</c> on the view — so a query that read the hub's own database, or anything but the view, finds nothing to
/// read. VIDs 761030–761039; no row of the hub is written.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AtcActivitySourceTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const string ArchiveDatabase = "ivaohub_e10b_atc_archive";

    /// <summary>The controller asked about.</summary>
    private const int Controller = 761030;

    /// <summary>Another controller, on the same tower the same evening.</summary>
    private const int Colleague = 761031;

    /// <summary>Whoever opened the oldest rows of the archive.</summary>
    private const int Pioneer = 761032;

    /// <summary>A controller the archive has never seen.</summary>
    private const int Stranger = 761039;

    /// <summary>The evening of an event: the controller's shift on the tower, 18:00 to 19:00.</summary>
    private static readonly DateTime ShiftFrom = new(2026, 9, 12, 18, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ShiftTo = ShiftFrom.AddHours(1);

    /// <summary>Where the archive is complete from: its oldest row of the division's positions, and of the world's.</summary>
    private static readonly DateTime DivisionSince = new(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime WorldSince = new(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Session[] Sessions =
    [
        new(1, Pioneer, "LIMM_CTR", DivisionSince, DivisionSince.AddHours(1), false),
        new(2, Pioneer, "LFPG_TWR", WorldSince, WorldSince.AddHours(1), true),

        // The controller's year: the tower more than anything, the approach, once a French sector — and before the year, a
        // tower that no longer counts.
        new(3, Controller, "LIRF_TWR", new(2025, 8, 1, 18, 0, 0, DateTimeKind.Utc), new(2025, 8, 1, 20, 0, 0, DateTimeKind.Utc), false),
        new(4, Controller, "LIRF_TWR", new(2026, 3, 10, 18, 0, 0, DateTimeKind.Utc), new(2026, 3, 10, 20, 0, 0, DateTimeKind.Utc), false),
        new(5, Controller, "LIRF_TWR", new(2026, 6, 5, 17, 30, 0, DateTimeKind.Utc), new(2026, 6, 5, 19, 0, 0, DateTimeKind.Utc), false),
        new(6, Controller, "LIRF_APP", new(2026, 7, 20, 19, 0, 0, DateTimeKind.Utc), new(2026, 7, 20, 21, 0, 0, DateTimeKind.Utc), false),
        new(7, Controller, "LFMM_CTR", new(2026, 9, 1, 19, 0, 0, DateTimeKind.Utc), new(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc), true),
        new(8, Colleague, "LIRF_APP", new(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc), new(2026, 6, 1, 19, 0, 0, DateTimeKind.Utc), false),

        // The evening: the controller opens the tower before the shift and drops, the colleague takes it, the controller comes
        // back and is still on it; later, the approach.
        new(9, Controller, "LIRF_TWR", ShiftFrom.AddMinutes(-10), ShiftFrom.AddMinutes(40), false),
        new(10, Colleague, "LIRF_TWR", ShiftFrom.AddMinutes(40), ShiftFrom.AddHours(2), false),
        new(11, Controller, "LIRF_TWR", ShiftFrom.AddMinutes(50), null, false),
        new(12, Controller, "LIRF_APP", ShiftTo.AddHours(2), ShiftTo.AddHours(3), false),
    ];

    private HubWebApplicationFactory _factory = null!;
    private string _archive = null!;

    public async ValueTask InitializeAsync()
    {
        _archive = await ArchiveAsync(TestContext.Current.CancellationToken);

        // A host that starts on an empty snapshot of the reference data fills it: from the files a developer without
        // credentials reads, never from IVAO.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await AsRootAsync($"DROP DATABASE IF EXISTS `{ArchiveDatabase}`", CancellationToken.None);
    }

    /// <summary>
    /// The hour of a shift: the controller's own connections in it — one begun before, one still open —, and who had the
    /// position in between, from the connections of everybody, each of which now says whose it was.
    /// </summary>
    [Fact]
    public async Task TheHourOfAShiftHoldsTheControllersConnectionsAndWhoCoveredThePosition()
    {
        var token = TestContext.Current.CancellationToken;
        await using var host = WithArchive(AtcDataOptions.Vipi, _archive);
        await using var scope = host.Services.CreateAsyncScope();
        var archive = scope.ServiceProvider.GetRequiredService<IAtcActivitySource>();

        var mine = await archive.SessionsOfAsync(Controller, ShiftFrom, ShiftTo, token);

        Assert.NotNull(mine);
        Assert.Equal([ShiftFrom.AddMinutes(-10), ShiftFrom.AddMinutes(50)], mine.Of("LIRF_TWR").Select(presence => presence.StartedAt));
        Assert.Equal<DateTime?>([ShiftFrom.AddMinutes(40), null], mine.Of("lirf_twr").Select(presence => presence.EndedAt));
        Assert.All(mine.Online, presence => Assert.Equal(Controller, presence.Vid));
        Assert.Empty(mine.Of("LIRF_APP"));
        Assert.True(mine.Covers("LIRF_TWR", ShiftFrom));

        var everybody = await archive.OnlineAsync(ShiftFrom, ShiftTo, token);

        Assert.NotNull(everybody);
        Assert.Equal<int?>([Controller, Colleague, Controller], everybody.Of("LIRF_TWR").Select(presence => presence.Vid));
    }

    /// <summary>
    /// A year of one controller: their connections and nobody else's, position by position, with how far back the archive is
    /// complete for each half — the division's the whole year, the world's only since the archive kept it.
    /// </summary>
    [Fact]
    public async Task AYearOfAControllerIsTheirConnectionsPositionByPosition()
    {
        var token = TestContext.Current.CancellationToken;
        await using var host = WithArchive(AtcDataOptions.Vipi, _archive);
        await using var scope = host.Services.CreateAsyncScope();
        var archive = scope.ServiceProvider.GetRequiredService<IAtcActivitySource>();

        var from = ShiftFrom.AddYears(-1);
        var year = await archive.SessionsOfAsync(Controller, from, ShiftTo.AddHours(4), token);

        Assert.NotNull(year);
        Assert.Equal([4L, 5L, 9L, 11L], Ids(year.Of("LIRF_TWR")));
        Assert.Equal([6L, 12L], Ids(year.Of("LIRF_APP")));
        Assert.Equal([7L], Ids(year.Of("LFMM_CTR")));
        Assert.Equal(7, year.Online.Count);
        Assert.All(year.Online, presence => Assert.Equal(Controller, presence.Vid));

        Assert.Equal(DivisionSince, year.DivisionSince);
        Assert.Equal(WorldSince, year.WorldSince);
        Assert.True(year.Covers("LIRF_TWR", from));
        Assert.False(year.Covers("LFMM_CTR", from));

        // A controller the archive has never seen has no connection, which is an answer: not «not available».
        var stranger = await archive.SessionsOfAsync(Stranger, from, ShiftTo, token);
        Assert.NotNull(stranger);
        Assert.Empty(stranger.Online);
    }

    /// <summary>
    /// <c>atcData.source: none</c> — a fork's, and IT's until its view is switched on —: both questions are «not available».
    /// So is the new one on an archive written before E10b, which only knows who was online.
    /// </summary>
    [Fact]
    public async Task WithoutAnArchiveBothQuestionsAreNotAvailable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var host = WithArchive(AtcDataOptions.None, _archive);
        await using var scope = host.Services.CreateAsyncScope();
        var archive = scope.ServiceProvider.GetRequiredService<IAtcActivitySource>();

        Assert.IsType<UnavailableAtcActivitySource>(archive);
        Assert.Null(await archive.OnlineAsync(ShiftFrom, ShiftTo, token));
        Assert.Null(await archive.SessionsOfAsync(Controller, ShiftFrom, ShiftTo, token));

        IAtcActivitySource before = new ArchiveBeforeE10b();
        Assert.NotNull(await before.OnlineAsync(ShiftFrom, ShiftTo, token));
        Assert.Null(await before.SessionsOfAsync(Controller, ShiftFrom, ShiftTo, token));
    }

    /// <summary>An archive that cannot be read — its view gone — is «not available» to the new question as to the old one.</summary>
    [Fact]
    public async Task AnArchiveThatCannotBeReadIsNotAvailable()
    {
        var token = TestContext.Current.CancellationToken;
        await AsRootAsync($"DROP VIEW `{ArchiveDatabase}`.`{VipiShareDbContext.SessionsView}`", token);

        await using var host = WithArchive(AtcDataOptions.Vipi, _archive);
        await using var scope = host.Services.CreateAsyncScope();
        var archive = scope.ServiceProvider.GetRequiredService<IAtcActivitySource>();

        Assert.Null(await archive.SessionsOfAsync(Controller, ShiftFrom, ShiftTo, token));
        Assert.Null(await archive.OnlineAsync(ShiftFrom, ShiftTo, token));
    }

    /// <summary>
    /// A division that names the archive before its connection string is in the secrets: the hub starts, and the archive is
    /// «not available», as its source has always said. Until E10b the view's context was built with the source, outside the
    /// source's own try, and the start of the hub failed on it.
    /// </summary>
    [Fact]
    public async Task AnArchiveNamedBeforeItsConnectionStringIsNotAvailable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var host = WithArchive(AtcDataOptions.Vipi, archive: null);
        await using var scope = host.Services.CreateAsyncScope();
        var archive = scope.ServiceProvider.GetRequiredService<IAtcActivitySource>();

        Assert.IsType<VipiAtcActivitySource>(archive);
        Assert.Null(await archive.OnlineAsync(ShiftFrom, ShiftTo, token));
        Assert.Null(await archive.SessionsOfAsync(Controller, ShiftFrom, ShiftTo, token));
    }

    // ---- helpers -----------------------------------------------------------------------------

    /// <summary>
    /// The hub with <c>division.json → atcData.source</c> set to <paramref name="source"/>, and the archive's connection when
    /// there is one: the real choice of <c>AddAtcActivity</c>, whatever IT's own file says.
    /// </summary>
    private WebApplicationFactory<Program> WithArchive(string source, string? archive) =>
        _factory.WithWebHostBuilder(builder =>
        {
            if (archive is not null)
            {
                builder.UseSetting($"ConnectionStrings:{AtcServiceCollectionExtensions.ConnectionStringName}", archive);
            }

            builder.ConfigureTestServices(services => services.AddSingleton<IOptions<DivisionOptions>>(provider => Options.Create(
                provider.GetRequiredService<IOptionsFactory<DivisionOptions>>().Create(Options.DefaultName) with
                {
                    AtcData = new AtcDataOptions { Source = source },
                })));
        });

    /// <summary>
    /// vIPI's table as far as the view reads it, with its index on the controller, the view of the contract over it word for
    /// word (vIPI's migration <c>VistaCondivisaSessioniAtc</c>), the grant its administrator gives the hub, and the rows.
    /// </summary>
    private async Task<string> ArchiveAsync(CancellationToken cancellationToken)
    {
        await AsRootAsync(
            $"""
            DROP DATABASE IF EXISTS `{ArchiveDatabase}`;
            CREATE DATABASE `{ArchiveDatabase}` CHARACTER SET {HubDbContext.CharSet} COLLATE {HubDbContext.Collation};
            USE `{ArchiveDatabase}`;
            CREATE TABLE AtcSessions (
                SessionId bigint NOT NULL PRIMARY KEY, UserId int NOT NULL, Callsign varchar(32) NOT NULL, Position varchar(16) NULL,
                Frequency varchar(16) NULL, StartUtc datetime(6) NOT NULL, EndUtc datetime(6) NULL, DurationSeconds int NOT NULL,
                Rating int NULL, IsOutsideDivision tinyint(1) NOT NULL, TrafficCount int NOT NULL DEFAULT 0,
                KEY IX_AtcSessions_StartUtc (StartUtc), KEY IX_AtcSessions_UserId_StartUtc (UserId, StartUtc));
            CREATE OR REPLACE SQL SECURITY DEFINER VIEW v_share_atc_sessions AS
            SELECT SessionId         AS session_id,
                   UserId            AS vid,
                   Callsign          AS callsign,
                   Position          AS position,
                   Frequency         AS frequency,
                   StartUtc          AS start_utc,
                   EndUtc            AS end_utc,
                   DurationSeconds   AS duration_seconds,
                   Rating            AS rating,
                   IsOutsideDivision AS is_outside_division
            FROM AtcSessions;
            GRANT SELECT ON `{ArchiveDatabase}`.v_share_atc_sessions TO 'ivaohub'@'%';
            """,
            cancellationToken);

        await using var connection = new MySqlConnection(RootOn(ArchiveDatabase));
        await connection.OpenAsync(cancellationToken);

        foreach (var session in Sessions)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO AtcSessions (SessionId, UserId, Callsign, Position, Frequency, StartUtc, EndUtc, DurationSeconds, Rating, IsOutsideDivision) "
                + "VALUES (@id, @vid, @callsign, @position, NULL, @start, @end, @seconds, 5, @outside)";
            command.Parameters.AddWithValue("@id", session.Id);
            command.Parameters.AddWithValue("@vid", session.Vid);
            command.Parameters.AddWithValue("@callsign", session.Callsign);
            command.Parameters.AddWithValue("@position", session.Callsign[(session.Callsign.LastIndexOf('_') + 1)..]);
            command.Parameters.AddWithValue("@start", session.Start);
            command.Parameters.AddWithValue("@end", session.End is { } end ? end : DBNull.Value);
            command.Parameters.AddWithValue("@seconds", session.End is { } ended ? (int)(ended - session.Start).TotalSeconds : 0);
            command.Parameters.AddWithValue("@outside", session.Outside);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new MySqlConnectionStringBuilder(mariaDb.ConnectionString) { Database = ArchiveDatabase }.ConnectionString;
    }

    /// <summary>The application user of the container may not create a database, nor give a grant: root does.</summary>
    private async Task AsRootAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string RootOn(string database) =>
        new MySqlConnectionStringBuilder(mariaDb.RootConnectionString) { Database = database }.ConnectionString;

    /// <summary>Which rows of <see cref="Sessions"/> a list of connections is, told by their start and callsign.</summary>
    private static long[] Ids(IEnumerable<AtcPresence> presences) =>
    [
        .. presences.Select(presence => Sessions.Single(session =>
            session.Start == presence.StartedAt && string.Equals(session.Callsign, presence.Callsign, StringComparison.Ordinal)).Id),
    ];

    /// <summary>A row of the archive; <paramref name="Outside"/> is a position outside the division, which vIPI marks.</summary>
    private sealed record Session(long Id, int Vid, string Callsign, DateTime Start, DateTime? End, bool Outside);

    /// <summary>An archive written before E10b: it says who was online, and was never asked about one controller.</summary>
    private sealed class ArchiveBeforeE10b : IAtcActivitySource
    {
        public Task<AtcActivity?> OnlineAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<AtcActivity?>(new AtcActivity([], null, null, []));
    }
}
