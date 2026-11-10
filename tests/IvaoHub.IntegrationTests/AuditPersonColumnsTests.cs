using System.Globalization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A member's address, Discord and session stamp were copied into the audit of <c>hub_users</c>, served to every holder of
/// <c>Audit.View</c>, and every sign in left a row for its <c>LastLoginAt</c> (issue #326, item 2): columns marked
/// <c>[NotAudited]</c> are never copied, and a save that moves only them is no change.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AuditPersonColumnsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int MemberVid = 931004;
    private const string Address = "audit-test-931004@example.org";

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
    public async Task TheAuditOfAMemberCopiesNeitherTheirAddressNorTheirStampAndASignInAloneLeavesNoRow()
    {
        var token = TestContext.Current.CancellationToken;
        var key = MemberVid.ToString(CultureInfo.InvariantCulture);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = new HubUser
        {
            Vid = MemberVid,
            FirstName = "Audit",
            LastName = "Test",
            Email = Address,
            DiscordId = "discord-931004",
            DiscordUsername = "discord-name-931004",
            DiscordLinkedAt = clock.UtcNow,
            SecurityStamp = SuperadminService.NewStamp(),
            LastLoginAt = clock.UtcNow,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        };
        database.Users.Add(user);
        await database.SaveChangesAsync(token);

        // A change of the row with the address and the stamp moving too: the name is in the audit, they are not.
        user.FirstName = "Audited";
        user.Email = $"new-{Address}";
        user.SecurityStamp = SuperadminService.NewStamp();
        await database.SaveChangesAsync(token);

        var rows = await RowsAsync(database, key, token);
        Assert.Equal(["created", "updated"], rows.Select(row => row.Action));
        foreach (var json in rows.SelectMany(row => new[] { row.BeforeJson, row.AfterJson }).OfType<string>())
        {
            Assert.DoesNotContain(nameof(HubUser.Email), json, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(HubUser.DiscordId), json, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(HubUser.DiscordUsername), json, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(HubUser.DiscordLinkedAt), json, StringComparison.Ordinal);
            Assert.DoesNotContain("discord-name", json, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(HubUser.SecurityStamp), json, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(HubUser.LastLoginAt), json, StringComparison.Ordinal);
            Assert.DoesNotContain("example.org", json, StringComparison.Ordinal);
        }

        Assert.Contains("Audited", rows[^1].AfterJson, StringComparison.Ordinal);

        // A sign in that changes nothing else: no row.
        user.LastLoginAt = clock.UtcNow.AddMinutes(5);
        await database.SaveChangesAsync(token);
        Assert.Equal(2, (await RowsAsync(database, key, token)).Count);
    }

    private static Task<List<AuditLogEntry>> RowsAsync(HubDbContext database, string key, CancellationToken cancellationToken) =>
        database.AuditLog.AsNoTracking()
            .Where(row => row.Entity == "hub_users" && row.EntityId == key)
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);

    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var key = MemberVid.ToString(CultureInfo.InvariantCulture);
        await database.AuditLog.Where(row => row.Entity == "hub_users" && row.EntityId == key).ExecuteDeleteAsync(cancellationToken);
        await database.Users.Where(row => row.Vid == MemberVid).ExecuteDeleteAsync(cancellationToken);
    }
}
