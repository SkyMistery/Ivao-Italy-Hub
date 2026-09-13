using System.Text.Json;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The grants to positions a division starts with, read from <c>division.json → positionGrants</c>
/// (M2, note 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.2).
/// <para>Applied <b>once</b>: a row in <c>hub_division_settings</c> remembers it, so a grant deleted from
/// the permissions screen stays deleted after a restart and editing the file later changes nothing —
/// the same rule as the super administrators, for the same reason (plan section 4.1). A seed that
/// names a permission this installation does not know, or a global one, is skipped with a warning:
/// the screen would refuse it too.</para>
/// </summary>
public sealed class PositionGrantSeeder(
    HubDbContext database,
    IOptions<DivisionOptions> division,
    PermissionCatalog catalogue,
    IClock clock,
    ILogger<PositionGrantSeeder> logger)
{
    /// <summary>The setting that remembers the seed was applied.</summary>
    public const string AppliedSettingKey = "positionGrants.seeded";

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await database.DivisionSettings.AnyAsync(row => row.Key == AppliedSettingKey, cancellationToken))
        {
            return 0;
        }

        var added = 0;
        foreach (var seed in division.Value.PositionGrants)
        {
            if (!catalogue.IsKnown(seed.Permission) || catalogue.IsGlobal(seed.Permission))
            {
                logger.LogWarning(
                    "division.json: the grant of {Permission} to {Department} {Levels} is not applied: the permission is unknown or global.",
                    seed.Permission,
                    seed.Department,
                    string.Join(", ", seed.Levels));
                continue;
            }

            database.UserGrants.Add(new UserGrant
            {
                PositionDepartment = seed.Department,
                PositionLevels = seed.Levels,
                Kind = GrantKind.Permission,
                Value = seed.Permission,
                Department = seed.Scope,
                Effect = seed.Deny ? GrantEffect.Deny : GrantEffect.Grant,
                Reason = "division.json",
            });
            added++;
        }

        database.DivisionSettings.Add(new DivisionSetting
        {
            Key = AppliedSettingKey,
            ValueJson = JsonSerializer.Serialize(added),
            UpdatedAt = clock.UtcNow,
        });

        await database.SaveChangesAsync(cancellationToken);

        if (added > 0)
        {
            logger.LogInformation("Seeded {Count} grant(s) to positions from division.json.", added);
        }

        return added;
    }
}
