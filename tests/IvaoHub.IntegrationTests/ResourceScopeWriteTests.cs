using System.Globalization;
using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The backbone once more (M4, E10i, note 2026-10-09-il-grant-su-una-riga-scrive-la-sua-riga): a permission granted on one row,
/// asked by the interceptor's guard itself with the row's scope — as the single handler asks it —, with no endpoint in the way, as
/// <see cref="AlternativeWritePermissionTests"/> does, on the test module's tables and a real MariaDB. An item is what an event is,
/// a part what its slot is (<see cref="SamplePart"/>):
/// <list type="bullet">
/// <item><c>Edit</c> granted on one item changes and deletes that item and the parts that answer with its scope, and nothing of
/// another item — a part removed is judged by the item it had, not by one written into it before;</item>
/// <item>it brings a part of its item into existence, and never a part of another item nor a new item;</item>
/// <item>a part moved from one item to another needs it on both, and no alternative moves it;</item>
/// <item>an alternative marked for creation and held on one item creates a part of it, as <c>Edit</c> does;</item>
/// <item>held across the department, with no scope, it does what it always did, and moving a row between departments still needs
/// both sides.</item>
/// </list>
/// <para>Who writes is the identity a login puts in the cookie (<see cref="HubClaims.BuildIdentity"/>), read by the host's own
/// <see cref="HttpContextCurrentUser"/> from the request. Nobody is seeded: the claims are the person, so the one VID of the phase
/// is a different holder in each test.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ResourceScopeWriteTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range of the events module (CONTRIBUTING.md): the one VID left to E10i. No ED or MD staff with an address: nobody is seeded.
    private const int WriterVid = 761097;

    private readonly List<long> _items = [];
    private readonly List<long> _parts = [];
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Nothing left behind: the rows go as the installation, which the guard leaves alone.
        var token = TestContext.Current.CancellationToken;
        if (_parts.Count > 0)
        {
            await AsAsync(who: null, database => database.Parts.Where(row => _parts.Contains(row.Id)).ExecuteDeleteAsync(token));
        }

        if (_items.Count > 0)
        {
            await AsAsync(who: null, database => database.Items.Where(row => _items.Contains(row.Id)).ExecuteDeleteAsync(token));
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task EditGrantedOnOneItemChangesAndDeletesThatItemAndNoOther()
    {
        var token = TestContext.Current.CancellationToken;
        var item = await ItemAsync(who: null, "evt-test-e10i item", token);
        var other = await ItemAsync(who: null, "evt-test-e10i other item", token);

        // Sample.Edit on the item alone, the way a member is granted EventBookings.Edit on one event.
        var onTheItem = Holding(Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(item)));

        await ChangeItemAsync(onTheItem, item, row => row.Title = "evt-test-e10i item, changed", token);
        await RefusedAsync(() => ChangeItemAsync(onTheItem, other, row => row.Title = "evt-test-e10i not theirs", token));
        await RefusedAsync(() => DeleteItemAsync(onTheItem, other, token));

        await DeleteItemAsync(onTheItem, item, token);

        Assert.Null(await FindItemAsync(item, token));
        Assert.Equal("evt-test-e10i other item", (await FindItemAsync(other, token))!.Title);
        Assert.True(await AuditedAsync("smp_items", item, "updated", token));
        Assert.True(await AuditedAsync("smp_items", item, "deleted", token));
    }

    [Fact]
    public async Task ItReachesThePartsThatAnswerWithItsScope()
    {
        var token = TestContext.Current.CancellationToken;
        var item = await ItemAsync(who: null, "evt-test-e10i item", token);
        var other = await ItemAsync(who: null, "evt-test-e10i other item", token);
        var part = await PartAsync(who: null, item, "evt-test-e10i part", token);
        var notTheirs = await PartAsync(who: null, other, "evt-test-e10i part of the other", token);

        var onTheItem = Holding(Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(item)));

        // A slot of the event: changed and taken away with the grant on its event...
        await ChangePartAsync(onTheItem, part, row => row.Title = "evt-test-e10i part, changed", token);
        Assert.Equal("evt-test-e10i part, changed", (await FindPartAsync(part, token))!.Title);
        await DeletePartAsync(onTheItem, part, token);
        Assert.Null(await FindPartAsync(part, token));

        // ...and a slot of another event, neither.
        await RefusedAsync(() => ChangePartAsync(onTheItem, notTheirs, row => row.Title = "evt-test-e10i not theirs", token));
        await RefusedAsync(() => DeletePartAsync(onTheItem, notTheirs, token));
        Assert.Equal("evt-test-e10i part of the other", (await FindPartAsync(notTheirs, token))!.Title);
    }

    /// <summary>
    /// A deletion is asked with the scope the row had, read from the tracker's original values, never with what the instance in hand
    /// says by then (the review of #237, point 1): the guard judges the row the database holds.
    /// </summary>
    [Fact]
    public async Task ARemovedPartIsJudgedByTheItemItHadNotByTheOneWrittenIntoIt()
    {
        var token = TestContext.Current.CancellationToken;
        var item = await ItemAsync(who: null, "evt-test-e10i item", token);
        var other = await ItemAsync(who: null, "evt-test-e10i other item", token);
        var part = await PartAsync(who: null, other, "evt-test-e10i part of the other", token);

        // A part of the other item, loaded, given the held item's id and removed in the same save: the item it had decides.
        var onTheItem = Holding(Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(item)));
        await RefusedAsync(() => RemoveAsAPartOfAsync(onTheItem, part, item, token));
        Assert.Equal(other, (await FindPartAsync(part, token))!.ItemId);

        // The other way round: the grant on the item the part had removes it, whatever is written into it first.
        var onTheOther = Holding(Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(other)));
        await RemoveAsAPartOfAsync(onTheOther, part, item, token);
        Assert.Null(await FindPartAsync(part, token));
    }

    [Fact]
    public async Task ItBringsAPartOfItsItemIntoExistenceAndNothingElse()
    {
        var token = TestContext.Current.CancellationToken;
        var item = await ItemAsync(who: null, "evt-test-e10i item", token);
        var other = await ItemAsync(who: null, "evt-test-e10i other item", token);

        var onTheItem = Holding(Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(item)));

        // A new slot of its event: the new row answers with the event's scope, which the grant names.
        var part = await PartAsync(onTheItem, item, "evt-test-e10i new part", token);
        Assert.True(await AuditedAsync("smp_parts", part, "created", token));

        // Not a slot of another event, and not a new event: that one answers with its own scope, built on a key the database
        // has not given yet, which no grant names.
        await RefusedAsync(() => PartAsync(onTheItem, other, "evt-test-e10i part of the other", token));
        await RefusedAsync(() => ItemAsync(onTheItem, "evt-test-e10i new item", token));

        Assert.Equal([part], await PartsOfAsync(item, token));
        Assert.Empty(await PartsOfAsync(other, token));
    }

    [Fact]
    public async Task APartMovedToAnotherItemNeedsItOnBothAndNoAlternativeMovesIt()
    {
        var token = TestContext.Current.CancellationToken;
        var item = await ItemAsync(who: null, "evt-test-e10i from", token);
        var other = await ItemAsync(who: null, "evt-test-e10i to", token);
        var part = await PartAsync(who: null, item, "evt-test-e10i moving part", token);

        // Held on the item it leaves only, the permission does not give the part to another item...
        var onTheFirst = Holding(Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(item)));
        await RefusedAsync(() => ChangePartAsync(onTheFirst, part, row => row.ItemId = other, token));

        // ...and held on the item it reaches only, it does not take the part from the first: the scope as it was counts too.
        var onTheSecond = Holding(Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(other)));
        await RefusedAsync(() => ChangePartAsync(onTheSecond, part, row => row.ItemId = other, token));

        // Nor does an alternative, held on both: no alternative moves a row, between scopes no more than between departments.
        var decidingOnBoth = Holding(
            Held(SampleModule.DecidePermission, Department.ED, SampleItem.ScopeOf(item)),
            Held(SampleModule.DecidePermission, Department.ED, SampleItem.ScopeOf(other)));
        await RefusedAsync(() => ChangePartAsync(decidingOnBoth, part, row => row.ItemId = other, token));
        Assert.Equal(item, (await FindPartAsync(part, token))!.ItemId);

        // Held on both items, it moves the part.
        var onBoth = Holding(
            Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(item)),
            Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(other)));
        await ChangePartAsync(onBoth, part, row => row.ItemId = other, token);
        Assert.Equal(other, (await FindPartAsync(part, token))!.ItemId);
    }

    [Fact]
    public async Task AnAlternativeMarkedForCreationHeldOnOneItemCreatesAPartOfItAsEditDoes()
    {
        var token = TestContext.Current.CancellationToken;
        var item = await ItemAsync(who: null, "evt-test-e10i item", token);
        var other = await ItemAsync(who: null, "evt-test-e10i other item", token);

        // Sample.Record, marked AlsoOnCreation on a part, held on the item alone: asked with the scope the new row answers with.
        var recordingTheItem = Holding(Held(SampleModule.RecordPermission, Department.ED, SampleItem.ScopeOf(item)));
        var part = await PartAsync(recordingTheItem, item, "evt-test-e10i recorded part", token);
        await RefusedAsync(() => PartAsync(recordingTheItem, other, "evt-test-e10i recorded elsewhere", token));

        // Sample.Decide changes the part of its item and creates none, not being marked for creation.
        var decidingTheItem = Holding(Held(SampleModule.DecidePermission, Department.ED, SampleItem.ScopeOf(item)));
        await ChangePartAsync(decidingTheItem, part, row => row.Title = "evt-test-e10i decided part", token);
        await RefusedAsync(() => PartAsync(decidingTheItem, item, "evt-test-e10i decided part, new", token));

        Assert.Equal([part], await PartsOfAsync(item, token));
        Assert.Equal("evt-test-e10i decided part", (await FindPartAsync(part, token))!.Title);
    }

    [Fact]
    public async Task HeldAcrossTheDepartmentItDoesWhatItAlwaysDid()
    {
        var token = TestContext.Current.CancellationToken;

        // Sample.Edit on the department, with no scope: every item and every part, as it has always reached them.
        var editor = Holding(Held(SampleModule.EditPermission, Department.ED));
        var item = await ItemAsync(editor, "evt-test-e10i by the editor", token);
        var other = await ItemAsync(editor, "evt-test-e10i other by the editor", token);
        var part = await PartAsync(editor, item, "evt-test-e10i part by the editor", token);

        await ChangeItemAsync(editor, item, row => row.Title = "evt-test-e10i by the editor, changed", token);
        await ChangePartAsync(editor, part, row => row.ItemId = other, token);
        Assert.Equal(other, (await FindPartAsync(part, token))!.ItemId);
        await DeletePartAsync(editor, part, token);
        await DeleteItemAsync(editor, item, token);
        Assert.Null(await FindItemAsync(item, token));

        // Moving a row between departments still needs the permission on both sides: on the department it reaches alone, it does
        // not take the row there...
        var elsewhere = Holding(Held(SampleModule.EditPermission, Department.SOD));
        await RefusedAsync(() => ChangeItemAsync(
            elsewhere,
            other,
            row => row.OwnerDepartmentMask = DepartmentMask.Of([Department.SOD, Department.ED]),
            token));

        // ...nor held on the row alone in that department...
        var onTheRowElsewhere = Holding(Held(SampleModule.EditPermission, Department.SOD, SampleItem.ScopeOf(other)));
        await RefusedAsync(() => ChangeItemAsync(
            onTheRowElsewhere,
            other,
            row => row.OwnerDepartmentMask = DepartmentMask.Of([Department.SOD, Department.ED]),
            token));
        Assert.Equal([Department.ED], ((IOwnedByDepartment)(await FindItemAsync(other, token))!).OwnerDepartments);

        // ...and on the row in both departments, it does.
        var onTheRowInBoth = Holding(
            Held(SampleModule.EditPermission, Department.SOD, SampleItem.ScopeOf(other)),
            Held(SampleModule.EditPermission, Department.ED, SampleItem.ScopeOf(other)));
        await ChangeItemAsync(
            onTheRowInBoth,
            other,
            row => row.OwnerDepartmentMask = DepartmentMask.Of([Department.SOD, Department.ED]),
            token);
        Assert.Equal([Department.SOD, Department.ED], ((IOwnedByDepartment)(await FindItemAsync(other, token))!).OwnerDepartments);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    /// <summary>A signed in member holding exactly these permissions, as a login writes them into the cookie.</summary>
    private static ClaimsPrincipal Holding(params EffectivePermission[] permissions) =>
        new(HubClaims.BuildIdentity(
            WriterVid,
            firstName: "Test",
            lastName: "Scope",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: [],
            permissions: permissions));

    /// <summary>A permission held on a department, across it or — with a scope — on one row only, as a grant gives it.</summary>
    private static EffectivePermission Held(string permission, Department department, string? scope = null) =>
        new(permission, department, $"{EffectivePermissionsCalculator.GrantSourcePrefix}test", scope);

    /// <summary>The guard's refusal: every way in tried, and <c>Edit</c> — the permission that always suffices — missing there.</summary>
    private static async Task RefusedAsync(Func<Task> write)
    {
        var refused = await Assert.ThrowsAsync<ForbiddenDomainException>(write);
        Assert.Equal(SampleModule.EditPermission, refused.Permission);
    }

    /// <summary>An item in the module's base department, created by <paramref name="who"/> — or by the installation.</summary>
    private async Task<long> ItemAsync(ClaimsPrincipal? who, string title, CancellationToken cancellationToken)
    {
        var id = await AsAsync(who, async database =>
        {
            var row = new SampleItem
            {
                Title = title,
                Visibility = Visibility.Department,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            };

            database.Items.Add(row);
            await database.SaveChangesAsync(cancellationToken);
            return row.Id;
        });

        _items.Add(id);
        return id;
    }

    /// <summary>A part of an item, in the item's care, created by <paramref name="who"/> — or by the installation.</summary>
    private async Task<long> PartAsync(ClaimsPrincipal? who, long item, string title, CancellationToken cancellationToken)
    {
        var id = await AsAsync(who, async database =>
        {
            var row = new SamplePart
            {
                ItemId = item,
                Title = title,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            };

            database.Parts.Add(row);
            await database.SaveChangesAsync(cancellationToken);
            return row.Id;
        });

        _parts.Add(id);
        return id;
    }

    private Task ChangeItemAsync(ClaimsPrincipal who, long id, Action<SampleItem> change, CancellationToken cancellationToken) =>
        AsAsync(who, async database =>
        {
            change(await database.Items.IgnoreQueryFilters().SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task DeleteItemAsync(ClaimsPrincipal who, long id, CancellationToken cancellationToken) =>
        AsAsync(who, async database =>
        {
            database.Items.Remove(await database.Items.IgnoreQueryFilters().SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task ChangePartAsync(ClaimsPrincipal who, long id, Action<SamplePart> change, CancellationToken cancellationToken) =>
        AsAsync(who, async database =>
        {
            change(await database.Parts.SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task DeletePartAsync(ClaimsPrincipal who, long id, CancellationToken cancellationToken) =>
        AsAsync(who, async database =>
        {
            database.Parts.Remove(await database.Parts.SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    /// <summary>A part loaded, given another item's id in memory, and removed in the same save.</summary>
    private Task RemoveAsAPartOfAsync(ClaimsPrincipal who, long id, long item, CancellationToken cancellationToken) =>
        AsAsync(who, async database =>
        {
            var row = await database.Parts.SingleAsync(row => row.Id == id, cancellationToken);
            row.ItemId = item;
            database.Parts.Remove(row);
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task<SampleItem?> FindItemAsync(long id, CancellationToken cancellationToken) =>
        AsAsync(who: null, database => database.Items.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    private Task<SamplePart?> FindPartAsync(long id, CancellationToken cancellationToken) =>
        AsAsync(who: null, database => database.Parts.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    private Task<List<long>> PartsOfAsync(long item, CancellationToken cancellationToken) =>
        AsAsync(who: null, database => database.Parts.AsNoTracking()
            .Where(row => row.ItemId == item)
            .OrderBy(row => row.Id)
            .Select(row => row.Id)
            .ToListAsync(cancellationToken));

    /// <summary>Whether the audit of the core holds this action on this row, written by the phase's VID.</summary>
    private async Task<bool> AuditedAsync(string table, long id, string action, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var key = id.ToString(CultureInfo.InvariantCulture);

        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(entry => entry.Entity == table && entry.EntityId == key && entry.Action == action && entry.Vid == WriterVid, cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="work"/> in a scope of its own, as <paramref name="who"/> — or as the installation itself when
    /// nobody is given, which the guard leaves alone. The identity goes where the cookie middleware puts it, the request,
    /// and the host's current user reads it from there.
    /// </summary>
    private async Task<T> AsAsync<T>(ClaimsPrincipal? who, Func<SampleDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = who is null ? null : new DefaultHttpContext { User = who, RequestServices = scope.ServiceProvider };

        try
        {
            return await work(scope.ServiceProvider.GetRequiredService<SampleDbContext>());
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }
}
