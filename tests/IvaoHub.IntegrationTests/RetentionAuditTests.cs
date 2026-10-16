using System.Globalization;
using System.Text.Json.Nodes;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Rows a module deletes for retention leave the audit too (M4, E10s, note 2026-10-16-la-conservazione-nell-audit), through the real
/// host and the test module, whose item marks its title as the data of the member it is about (<see cref="ErasedWithAttribute"/>),
/// the way a registration's answers are its member's. The rows are written and deleted as a job writes them, with nobody signed in:
/// <list type="bullet">
/// <item>the rows deleted and handed to <see cref="AuditRedaction.EmptyDeletedAsync"/> lose the title in every copy — created, both
/// sides of the update, deleted —, whoever they were about, and keep the rest;</item>
/// <item>a row deleted in the same save and not handed over keeps its title: without the call, the data stays in the log;</item>
/// <item>a key handed over whose row is still in the table keeps its history;</item>
/// <item>in the module's transaction, a rollback takes the delete and the emptying back together.</item>
/// </list>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class RetentionAuditTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // ErasureTests' people, only named on the rows here: nobody is erased, nobody signs in.
    private const int MemberVid = 780096;
    private const int OtherVid = 780097;

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _items = [];

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
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
    public async Task RowsDeletedForRetentionLoseThePersonsDataInEveryCopy()
    {
        var token = TestContext.Current.CancellationToken;
        var stamp = Guid.NewGuid().ToString("N");

        // A row written and changed — passed to another member on the way —, one written once, one deleted with them and not
        // handed over, and one that stays.
        var changed = await CreateAsync($"e10s-first-{stamp}", MemberVid, token);
        await UpdateAsync(changed, $"e10s-second-{stamp}", OtherVid, token);
        var once = await CreateAsync($"e10s-once-{stamp}", MemberVid, token);
        var forgotten = await CreateAsync($"e10s-forgotten-{stamp}", MemberVid, token);
        var stays = await CreateAsync($"e10s-stays-{stamp}", MemberVid, token);
        _items.Add(stays);

        // The retention: the delete saved, then the call with the keys it deleted (and one it did not), in one transaction.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            await using var transaction = await sample.Database.BeginTransactionAsync(token);

            long[] deleted = [changed, once, forgotten];
            sample.Items.RemoveRange(await CrudSource.BackOffice<SampleItem>(sample).AsTracking()
                .Where(row => deleted.Contains(row.Id))
                .ToListAsync(token));
            await sample.SaveChangesAsync(token);

            await AuditRedaction.EmptyDeletedAsync<SampleItem, long>(sample, [changed, once, stays], token);
            await sample.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }

        // Every copy of the rows handed over, without the title, with the rest.
        var history = await CopiesAsync(changed, token);
        Assert.Equal(["created", "updated", "deleted"], history.Select(row => row.Action));
        Assert.All(history, row => Assert.DoesNotContain(stamp, $"{row.BeforeJson}{row.AfterJson}", StringComparison.Ordinal));

        var created = Json(history[0].AfterJson);
        Assert.True(created.ContainsKey("Title"));
        Assert.Null(created["Title"]);
        Assert.Equal(MemberVid, created["StakeholderVid"]!.GetValue<int>());
        Assert.Equal(nameof(Visibility.Department), created["Visibility"]!.GetValue<string>());

        var updated = (Before: Json(history[1].BeforeJson), After: Json(history[1].AfterJson));
        Assert.True(updated.Before.ContainsKey("Title") && updated.After.ContainsKey("Title"));
        Assert.Null(updated.Before["Title"]);
        Assert.Null(updated.After["Title"]);
        Assert.Equal((MemberVid, OtherVid), (updated.Before["StakeholderVid"]!.GetValue<int>(), updated.After["StakeholderVid"]!.GetValue<int>()));

        var gone = Json(history[2].BeforeJson);
        Assert.Null(gone["Title"]);
        Assert.Equal(OtherVid, gone["StakeholderVid"]!.GetValue<int>());
        Assert.Equal(changed, gone["Id"]!.GetValue<long>());

        var single = await CopiesAsync(once, token);
        Assert.Equal(["created", "deleted"], single.Select(row => row.Action));
        Assert.Null(Json(single[0].AfterJson)["Title"]);
        Assert.Null(Json(single[1].BeforeJson)["Title"]);

        // Deleted in the same save but not handed over: the title is still in the log. This is what the call is for.
        var kept = await CopiesAsync(forgotten, token);
        Assert.Equal(["created", "deleted"], kept.Select(row => row.Action));
        Assert.Equal($"e10s-forgotten-{stamp}", Title(kept[0].AfterJson));
        Assert.Equal($"e10s-forgotten-{stamp}", Title(kept[1].BeforeJson));

        // Handed over but still in the table: its history is its own.
        var standing = await CopiesAsync(stays, token);
        Assert.Equal(["created"], standing.Select(row => row.Action));
        Assert.Equal($"e10s-stays-{stamp}", Title(standing[0].AfterJson));

        // A second call — a job run again — changes nothing more.
        var before = await CopiesAsync(changed, token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            await AuditRedaction.EmptyDeletedAsync<SampleItem, long>(sample, [changed], token);
            await sample.SaveChangesAsync(token);
        }

        Assert.Equal(before, await CopiesAsync(changed, token));
    }

    [Fact]
    public async Task ARetentionRolledBackLeavesTheRowAndItsAudit()
    {
        var token = TestContext.Current.CancellationToken;
        var stamp = Guid.NewGuid().ToString("N");
        var item = await CreateAsync($"e10s-rolled-back-{stamp}", MemberVid, token);
        _items.Add(item);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            await using var transaction = await sample.Database.BeginTransactionAsync(token);

            sample.Items.Remove(await CrudSource.BackOffice<SampleItem>(sample).AsTracking().SingleAsync(row => row.Id == item, token));
            await sample.SaveChangesAsync(token);
            await AuditRedaction.EmptyDeletedAsync<SampleItem, long>(sample, [item], token);
            await sample.SaveChangesAsync(token);

            // Inside the transaction the copies are already empty: the call sees the delete and its own audit row.
            var inside = await sample.Set<AuditLogEntry>().AsNoTracking()
                .Where(row => row.Entity == "smp_items" && row.EntityId == item.ToString(CultureInfo.InvariantCulture))
                .OrderBy(row => row.Id)
                .Select(row => new { row.Action, row.BeforeJson, row.AfterJson })
                .ToListAsync(token);
            Assert.Equal(["created", "deleted"], inside.Select(row => row.Action));
            Assert.All(inside, row => Assert.DoesNotContain(stamp, $"{row.BeforeJson}{row.AfterJson}", StringComparison.Ordinal));

            await transaction.RollbackAsync(token);
        }

        var history = await CopiesAsync(item, token);
        Assert.Equal(["created"], history.Select(row => row.Action));
        Assert.Equal($"e10s-rolled-back-{stamp}", Title(history[0].AfterJson));

        await using var check = _factory.Services.CreateAsyncScope();
        Assert.True(await CrudSource.BackOffice<SampleItem>(check.ServiceProvider.GetRequiredService<SampleDbContext>())
            .AnyAsync(row => row.Id == item, token));
    }

    /// <summary>What the call could not honour, refused rather than emptying nothing without a word.</summary>
    [Fact]
    public async Task TheCallRefusesWhatItCouldNotHonour()
    {
        var token = TestContext.Current.CancellationToken;
        var stamp = Guid.NewGuid().ToString("N");
        var item = await CreateAsync($"e10s-unsaved-{stamp}", MemberVid, token);
        _items.Add(item);

        await using var scope = _factory.Services.CreateAsyncScope();
        var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        // The delete not saved yet: the row would still be there, and nothing would be emptied.
        sample.Items.Remove(await CrudSource.BackOffice<SampleItem>(sample).AsTracking().SingleAsync(row => row.Id == item, token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditRedaction.EmptyDeletedAsync<SampleItem, long>(sample, [item], token));
        sample.ChangeTracker.Clear();

        // An entity with no column marked as a person's data, and keys of another type than the entity's.
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditRedaction.EmptyDeletedAsync<SampleRecord, long>(sample, [item], token));
        await Assert.ThrowsAsync<ArgumentException>(() => AuditRedaction.EmptyDeletedAsync<SampleItem, int>(sample, [1], token));

        Assert.Equal($"e10s-unsaved-{stamp}", Title((await CopiesAsync(item, token))[0].AfterJson));
    }

    private sealed record AuditCopy(string Action, int Vid, string? BeforeJson, string? AfterJson);

    private static JsonObject Json(string? json) => JsonNode.Parse(json!)!.AsObject();

    private static string? Title(string? json) => Json(json)["Title"]?.GetValue<string>();

    private async Task<List<AuditCopy>> CopiesAsync(long id, CancellationToken cancellationToken)
    {
        var key = id.ToString(CultureInfo.InvariantCulture);
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking()
            .Where(row => row.Entity == "smp_items" && row.EntityId == key)
            .OrderBy(row => row.Id)
            .Select(row => new AuditCopy(row.Action, row.Vid, row.BeforeJson, row.AfterJson))
            .ToListAsync(cancellationToken);
    }

    private async Task<long> CreateAsync(string title, int stakeholder, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var item = new SampleItem
        {
            Title = title,
            Visibility = Visibility.Department,
            StakeholderVid = stakeholder,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of([Department.ED]),
        };

        sample.Items.Add(item);
        await sample.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    private async Task UpdateAsync(long id, string title, int stakeholder, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var item = await CrudSource.BackOffice<SampleItem>(sample).AsTracking().SingleAsync(row => row.Id == id, cancellationToken);
        item.Title = title;
        item.StakeholderVid = stakeholder;
        await sample.SaveChangesAsync(cancellationToken);
    }
}
