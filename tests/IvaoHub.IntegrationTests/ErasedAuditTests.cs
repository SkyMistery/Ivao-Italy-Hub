using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The audit of a row already gone when its person is erased (M4, E10n, note 2026-10-11-i-dati-di-una-persona-nell-audit), through
/// the real host, the core and the test module, whose item marks its title as the data of the member it is about
/// (<see cref="ErasedWithAttribute"/>), the way a booking's callsign is its pilot's:
/// <list type="bullet">
/// <item>an item about the person, taken away before the erasure, keeps its audit rows — created, updated, deleted — without its
/// title, under the pseudonym, with the rest of its history;</item>
/// <item>an item about another member, taken away the same way, keeps its title;</item>
/// <item>an item about the person that stays is its eraser's, as before: emptied whole when the eraser deletes it, its history kept
/// when the module keeps it.</item>
/// </list>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ErasedAuditTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The person erased: never used by another test, so that erasing them takes nothing another test counts on (E10n).
    private const int PersonVid = 761100;

    // ErasureTests' people, seeded the same way: the other member is only named on a row, the super administrator signs in.
    private const int OtherVid = 780096;
    private const int SuperadminVid = 780097;

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _items = [];

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(PersonVid, superadmin: false, token);
        await SeedUserAsync(SuperadminVid, superadmin: true, token);
    }

    public async ValueTask DisposeAsync()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            await sample.Items.IgnoreQueryFilters().Where(row => _items.Contains(row.Id)).ExecuteDeleteAsync(CancellationToken.None);
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ARowGoneBeforeTheErasureLosesThePersonsDataInItsAudit()
    {
        var token = TestContext.Current.CancellationToken;
        var stamp = Guid.NewGuid().ToString("N");
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        // The person's row, written, corrected and taken away a week before: a booking its pilot withdrew.
        var withdrawn = await CreateItemAsync(superadmin, $"e10n-withdrawn-{stamp}", PersonVid, token);
        await UpdateItemAsync(superadmin, withdrawn, $"e10n-corrected-{stamp}", PersonVid, token);
        await DeleteItemAsync(superadmin, withdrawn, token);

        // Another member's row, gone the same way.
        var others = await CreateItemAsync(superadmin, $"e10n-other-{stamp}", OtherVid, token);
        await DeleteItemAsync(superadmin, others, token);

        // The person's rows that are still there: one the eraser takes, one the module keeps as it is.
        var taken = await CreateItemAsync(superadmin, $"e10n-taken-{stamp}", PersonVid, token);
        var kept = await CreateItemAsync(superadmin, $"{SampleEraser.KeptPrefix}-e10n-{stamp}", PersonVid, token);
        _items.AddRange([taken, kept]);

        var pseudonym = await EraseAsync(superadmin, PersonVid, token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking();

        // The withdrawn row: every one of its rows is still there, in order, by the same writer, under the pseudonym and without
        // the title it had — the created one, the correction's two sides, the deleted one.
        var gone = await RowsOfAsync(audit, withdrawn, token);
        Assert.Equal(["created", "updated", "deleted"], gone.Select(row => row.Action));
        Assert.All(gone, row => Assert.Equal(SuperadminVid, row.Vid));
        Assert.All(gone, row => Assert.DoesNotContain(stamp, $"{row.BeforeJson}{row.AfterJson}", StringComparison.Ordinal));

        var created = JsonNode.Parse(gone[0].AfterJson!)!.AsObject();
        Assert.Null(created["Title"]);
        Assert.Equal(pseudonym, created["StakeholderVid"]!.GetValue<int>());
        Assert.Equal(nameof(Visibility.Department), created["Visibility"]!.GetValue<string>());

        var updated = (Before: JsonNode.Parse(gone[1].BeforeJson!)!.AsObject(), After: JsonNode.Parse(gone[1].AfterJson!)!.AsObject());
        Assert.True(updated.Before.ContainsKey("Title") && updated.After.ContainsKey("Title"));
        Assert.Null(updated.Before["Title"]);
        Assert.Null(updated.After["Title"]);

        var deleted = JsonNode.Parse(gone[2].BeforeJson!)!.AsObject();
        Assert.Null(deleted["Title"]);
        Assert.Equal(pseudonym, deleted["StakeholderVid"]!.GetValue<int>());
        Assert.Equal(withdrawn, deleted["Id"]!.GetValue<long>());

        // Another member's row: untouched, title and all.
        var theirs = await RowsOfAsync(audit, others, token);
        Assert.Equal(["created", "deleted"], theirs.Select(row => row.Action));
        Assert.Equal($"e10n-other-{stamp}", JsonNode.Parse(theirs[0].AfterJson!)!["Title"]!.GetValue<string>());
        Assert.Equal($"e10n-other-{stamp}", JsonNode.Parse(theirs[1].BeforeJson!)!["Title"]!.GetValue<string>());
        Assert.Equal(OtherVid, JsonNode.Parse(theirs[1].BeforeJson!)!["StakeholderVid"]!.GetValue<int>());

        // The rows that stayed are their eraser's, as before this phase: the one it deleted has empty copies and its "erased"
        // row; the one the module keeps keeps its title in its history.
        var erased = await RowsOfAsync(audit, taken, token);
        Assert.Equal(["created", "erased"], erased.Select(row => row.Action));
        Assert.All(erased, row => Assert.True(row.BeforeJson is null && row.AfterJson is null));

        var ban = await RowsOfAsync(audit, kept, token);
        Assert.Equal(["created"], ban.Select(row => row.Action));
        Assert.Equal($"{SampleEraser.KeptPrefix}-e10n-{stamp}", JsonNode.Parse(ban[0].AfterJson!)!["Title"]!.GetValue<string>());
    }

    [Fact]
    public async Task ASecondErasureOfThePersonChangesNothingMore()
    {
        var token = TestContext.Current.CancellationToken;
        var stamp = Guid.NewGuid().ToString("N");
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var withdrawn = await CreateItemAsync(superadmin, $"e10n-again-{stamp}", PersonVid, token);
        await DeleteItemAsync(superadmin, withdrawn, token);

        await EraseAsync(superadmin, PersonVid, token);
        var first = await SnapshotAsync(withdrawn, token);

        // Run again — an erasure that failed half way is — it finds the VID nowhere, and the copies are as the first left them.
        await EraseAsync(superadmin, PersonVid, token);
        Assert.Equal(first, await SnapshotAsync(withdrawn, token));
        Assert.DoesNotContain(first, row => $"{row.BeforeJson}{row.AfterJson}".Contains(stamp, StringComparison.Ordinal));
    }

    /// <summary>
    /// A row that changed hands (after the review of #251): one still in the table is its eraser's, whoever it is about now, and
    /// keeps every copy; of one taken away, only the copies written while it was the person's lose the data — the side of the
    /// change that was theirs —, and the other member's keep it.
    /// </summary>
    [Fact]
    public async Task ARowThatChangedHandsKeepsTheOtherMembersData()
    {
        var token = TestContext.Current.CancellationToken;
        var stamp = Guid.NewGuid().ToString("N");
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var stays = await CreateItemAsync(superadmin, $"e10n-was-mine-{stamp}", PersonVid, token);
        await UpdateItemAsync(superadmin, stays, $"e10n-now-theirs-{stamp}", OtherVid, token);
        _items.Add(stays);

        var gone = await CreateItemAsync(superadmin, $"e10n-mine-{stamp}", PersonVid, token);
        await UpdateItemAsync(superadmin, gone, $"e10n-theirs-{stamp}", OtherVid, token);
        await DeleteItemAsync(superadmin, gone, token);

        await EraseAsync(superadmin, PersonVid, token);

        var kept = await SnapshotAsync(stays, token);
        Assert.Equal(["created", "updated"], kept.Select(row => row.Action));
        Assert.Equal($"e10n-was-mine-{stamp}", Title(kept[0].AfterJson));
        Assert.Equal($"e10n-was-mine-{stamp}", Title(kept[1].BeforeJson));
        Assert.Equal($"e10n-now-theirs-{stamp}", Title(kept[1].AfterJson));

        var emptied = await SnapshotAsync(gone, token);
        Assert.Equal(["created", "updated", "deleted"], emptied.Select(row => row.Action));
        Assert.Null(Title(emptied[0].AfterJson));
        Assert.Null(Title(emptied[1].BeforeJson));
        Assert.Equal($"e10n-theirs-{stamp}", Title(emptied[1].AfterJson));
        Assert.Equal($"e10n-theirs-{stamp}", Title(emptied[2].BeforeJson));
        Assert.DoesNotContain(emptied, row => $"{row.BeforeJson}{row.AfterJson}".Contains($"e10n-mine-{stamp}", StringComparison.Ordinal));
    }

    /// <summary>
    /// A second run after a first that failed half way (after the review of #251): the modules commit before the core, so a row
    /// that stayed already holds the pseudonym while the log still holds the VID. It is still in the table, so it is its eraser's,
    /// and its history is kept. The state the first run leaves is written here as it writes it — the person column only, with no
    /// audit row (the interceptor writes none for it in erasure mode).
    /// </summary>
    [Fact]
    public async Task ASecondRunAfterAFirstThatFailedHalfWayLeavesTheRowsThatStayed()
    {
        var token = TestContext.Current.CancellationToken;
        var stamp = Guid.NewGuid().ToString("N");
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var stayed = await CreateItemAsync(superadmin, $"e10n-stayed-{stamp}", PersonVid, token);
        _items.Add(stayed);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Items.IgnoreQueryFilters()
                .Where(row => row.Id == stayed)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.StakeholderVid, -PersonVid), token);
        }

        await EraseAsync(superadmin, PersonVid, token);

        var history = await SnapshotAsync(stayed, token);
        Assert.Equal(["created"], history.Select(row => row.Action));
        Assert.Equal($"e10n-stayed-{stamp}", Title(history[0].AfterJson));
    }

    /// <summary>
    /// What the erased person did as staff (after the review of #251): a row about another member that they wrote and took away
    /// before the erasure keeps its data — with their number in <c>CreatedBy</c> and <c>UpdatedBy</c>, which take the pseudonym,
    /// and as the very title of the row, which is a word like any other. Only the marked person column says whose the data is.
    /// </summary>
    [Fact]
    public async Task WhatThePersonDidAsStaffOnAnotherMembersRowKeepsItsData()
    {
        var token = TestContext.Current.CancellationToken;
        var number = PersonVid.ToString(CultureInfo.InvariantCulture);

        // As a super administrator for a while, as in ErasureTests.
        await SeedUserAsync(PersonVid, superadmin: true, token);
        long theirs;
        using (var person = await SignedInAsync(PersonVid, token))
        {
            theirs = await CreateItemAsync(person, number, OtherVid, token);
            await DeleteItemAsync(person, theirs, token);
        }

        await SeedUserAsync(PersonVid, superadmin: false, token);
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var pseudonym = await EraseAsync(superadmin, PersonVid, token);

        var history = await SnapshotAsync(theirs, token);
        Assert.Equal(["created", "deleted"], history.Select(row => row.Action));
        Assert.All(history, row => Assert.Equal(pseudonym, row.Vid));

        var deleted = JsonNode.Parse(history[1].BeforeJson!)!.AsObject();
        Assert.Equal(number, deleted["Title"]!.GetValue<string>());
        Assert.Equal(OtherVid, deleted["StakeholderVid"]!.GetValue<int>());
        Assert.Equal((pseudonym, pseudonym), (deleted["CreatedBy"]!.GetValue<int>(), deleted["UpdatedBy"]!.GetValue<int>()));
        Assert.Equal(number, Title(history[0].AfterJson));
    }

    /// <summary>
    /// Every mark of the hub names a person column of its own entity (<see cref="PersonColumns.ErasedWith"/>): a mark that did not
    /// would make every erasure fail, so it is caught here, in the core's contexts and in every module's, the day it is written.
    /// </summary>
    [Fact]
    public async Task EveryMarkOfThePersonsDataNamesAPersonColumn()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var modules = scope.ServiceProvider.GetRequiredService<ModuleRegistry>();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<HubDbContext>(),
            .. modules.Enabled
                .SelectMany(module => module.DbContextTypes)
                .Select(type => (DbContext)scope.ServiceProvider.GetRequiredService(type)),
        ];

        var marks = contexts
            .SelectMany(context => context.Model.GetEntityTypes().Where(entity => PersonColumns.RewrittenBy(context, entity)))
            .SelectMany(entity => PersonColumns.ErasedWith(entity)
                .SelectMany(group => group.Columns.Select(column => $"{entity.GetTableName()}.{column.Name} of {group.Person.Name}")))
            .ToList();

        // The test module's, at least: the mark this class proves the erasure with.
        Assert.Contains("smp_items.Title of StakeholderVid", marks);
    }

    private sealed record AuditCopy(string Action, int Vid, string? BeforeJson, string? AfterJson);

    private static string? Title(string? json) => JsonNode.Parse(json!)!["Title"]?.GetValue<string>();

    private static async Task<List<AuditCopy>> RowsOfAsync(IQueryable<AuditLogEntry> audit, long id, CancellationToken cancellationToken)
    {
        var key = id.ToString(CultureInfo.InvariantCulture);
        return await audit
            .Where(row => row.Entity == "smp_items" && row.EntityId == key)
            .OrderBy(row => row.Id)
            .Select(row => new AuditCopy(row.Action, row.Vid, row.BeforeJson, row.AfterJson))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<AuditCopy>> SnapshotAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await RowsOfAsync(scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking(), id, cancellationToken);
    }

    private static async Task<int> EraseAsync(HttpClient client, int vid, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync(new Uri($"{ErasureEndpoints.Pattern}/{vid}", UriKind.Relative), null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("pseudonym").GetInt32();
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private static object Item(string title, int stakeholder) => new
    {
        title,
        visibility = nameof(Visibility.Department),
        ownerDepartments = new[] { nameof(Department.ED) },
        stakeholderVid = stakeholder,
    };

    private static async Task<long> CreateItemAsync(HttpClient client, string title, int stakeholder, CancellationToken cancellationToken)
    {
        using var created = await client.PostAsJsonAsync(SampleModule.ItemsPattern, Item(title, stakeholder), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task UpdateItemAsync(HttpClient client, long id, string title, int stakeholder, CancellationToken cancellationToken)
    {
        using var updated = await client.PutAsJsonAsync($"{SampleModule.ItemsPattern}/{id}", Item(title, stakeholder), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    private static async Task DeleteItemAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var deleted = await client.DeleteAsync(new Uri($"{SampleModule.ItemsPattern}/{id}", UriKind.Relative), cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    private async Task SeedUserAsync(int vid, bool superadmin, CancellationToken cancellationToken)
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

        user.FirstName = "Erasure";
        user.LastName = "Test";
        user.IsSuperadmin = superadmin;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }
}
