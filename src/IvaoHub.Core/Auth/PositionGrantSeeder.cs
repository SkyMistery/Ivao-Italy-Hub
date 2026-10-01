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
/// <para>Each one is applied <b>once</b>: a row in <c>hub_division_settings</c> remembers which seeds were
/// applied, so a grant deleted from the permissions screen stays deleted after a restart — the same rule
/// as the super administrators, for the same reason (plan section 4.1). A seed <b>added</b> to the file
/// later is applied at the next start, once: that is how a module arriving in a running installation
/// brings the grants of its base department (decided with Carmine, 16 September 2026, phase T5 of M2).
/// Until then the row held only a count, and a new module's grants never reached an installation that
/// had already started once.</para>
/// <para>A seed that names a permission this installation does not know, or a global one, is skipped
/// with a warning — the screen would refuse it too — and is not remembered, so it applies on the start
/// that finally knows it. The exception is a global permission a grant may confer — who assigns the
/// awards, which a division gives to a position here (M4, E10f) — and only with no <c>scope</c>.</para>
/// </summary>
public sealed class PositionGrantSeeder(
    HubDbContext database,
    IOptions<DivisionOptions> division,
    PermissionCatalog catalogue,
    IClock clock,
    ILogger<PositionGrantSeeder> logger)
{
    /// <summary>The setting that remembers which seeds were applied, as a JSON array of their fingerprints.</summary>
    public const string AppliedSettingKey = "positionGrants.seeded";

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var setting = await database.DivisionSettings
            .FirstOrDefaultAsync(row => row.Key == AppliedSettingKey, cancellationToken);

        var applied = ReadApplied(setting?.ValueJson);
        var added = 0;

        foreach (var seed in division.Value.PositionGrants)
        {
            var fingerprint = Fingerprint(seed);
            if (applied.Contains(fingerprint))
            {
                continue;
            }

            if (!catalogue.IsKnown(seed.Permission) || catalogue.IsClosedToGrants(seed.Permission))
            {
                logger.LogWarning(
                    "division.json: the grant of {Permission} to {Department} {Levels} is not applied: the permission is unknown or global.",
                    seed.Permission,
                    Subject(seed),
                    string.Join(", ", seed.Levels));
                continue;
            }

            // A global permission a grant may confer is held everywhere or not at all (M4, E10f): a scope would look like a
            // limit nothing applies. The screen refuses it too, and it is not remembered either, so the file can be put right.
            if (catalogue.IsGlobal(seed.Permission) && seed.Scope is not null)
            {
                logger.LogWarning(
                    "division.json: the grant of {Permission} to {Department} {Levels} is not applied: a global permission has no scope.",
                    seed.Permission,
                    Subject(seed),
                    string.Join(", ", seed.Levels));
                continue;
            }

            // A grant to the team of a FIR only names a permission of an area whose rows say their FIR (M3, A11a): anywhere
            // else it would reach no row. The screen refuses it too, and it is not remembered either, so it applies on the
            // start that brings such rows.
            if (seed.FirTeam && !catalogue.IsOfAnAreaWithAFir(seed.Permission))
            {
                logger.LogWarning(
                    "division.json: the grant of {Permission} to the team of a FIR {Levels} is not applied: no row of its area says its FIR.",
                    seed.Permission,
                    string.Join(", ", seed.Levels));
                continue;
            }

            database.UserGrants.Add(new UserGrant
            {
                PositionDepartment = seed.FirTeam ? null : seed.Department,
                PositionFirTeam = seed.FirTeam,
                PositionLevels = seed.Levels,
                Kind = GrantKind.Permission,
                Value = seed.Permission,
                Department = seed.Scope,
                Effect = seed.Deny ? GrantEffect.Deny : GrantEffect.Grant,
                Reason = "division.json",
            });
            applied.Add(fingerprint);
            added++;
        }

        if (setting is null)
        {
            setting = new DivisionSetting { Key = AppliedSettingKey };
            database.DivisionSettings.Add(setting);
        }
        else if (added == 0)
        {
            return 0;
        }

        setting.ValueJson = JsonSerializer.Serialize(applied.Order(StringComparer.Ordinal));
        setting.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);

        if (added > 0)
        {
            logger.LogInformation("Seeded {Count} grant(s) to positions from division.json.", added);
        }

        return added;
    }

    /// <summary>
    /// What makes two seeds the same seed: who, which levels, which permission, on which department and
    /// with which effect. Reordering the levels in the file is not a new seed. Who is a department, or
    /// the team of a FIR (M3, A11a), which no department is called.
    /// </summary>
    public static string Fingerprint(PositionGrantSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        return string.Join(
            '|',
            Subject(seed),
            string.Join(',', seed.Levels.Distinct().Order().Select(level => level.ToString())),
            seed.Permission,
            seed.Scope?.ToString() ?? "*",
            seed.Deny ? "deny" : "grant");
    }

    /// <summary>Who a seed is for: its department, or the team of a FIR — a word no department is called.</summary>
    private static string Subject(PositionGrantSeed seed) =>
        seed.FirTeam ? "firTeam" : seed.Department?.ToString() ?? string.Empty;

    /// <summary>
    /// The seeds already applied. A row written before T5 holds a number — the count of what it applied —
    /// and no way of telling which: it is read as none, which re-applies nothing an installation of this
    /// hub had, because no division file had a grant to a position before the tours module.
    /// </summary>
    private static HashSet<string> ReadApplied(string? json)
    {
        if (json is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? new HashSet<string>(
                document.RootElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty),
                StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }
}
