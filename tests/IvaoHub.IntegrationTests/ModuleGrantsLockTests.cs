using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A module's grant to one person is looked for and written under a lock (issue #319, item 8): two «add a validator» at once
/// each found none and wrote one, and «remove» took one of the two away.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ModuleGrantsLockTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int StaffVid = 931002;
    private const string Scope = "core-test-319-8";

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        await CleanAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(TestContext.Current.CancellationToken);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AGrantWaitsForTheOneBeingWrittenAndARemovalTakesEveryCopy()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var grants = scope.ServiceProvider.GetRequiredService<ModuleGrants>();
        await SeedStaffAsync(database, scope.ServiceProvider.GetRequiredService<IClock>(), token);

        await using (var other = await DatabaseLock.TakeAsync(
            database.Database.GetConnectionString(),
            name => DatabaseLock.NameFor(ModuleGrants.LockPrefix, name),
            TimeSpan.Zero,
            token))
        {
            Assert.True(other.Held);
            Assert.Equal("errors.grant.busy", await grants.GiveAsync(StaffVid, CorePermissions.LinksEdit, Department.FOD, Scope, Scope, token));
            Assert.Equal(0, await CountAsync(database, token));
        }

        // Given back: written once, and once only however often it is asked.
        Assert.Null(await grants.GiveAsync(StaffVid, CorePermissions.LinksEdit, Department.FOD, Scope, Scope, token));
        Assert.Null(await grants.GiveAsync(StaffVid, CorePermissions.LinksEdit, Department.FOD, Scope, Scope, token));
        Assert.Equal(1, await CountAsync(database, token));

        // A copy written before the lock existed goes with the removal too.
        database.UserGrants.Add(new UserGrant
        {
            Vid = StaffVid,
            Kind = GrantKind.Permission,
            Value = CorePermissions.LinksEdit,
            Department = Department.FOD,
            ResourceScope = Scope,
            Effect = GrantEffect.Grant,
            Reason = Scope,
        });
        await database.SaveChangesAsync(token);
        Assert.Equal(2, await CountAsync(database, token));

        Assert.True(await grants.TakeAsync(StaffVid, CorePermissions.LinksEdit, Department.FOD, Scope, Scope, token));
        Assert.Equal(0, await CountAsync(database, token));
    }

    [Fact]
    public async Task OnEveryRowOfTheDepartmentTheModuleTouchesOnlyItsOwnLiveCopies()
    {
        // The scope NULL — the grant on every row of a department, a validator's Tours.ViewPilots — is the one a unique index
        // could not guard, and the reason the lock exists. And the module's rows are told from the staff's by their reason.
        var token = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var grants = scope.ServiceProvider.GetRequiredService<ModuleGrants>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        await SeedStaffAsync(database, clock, token);

        Assert.Null(await grants.GiveAsync(StaffVid, CorePermissions.LinksEdit, Department.FOD, null, Scope, token));
        Assert.Null(await grants.GiveAsync(StaffVid, CorePermissions.LinksEdit, Department.FOD, null, Scope, token));
        Assert.Equal(1, await CountAsync(database, token, scoped: false));

        // The same grant written by hand on the permissions screen: the staff's, untouched by the module's removal.
        database.UserGrants.Add(new UserGrant
        {
            Vid = StaffVid,
            Kind = GrantKind.Permission,
            Value = CorePermissions.LinksEdit,
            Department = Department.FOD,
            Effect = GrantEffect.Grant,
            Reason = $"{Scope} by hand",
        });
        await database.SaveChangesAsync(token);

        Assert.True(await grants.TakeAsync(StaffVid, CorePermissions.LinksEdit, Department.FOD, null, Scope, token));
        Assert.Equal($"{Scope} by hand", Assert.Single(await database.UserGrants.AsNoTracking()
            .Where(grant => grant.Vid == StaffVid && grant.ResourceScope == null).Select(grant => grant.Reason).ToListAsync(token)));
        Assert.False(await grants.TakeAsync(StaffVid, CorePermissions.LinksEdit, Department.FOD, null, Scope, token));

        // A copy of the module's that expired holds nothing: asked again, the module writes one that counts.
        database.UserGrants.Add(new UserGrant
        {
            Vid = StaffVid,
            Kind = GrantKind.Permission,
            Value = CorePermissions.LinksEdit,
            Department = Department.FOD,
            Effect = GrantEffect.Grant,
            Reason = Scope,
            ExpiresAt = clock.UtcNow.AddDays(-1),
        });
        await database.SaveChangesAsync(token);

        Assert.Null(await grants.GiveAsync(StaffVid, CorePermissions.LinksEdit, Department.FOD, null, Scope, token));
        Assert.Equal(1, await database.UserGrants.AsNoTracking().CountAsync(
            grant => grant.Vid == StaffVid && grant.ResourceScope == null && grant.Reason == Scope && grant.ExpiresAt == null,
            token));
    }

    private static async Task SeedStaffAsync(HubDbContext database, IClock clock, CancellationToken cancellationToken)
    {
        database.Users.Add(new HubUser
        {
            Vid = StaffVid,
            FirstName = "Test",
            LastName = "Validator",
            IsStaff = true,
            SecurityStamp = SuperadminService.NewStamp(),
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    private static Task<int> CountAsync(HubDbContext database, CancellationToken cancellationToken, bool scoped = true) =>
        database.UserGrants.AsNoTracking().CountAsync(
            grant => grant.Vid == StaffVid && (scoped ? grant.ResourceScope == Scope : grant.ResourceScope == null),
            cancellationToken);

    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await hub.UserGrants.Where(grant => grant.Vid == StaffVid).ExecuteDeleteAsync(cancellationToken);
        await hub.Users.Where(user => user.Vid == StaffVid).ExecuteDeleteAsync(cancellationToken);
    }
}
