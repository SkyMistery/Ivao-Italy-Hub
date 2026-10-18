using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Awards;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// «Did this source row give an award?» (M4, E10x; note 2026-10-18-un-segnale-diventato-award): <see cref="AwardSignalQueries.AssignedAsync"/>
/// on the hub's context and on the test module's, which maps the queue as every module context does. Handled is the only yes; inside a
/// transaction the signal rows are locked, so an assignment in the same instant passes before or after the caller, never in between —
/// proven both ways, with the deterministic pattern of the events' races (E6a): a transaction holds a row, the other side waits, the test
/// sees the wait in <c>INNODB_TRX</c> and only then lets go.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AwardSignalQueriesTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The people of AwardsTests, in the range the tours module owns in the shared database (design M2 section 13).
    private const int SuperadminVid = 780041;
    private const int PilotVid = 780043;

    /// <summary>A module that is not the test one, for the signals the question must not see.</summary>
    private const string OtherModule = "e10x-other";

    private readonly string _prefix = $"e10x:{Guid.NewGuid():N}";
    private readonly List<long> _samples = [];
    private readonly List<long> _awards = [];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        await SeedSuperadminAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var token = CancellationToken.None;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var sources = _samples.Select(id => $"event:{id}").ToList();
            await hub.AwardAssignments.Where(row => _awards.Contains(row.AwardId)).ExecuteDeleteAsync(token);
            await hub.Awards.Where(row => _awards.Contains(row.Id)).ExecuteDeleteAsync(token);
            await hub.AwardSignals
                .Where(row => row.SourceId.StartsWith(_prefix) || (row.SourceModule == SampleModule.ModuleKey && sources.Contains(row.SourceId)))
                .ExecuteDeleteAsync(token);
            await hub.SearchIndex.IgnoreQueryFilters()
                .Where(row => row.SourceModule == SampleModule.ModuleKey && sources.Contains(row.SourceId))
                .ExecuteDeleteAsync(token);
            await hub.CalendarEntries.IgnoreQueryFilters()
                .Where(row => row.SourceModule == SampleModule.ModuleKey && sources.Contains(row.SourceId))
                .ExecuteDeleteAsync(token);
            await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Events
                .Where(row => _samples.Contains(row.Id))
                .ExecuteDeleteAsync(token);
        }

        await _factory.DisposeAsync();
    }

    /// <summary>Only a handled signal is an award: not a pending one, not a dismissed one, not a source with none, not another module's.</summary>
    [Fact]
    public async Task OnlyAHandledSignalOfTheModuleIsAnAward()
    {
        var token = TestContext.Current.CancellationToken;
        var handled = $"{_prefix}:handled";
        var pending = $"{_prefix}:pending";
        var dismissed = $"{_prefix}:dismissed";
        var none = $"{_prefix}:none";
        var elsewhere = $"{_prefix}:elsewhere";

        await SignalRowAsync(SampleModule.ModuleKey, handled, AwardSignalStatus.Handled, token);
        await SignalRowAsync(SampleModule.ModuleKey, pending, AwardSignalStatus.Pending, token);
        await SignalRowAsync(SampleModule.ModuleKey, dismissed, AwardSignalStatus.Dismissed, token);

        // The same source id handled in another module, and an id handled only there.
        await SignalRowAsync(OtherModule, pending, AwardSignalStatus.Handled, token);
        await SignalRowAsync(OtherModule, elsewhere, AwardSignalStatus.Handled, token);

        string[] asked = [handled, pending, dismissed, none, elsewhere, handled];

        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        // The same answer from the hub's context and from a module's, read plainly or under the lock.
        Assert.Equal([handled], await AwardSignalQueries.AssignedAsync(hub, SampleModule.ModuleKey, asked, token));
        Assert.Equal([handled], await AwardSignalQueries.AssignedAsync(module, SampleModule.ModuleKey, asked, token));

        await using (var transaction = await module.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token))
        {
            Assert.Equal([handled], await AwardSignalQueries.AssignedAsync(module, SampleModule.ModuleKey, asked, token));
        }

        // And the other module's question sees its own two.
        Assert.Equal(
            new HashSet<string>([pending, elsewhere]),
            await AwardSignalQueries.AssignedAsync(module, OtherModule, asked, token));
    }

    /// <summary>No source asked, nothing answered, in or out of a transaction.</summary>
    [Fact]
    public async Task NoSourceIsNoAward()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        Assert.Empty(await AwardSignalQueries.AssignedAsync(module, SampleModule.ModuleKey, [], token));

        await using var transaction = await module.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        Assert.Empty(await AwardSignalQueries.AssignedAsync(module, SampleModule.ModuleKey, [], token));
    }

    /// <summary>
    /// Inside a transaction the call waits for an assignment that holds the signal row, and then reads what it committed: a transaction of
    /// the test marks the pending signal handled, as the assignment's save does, and holds it; the call waits; the test commits; the call
    /// answers yes. A plain read would have answered no, at once.
    /// </summary>
    [Fact]
    public async Task InsideATransactionTheCallWaitsForAnAssignmentAndSeesIt()
    {
        var token = TestContext.Current.CancellationToken;
        var source = $"{_prefix}:held";
        var signal = await SignalRowAsync(SampleModule.ModuleKey, source, AwardSignalStatus.Pending, token);

        await using var held = _factory.Services.CreateAsyncScope();
        var hub = held.ServiceProvider.GetRequiredService<HubDbContext>();
        await using var holding = await hub.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await HandleAsync(hub, signal, token);

        await using var caller = _factory.Services.CreateAsyncScope();
        var module = caller.ServiceProvider.GetRequiredService<SampleDbContext>();
        await using var transaction = await module.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);

        var call = AwardSignalQueries.AssignedAsync(module, SampleModule.ModuleKey, [source], token);
        await WaitForALockWaitAsync(token);
        Assert.False(call.IsCompleted);

        await holding.CommitAsync(token);
        Assert.Equal([source], await call);
    }

    /// <summary>
    /// The other way round: an assignment that comes while the caller holds the signals waits for it, and once the caller has withdrawn the
    /// pending signal — as a row that stops pointing its member out does — the assignment finds nothing to answer: 409, and no award.
    /// </summary>
    [Fact]
    public async Task AnAssignmentWaitsForTheCallerAndFindsTheSignalWithdrawn()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var award = await CreateAwardAsync(superadmin, token);

        await using var caller = _factory.Services.CreateAsyncScope();
        var module = caller.ServiceProvider.GetRequiredService<SampleDbContext>();
        var sample = new SampleEvent
        {
            Title = $"{_prefix}-race",
            StartsAt = new DateTime(2030, 6, 1, 18, 0, 0, DateTimeKind.Utc),
            Status = PublishStatus.Published,
            AwardeeVid = PilotVid,
            ProposedAwardId = award,
        };
        module.Events.Add(sample);
        await module.SaveChangesAsync(token);
        _samples.Add(sample.Id);
        var signal = await SignalIdAsync(sample.SourceId, token);

        HttpResponseMessage answered;
        await using (var transaction = await module.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token))
        {
            Assert.Empty(await AwardSignalQueries.AssignedAsync(module, SampleModule.ModuleKey, [sample.SourceId], token));

            var request = superadmin.PostAsJsonAsync(
                AwardEndpoints.AssignmentsPattern,
                new { awardId = award, vid = PilotVid, reason = "e10x the race", signalId = signal },
                token);
            await WaitForALockWaitAsync(token);
            Assert.False(request.IsCompleted);

            sample.AwardeeVid = null;
            sample.Title = $"{_prefix}-race-withdrawn";
            await module.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            answered = await request;
        }

        using (answered)
        {
            Assert.Equal(HttpStatusCode.Conflict, answered.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.False(await hub.AwardSignals.AnyAsync(row => row.Id == signal, token));
        Assert.False(await hub.AwardAssignments.AnyAsync(row => row.SignalId == signal, token));
    }

    /// <summary>Outside a transaction it is a plain read: it does not wait for a held signal, and answers what is committed.</summary>
    [Fact]
    public async Task OutsideATransactionTheCallWaitsForNobody()
    {
        var token = TestContext.Current.CancellationToken;
        var source = $"{_prefix}:plain";
        var signal = await SignalRowAsync(SampleModule.ModuleKey, source, AwardSignalStatus.Pending, token);

        await using var held = _factory.Services.CreateAsyncScope();
        var hub = held.ServiceProvider.GetRequiredService<HubDbContext>();
        await using var holding = await hub.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await HandleAsync(hub, signal, token);

        await using var caller = _factory.Services.CreateAsyncScope();
        var module = caller.ServiceProvider.GetRequiredService<SampleDbContext>();

        // Well under the server's lock wait (50 seconds): a call that waited would be cancelled here, not hang — cancelled rather than
        // abandoned, so that its connection goes back to the pool clean.
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(token);
        patience.CancelAfter(TimeSpan.FromSeconds(10));
        Assert.Empty(await AwardSignalQueries.AssignedAsync(module, SampleModule.ModuleKey, [source], patience.Token));

        await holding.RollbackAsync(token);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private async Task<long> SignalRowAsync(string module, string source, AwardSignalStatus status, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        var row = new AwardSignal
        {
            SourceModule = module,
            SourceId = source,
            Vid = PilotVid,
            Reason = "e10x",
            Status = status,
            CreatedAt = now,
            HandledAt = status == AwardSignalStatus.Pending ? null : now,
            HandledBy = status == AwardSignalStatus.Pending ? null : SuperadminVid,

            // Told already: the mail to whoever assigns leaves these rows alone.
            NotifiedAt = now,
        };
        hub.AwardSignals.Add(row);
        await hub.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    /// <summary>What the assignment's save does to the line it answers, in the caller's transaction.</summary>
    private static Task HandleAsync(HubDbContext hub, long signal, CancellationToken cancellationToken) =>
        hub.AwardSignals.Where(row => row.Id == signal).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.Status, AwardSignalStatus.Handled)
                .SetProperty(row => row.HandledBy, SuperadminVid),
            cancellationToken);

    private async Task<long> SignalIdAsync(string source, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AwardSignals.AsNoTracking()
            .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == source)
            .Select(row => row.Id)
            .SingleAsync(cancellationToken);
    }

    /// <summary>Until a transaction waits for a lock: the side under test, behind the transaction the other side holds.</summary>
    /// <remarks>InnoDB refreshes <c>INNODB_TRX</c> only when nobody has read it for 100 ms: asked more often, it keeps answering what
    /// it saw the first time (CONTRIBUTING.md).</remarks>
    private async Task WaitForALockWaitAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.INNODB_TRX WHERE trx_state = 'LOCK WAIT'", connection);
        for (var tries = 0; tries < 40; tries++)
        {
            await Task.Delay(250, cancellationToken);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0)
            {
                return;
            }
        }

        Assert.Fail("Nothing ever waited for the transaction the other side holds.");
    }

    private async Task<long> CreateAwardAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var name = $"{_prefix}-award";
        using var created = await client.PostAsJsonAsync(
            AwardEndpoints.AwardsPattern,
            new
            {
                ownerDepartment = nameof(Department.FOD),
                name = new Dictionary<string, string> { ["it"] = name, ["en"] = name },
                description = (object?)null,
                criteria = (object?)null,
                imageMediaId = (long?)null,
                isActive = true,
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
        _awards.Add(id);
        return id;
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task SeedSuperadminAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == SuperadminVid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = SuperadminVid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "Awards";
        user.IsStaff = true;
        user.IsSuperadmin = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }
}
