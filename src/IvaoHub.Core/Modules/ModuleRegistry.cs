using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Modules;

/// <summary>
/// Every module this installation was built with, and what each of them contributes.
/// <para>The list is <b>explicit</b>: <c>IvaoHub.Web/Modules.cs</c> names them, one line each, and
/// nothing scans "whatever Web happens to reference". A scan reads the same as a list on the day it
/// is written and differently on the day a transitive reference appears (design M0 section 6.5).</para>
/// <para>An optional module a division switched off in <c>division.modules</c> is not in
/// <see cref="Enabled"/> and contributes nothing: no endpoints, no menu, no blocks. The department
/// modules and the editorial core are not optional and cannot be switched off.</para>
/// </summary>
public sealed class ModuleRegistry
{
    /// <summary>
    /// How long a maintenance flag is believed. Short, because it is asked on every write and
    /// flipped by hand at the worst possible moment; five seconds is what the design fixed.
    /// </summary>
    private static readonly TimeSpan MaintenanceLifetime = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly IMemoryCache _cache;

    public ModuleRegistry(
        IEnumerable<IModule> modules,
        IOptions<DivisionOptions> division,
        IServiceScopeFactory scopes,
        IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(division);

        _scopes = scopes;
        _cache = cache;

        All = [.. modules];

        var duplicated = All.GroupBy(module => module.Key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicated is not null)
        {
            throw new InvalidOperationException($"Two modules answer to the key '{duplicated.Key}'.");
        }

        var settings = division.Value.Modules;

        // Only an optional module can be switched off, and only by being named with false: a key
        // that is simply absent leaves the module on, so that a release which adds one does not
        // need every division to edit its configuration before the module works.
        Enabled =
        [
            .. All.Where(module => !module.IsOptional
                || !settings.TryGetValue(module.Key, out var enabled)
                || enabled),
        ];

        Keys = [.. All.Select(module => module.Key)];
        EnabledKeys = [.. Enabled.Select(module => module.Key)];

        PublicNavigation = [.. Enabled.SelectMany(module => module.PublicNavigation)];
        StaffNavigation = [.. Enabled.SelectMany(module => module.StaffNavigation)];
        SpaFallbackExclusions =
        [
            .. Enabled.SelectMany(module => module.SpaFallbackExclusions)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>How a maintenance flag is keyed in <c>hub_division_settings</c>.</summary>
    public static string MaintenanceKey(string moduleKey) => $"modules.{moduleKey}.maintenance";

    /// <summary>Every module that was compiled in, the ones switched off included.</summary>
    public IReadOnlyList<IModule> All { get; }

    /// <summary>The ones this division actually runs.</summary>
    public IReadOnlyList<IModule> Enabled { get; }

    /// <summary>The keys of <see cref="All"/>: what <c>division.modules</c> may legitimately name.</summary>
    public IReadOnlyCollection<string> Keys { get; }

    public IReadOnlyCollection<string> EnabledKeys { get; }

    /// <summary>The public menu entries of the enabled modules, in the order the list declares them.</summary>
    public IReadOnlyList<NavItemDescriptor> PublicNavigation { get; }

    public IReadOnlyList<NavItemDescriptor> StaffNavigation { get; }

    /// <summary>What the single page application must not answer for, on top of the core's own.</summary>
    public IReadOnlyList<string> SpaFallbackExclusions { get; }

    public IModule? Find(string? key) => key is null
        ? null
        : Enabled.FirstOrDefault(module => string.Equals(module.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// The module a request belongs to, read from its path: <c>/api/atc/anything</c> is the atc
    /// module. Null for everything else, which is most requests.
    /// </summary>
    public IModule? ForApiPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // "/api/", then a key, then either the end of the path or a slash. Written out instead of
        // matched with a regular expression because this runs on every single request.
        const string prefix = "/api/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = path.AsSpan(prefix.Length);
        var slash = rest.IndexOf('/');
        var key = slash < 0 ? rest : rest[..slash];

        foreach (var module in Enabled)
        {
            if (key.Equals(module.Key, StringComparison.OrdinalIgnoreCase))
            {
                return module;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the module is closed for maintenance. Cached for a few seconds: it is asked on every
    /// write, and a round trip to the database per write to read one boolean buys nothing.
    /// </summary>
    public async ValueTask<bool> IsInMaintenanceAsync(string moduleKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);

        var cacheKey = MaintenanceKey(moduleKey);
        if (_cache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        await using var scope = _scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var stored = await database.DivisionSettings
            .AsNoTracking()
            .Where(setting => setting.Key == cacheKey)
            .Select(setting => setting.ValueJson)
            .FirstOrDefaultAsync(cancellationToken);

        var value = stored is not null && ReadBoolean(stored);
        _cache.Set(cacheKey, value, MaintenanceLifetime);
        return value;
    }

    /// <summary>
    /// Opens or closes a module. The audit row is written by the interceptor, because
    /// <c>DivisionSetting</c> is marked <c>[Audited]</c>: a service writing one by hand would be a
    /// second thing that knows the shape of an audit row.
    /// </summary>
    public async Task SetMaintenanceAsync(
        string moduleKey,
        bool inMaintenance,
        HubDbContext database,
        IClock clock,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        var key = MaintenanceKey(moduleKey);
        var setting = await database.DivisionSettings.FirstOrDefaultAsync(row => row.Key == key, cancellationToken);

        if (setting is null)
        {
            setting = new DivisionSetting { Key = key };
            database.DivisionSettings.Add(setting);
        }

        setting.ValueJson = inMaintenance ? "true" : "false";
        setting.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);

        // Not "wait for the cache to expire": whoever flipped the switch is about to check that it
        // worked, and five seconds of the previous answer looks exactly like a broken button.
        _cache.Set(key, inMaintenance, MaintenanceLifetime);
    }

    private static bool ReadBoolean(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<bool?>(json) ?? false;
        }
        catch (JsonException)
        {
            // A setting someone hand edited into something that is not a boolean means "no":
            // refusing to serve the site because a flag is malformed would be the worse answer.
            return false;
        }
    }
}
