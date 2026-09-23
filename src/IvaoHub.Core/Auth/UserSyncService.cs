using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// What IVAO says about a person at the moment they log in.
/// <para><paramref name="LanguageId"/> is the language they chose on IVAO, used as their first
/// language here. <paramref name="IvaoIsStaff"/> is what IVAO itself calls staff, which is wider
/// than what this division calls staff and is therefore only ever recorded, never acted upon.</para>
/// </summary>
public sealed record IvaoUserProfile(
    int Vid,
    string FirstName,
    string LastName,
    string? PublicNickname,
    string? DivisionCode,
    string? CountryId,
    int? RatingAtc,
    int? RatingPilot,
    string? DiscordId,
    string? Email,
    string? LanguageId,
    bool? IvaoIsStaff,
    bool? IvaoIsSupervisor,
    IReadOnlyList<string> StaffPositions);

/// <summary>The identity the hub hands to the cookie once a login has been processed.</summary>
public sealed record SignedInUser(
    HubUser User,
    IReadOnlyList<StaffPosition> Positions,
    IReadOnlyList<EffectivePermission> Permissions);

/// <summary>
/// Turns an IVAO profile into a row of <c>hub_users</c> and a set of effective permissions.
/// The roster of the hub is exactly the people who have logged in at least once: IVAO exposes no
/// endpoint that lists the staff of a division (plan section 16.13).
/// </summary>
public sealed class UserSyncService(
    HubDbContext database,
    IFirDirectory firs,
    IOptions<DivisionOptions> division,
    PermissionCatalog catalogue,
    IClock clock,
    ILogger<UserSyncService> logger) : IPermissionHolders
{
    public async Task<SignedInUser> UpsertAsync(IvaoUserProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var options = division.Value;
        var firIds = await firs.GetFirIdsAsync(cancellationToken);

        var parsed = profile.StaffPositions
            .Select(raw => (Raw: raw, Position: StaffRoleMap.Parse(raw, options.Code, firIds)))
            .ToArray();

        foreach (var (raw, position) in parsed.Where(entry => entry.Position is null))
        {
            // Never dropped: it stays in hub_user_staff_positions and becomes meaningful as soon as
            // the reference data lands, or tells us the map needs a new row.
            logger.LogInformation("Staff position {Position} of VID {Vid} was not recognised.", raw, profile.Vid);
        }

        var positions = parsed.Select(entry => entry.Position).OfType<StaffPosition>().ToArray();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == profile.Vid, cancellationToken);
        if (user is null)
        {
            // Locale is deliberately left null here: it is decided a few lines below by the one
            // rule that knows how, and a value set now would make that rule unreachable.
            user = new HubUser
            {
                Vid = profile.Vid,
                CreatedAt = clock.UtcNow,
                SecurityStamp = SuperadminService.NewStamp(),
            };
            database.Users.Add(user);
        }

        user.FirstName = profile.FirstName;
        user.LastName = profile.LastName;
        user.PublicNickname = profile.PublicNickname;
        user.DivisionCode = profile.DivisionCode;
        user.Country = profile.CountryId;
        user.RatingAtc = profile.RatingAtc;
        user.RatingPilot = profile.RatingPilot;
        user.DiscordId = profile.DiscordId;

        // Read for the notification service and for nothing else. Kept up to date at every sign
        // in, and left alone when IVAO sends nothing rather than blanked: an address the hub had
        // is better than no address, and a member who wants none clears it by other means.
        user.Email = profile.Email ?? user.Email;
        user.IvaoIsStaff = profile.IvaoIsStaff;
        user.IvaoIsSupervisor = profile.IvaoIsSupervisor;

        // Ours means "holds a position of THIS division", which is what permissions and grants rest
        // on. IVAO's own isStaff is wider and is kept alongside, never merged into this one.
        user.IsStaff = positions.Length > 0;
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        // Only when there is nothing to keep: the language IVAO has for them if the division speaks
        // it, English otherwise. A member who has chosen one here is never overwritten by a login.
        user.Locale ??= LocalePreference.Resolve(profile.LanguageId, options);

        await ReplacePositionsAsync(profile.Vid, parsed, cancellationToken);

        var grants = await database.UserGrants
            .Where(grant => grant.Vid == profile.Vid || grant.PositionDepartment != null)
            .ToListAsync(cancellationToken);

        // A grant only survives while the person is staff: losing every position suspends them,
        // it never deletes them, so they come back if the person returns (plan section 6.3). A grant
        // to a position has nobody to suspend: it stops counting when nobody holds the position.
        foreach (var grant in grants.Where(grant => grant.Vid == profile.Vid))
        {
            var shouldBeSuspended = !user.IsStaff;
            if (shouldBeSuspended && grant.SuspendedAt is null)
            {
                grant.SuspendedAt = clock.UtcNow;
                grant.UpdatedAt = clock.UtcNow;
            }
            else if (!shouldBeSuspended && grant.SuspendedAt is not null)
            {
                grant.SuspendedAt = null;
                grant.UpdatedAt = clock.UtcNow;
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        var permissions = EffectivePermissionsCalculator.Calculate(
            positions,
            grants,
            user.IsSuperadmin,
            clock.UtcNow,
            catalogue);

        return new SignedInUser(user, positions, permissions);
    }

    /// <summary>
    /// Recomputes the effective permissions of a user who is already known, without a login: used
    /// after a grant changes and by the tests.
    /// </summary>
    public async Task<SignedInUser?> LoadAsync(int vid, CancellationToken cancellationToken = default)
    {
        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var options = division.Value;
        var firIds = await firs.GetFirIdsAsync(cancellationToken);

        var positions = await database.UserStaffPositions
            .Where(position => position.Vid == vid)
            .Select(position => position.Position)
            .ToListAsync(cancellationToken);

        var parsed = positions
            .Select(raw => StaffRoleMap.Parse(raw, options.Code, firIds))
            .OfType<StaffPosition>()
            .ToArray();

        var grants = await database.UserGrants
            .Where(grant => grant.Vid == vid || grant.PositionDepartment != null)
            .ToListAsync(cancellationToken);

        return new SignedInUser(
            user,
            parsed,
            EffectivePermissionsCalculator.Calculate(parsed, grants, user.IsSuperadmin, clock.UtcNow, catalogue));
    }

    /// <summary>
    /// Everybody who holds a permission, with their effective permissions to ask where: a notification that goes to
    /// "whoever may validate", each with their own tours (M2, T13, note 2026-09-23-la-validazione §3.3).
    /// <para>The answer is the calculator's, run on every candidate exactly as a login runs it — the staff, whoever a
    /// grant names, the super administrators — so it cannot drift from what the same people are allowed on the next
    /// request. Nobody else can hold anything: a member with no position and no grant holds no permission.</para>
    /// </summary>
    public async Task<IReadOnlyList<PermissionHolder>> HoldersOfAsync(string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var options = division.Value;
        var firIds = await firs.GetFirIdsAsync(cancellationToken);

        var positions = (await database.UserStaffPositions.AsNoTracking()
                .Select(position => new { position.Vid, position.Position })
                .ToListAsync(cancellationToken))
            .GroupBy(position => position.Vid)
            .ToDictionary(
                group => group.Key,
                group => group.Select(position => StaffRoleMap.Parse(position.Position, options.Code, firIds)).OfType<StaffPosition>().ToArray());

        var grants = await database.UserGrants.AsNoTracking().ToListAsync(cancellationToken);
        var named = grants.Where(grant => grant.Vid is not null).Select(grant => grant.Vid!.Value);

        var candidates = positions.Keys.Concat(named).ToHashSet();
        var users = await database.Users.AsNoTracking()
            .Where(user => candidates.Contains(user.Vid) || user.IsSuperadmin)
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        var holders = new List<PermissionHolder>();
        foreach (var user in users)
        {
            var held = positions.GetValueOrDefault(user.Vid) ?? [];
            var own = grants.Where(grant => grant.Vid == user.Vid || grant.PositionDepartment != null);
            var permissions = EffectivePermissionsCalculator.Calculate(held, own, user.IsSuperadmin, now, catalogue);
            if (PermissionSet.HasAny(permissions, user.IsSuperadmin, permission))
            {
                holders.Add(new PermissionHolder(user.Vid, user.IsSuperadmin, permissions));
            }
        }

        return holders;
    }

    private async Task ReplacePositionsAsync(
        int vid,
        IReadOnlyList<(string Raw, StaffPosition? Position)> parsed,
        CancellationToken cancellationToken)
    {
        var existing = await database.UserStaffPositions
            .Where(position => position.Vid == vid)
            .ToListAsync(cancellationToken);

        var incoming = parsed
            .Select(entry => entry.Position?.Raw ?? entry.Raw.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        database.UserStaffPositions.RemoveRange(
            existing.Where(position => !incoming.Contains(position.Position, StringComparer.OrdinalIgnoreCase)));

        foreach (var (raw, position) in parsed)
        {
            var code = position?.Raw ?? raw.Trim().ToUpperInvariant();
            var row = existing.FirstOrDefault(item => string.Equals(item.Position, code, StringComparison.OrdinalIgnoreCase));

            if (row is null)
            {
                row = new UserStaffPosition { Vid = vid, Position = code };
                database.UserStaffPositions.Add(row);
            }

            row.Department = position?.Department;
            row.Level = position?.Level;
            row.Fir = position?.Fir;
            row.SyncedAt = clock.UtcNow;
        }
    }
}
