using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// Who maintains the system. A super administrator bypasses every policy, which is exactly why the
/// truth is the <c>is_superadmin</c> column and not a file: whoever has FTP access already controls
/// everything, so the goal is that changing the file achieves nothing and that every change is
/// visible and attributable (plan section 6.3).
/// </summary>
public sealed class SuperadminService(
    HubDbContext database,
    IOptions<DivisionOptions> division,
    ISecurityStampCache stamps,
    IClock clock,
    ILogger<SuperadminService> logger)
{
    /// <summary>Key under which the hash of the effective set is remembered between starts.</summary>
    public const string HashSettingKey = "superadmins.hash";

    private const string AuditEntity = "hub_users";

    /// <summary>
    /// Reads <c>division.json</c> only when the database holds no super administrator at all: the
    /// first start, or the recovery path after the last one was removed. Any other time the file is
    /// ignored, so editing it on the server changes nothing.
    /// </summary>
    public async Task<int> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var existing = await database.Users.CountAsync(user => user.IsSuperadmin, cancellationToken);

        if (existing == 0)
        {
            foreach (var vid in division.Value.SuperAdmins.Distinct())
            {
                var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
                if (user is null)
                {
                    // A placeholder the first login will complete: the hub has no way of knowing a
                    // name before the person shows up (plan section 16.13).
                    user = new HubUser
                    {
                        Vid = vid,
                        FirstName = string.Empty,
                        LastName = string.Empty,
                        SecurityStamp = NewStamp(),
                        CreatedAt = clock.UtcNow,
                        UpdatedAt = clock.UtcNow,
                    };
                    database.Users.Add(user);
                }

                user.IsSuperadmin = true;
                user.SecurityStamp = NewStamp();
                user.UpdatedAt = clock.UtcNow;
                stamps.Invalidate(vid);
            }

            await database.SaveChangesAsync(cancellationToken);
            existing = await database.Users.CountAsync(user => user.IsSuperadmin, cancellationToken);
            logger.LogInformation("Bootstrapped {Count} super administrator(s) from division.json.", existing);
        }

        await ReportChangesAsync(cancellationToken);
        return existing;
    }

    public Task<List<int>> ListAsync(CancellationToken cancellationToken = default) =>
        database.Users
            .Where(user => user.IsSuperadmin)
            .Select(user => user.Vid)
            .OrderBy(vid => vid)
            .ToListAsync(cancellationToken);

    /// <summary>Adds one. The caller must already be a super administrator; the endpoint enforces it.</summary>
    public async Task AddAsync(int vid, int byVid, CancellationToken cancellationToken = default)
    {
        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken)
            ?? throw new InvalidOperationException(
                $"VID {vid} has never logged in, so the hub knows nothing about them. "
                + "The roster of the hub is exactly the people who have logged in at least once.");

        if (user.IsSuperadmin)
        {
            return;
        }

        user.IsSuperadmin = true;
        user.SecurityStamp = NewStamp();
        user.UpdatedAt = clock.UtcNow;
        stamps.Invalidate(vid);

        await WriteAuditAsync("superadmin.added", vid, byVid, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await ReportChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Removes one, unless it is the last: a division without a super administrator can only be
    /// recovered by editing <c>division.json</c> and restarting, which is not something to walk into
    /// by accident.
    /// </summary>
    public async Task RemoveAsync(int vid, int byVid, CancellationToken cancellationToken = default)
    {
        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid && row.IsSuperadmin, cancellationToken);
        if (user is null)
        {
            return;
        }

        var count = await database.Users.CountAsync(row => row.IsSuperadmin, cancellationToken);
        if (count <= 1)
        {
            throw new InvalidOperationException(
                "The last super administrator cannot be removed: the division would be left with nobody "
                + "able to grant that role back.");
        }

        user.IsSuperadmin = false;
        user.SecurityStamp = NewStamp();
        user.UpdatedAt = clock.UtcNow;
        stamps.Invalidate(vid);

        await WriteAuditAsync("superadmin.removed", vid, byVid, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await ReportChangesAsync(cancellationToken);
    }

    /// <summary>A fresh stamp makes every cookie of that user stale on the next request.</summary>
    public static string NewStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// Compares the effective set with the last known one and leaves an audit row when it moved.
    /// The email to every super administrator arrives in M1, with the notification service.
    /// </summary>
    private async Task ReportChangesAsync(CancellationToken cancellationToken)
    {
        var current = await ListAsync(cancellationToken);
        var hash = Hash(current);

        var setting = await database.DivisionSettings
            .FirstOrDefaultAsync(row => row.Key == HashSettingKey, cancellationToken);

        if (setting is null)
        {
            database.DivisionSettings.Add(new DivisionSetting
            {
                Key = HashSettingKey,
                ValueJson = JsonSerializer.Serialize(hash),
                UpdatedAt = clock.UtcNow,
            });
        }
        else if (setting.ValueJson != JsonSerializer.Serialize(hash))
        {
            setting.ValueJson = JsonSerializer.Serialize(hash);
            setting.UpdatedAt = clock.UtcNow;

            database.AuditLog.Add(new AuditLogEntry
            {
                Vid = 0,
                Action = "superadmin.set_changed",
                Entity = AuditEntity,
                EntityId = "*",
                AfterJson = JsonSerializer.Serialize(current),
                IsSuperadmin = true,
                At = clock.UtcNow,
            });

            logger.LogWarning("The set of super administrators changed: {Vids}.", string.Join(", ", current));
        }
        else
        {
            return;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    // In F2 the service writes its own audit row; F4 replaces this with the save changes
    // interceptor and the [Audited] attribute, and this method goes away.
    private async Task WriteAuditAsync(string action, int vid, int byVid, CancellationToken cancellationToken)
    {
        database.AuditLog.Add(new AuditLogEntry
        {
            Vid = byVid,
            Action = action,
            Entity = AuditEntity,
            EntityId = vid.ToString(CultureInfo.InvariantCulture),
            IsSuperadmin = true,
            At = clock.UtcNow,
        });

        await Task.CompletedTask;
    }

    private static string Hash(IEnumerable<int> vids)
    {
        var joined = string.Join(',', vids.Order());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined)));
    }
}
