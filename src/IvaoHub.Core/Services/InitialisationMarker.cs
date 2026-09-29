using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Services;

/// <summary>
/// The mark of the last start that initialised the database completely, so that a start which finds nothing changed
/// since then does not do it again (note 2026-09-28-il-marcatore-d-inizializzazione). Passenger stops an idle hub after
/// half a minute and the next visitor waits for the whole start: on the server the steps this skips cost about a second.
/// </summary>
/// <remarks>
/// <para>It lives in the database, as one row of <c>hub_division_settings</c>, and not in a file: a database restored from
/// an old copy brings its own old mark, or none, and never a file's word that a schema it has not seen is ready.</para>
/// <para>It is written <b>last</b>, after every step of the initialisation has succeeded: a start that fails half way leaves
/// the mark it found, which names another key, and the next start does everything again. Every step is already applied
/// once and only once, so doing it again is only time. Two processes that start together without a valid mark both
/// initialise, as every start did before the mark, and both write it.</para>
/// <para>Anything that fails while reading it means a full initialisation, never a failed start: on the very first start
/// the table does not exist yet.</para>
/// </remarks>
public sealed class InitialisationMarker(HubDbContext database, IClock clock, ILogger<InitialisationMarker> logger)
{
    /// <summary>The row of <c>hub_division_settings</c> that holds the mark.</summary>
    public const string SettingKey = "startup.initialised";

    /// <summary>
    /// Runs <paramref name="initialise"/> unless the mark says that a start with this very <paramref name="key"/> has
    /// already done it, then writes the mark; says which of the two happened and why.
    /// </summary>
    public async Task<InitialisationOutcome> RunAsync(
        InitialisationKey key,
        Func<CancellationToken, Task> initialise,
        StartupTimings? timings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(initialise);

        var stored = await ReadAsync(cancellationToken);
        timings?.Step("marker");

        var changes = key.ChangesSince(stored);
        if (changes.Count == 0)
        {
            return new InitialisationOutcome(Skipped: true, changes, stored);
        }

        logger.LogInformation("A full initialisation: {Changes}.", string.Join("; ", changes));
        await initialise(cancellationToken);

        await WriteAsync(key, cancellationToken);
        timings?.Step("marker written");

        return new InitialisationOutcome(Skipped: false, changes, stored);
    }

    /// <summary>The mark as the database holds it; null when there is none, or it cannot be read.</summary>
    public async Task<StoredInitialisation?> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await database.DivisionSettings
                .AsNoTracking()
                .Where(setting => setting.Key == SettingKey)
                .Select(setting => setting.ValueJson)
                .FirstOrDefaultAsync(cancellationToken);

            return json is null ? null : JsonSerializer.Deserialize<StoredInitialisation>(json);
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or JsonException)
        {
            // The first start of an installation has no table yet. Whatever else it is, a full initialisation answers it.
            logger.LogInformation("The initialisation marker could not be read ({Reason}): a full initialisation.", exception.Message);
            return null;
        }
    }

    private async Task WriteAsync(InitialisationKey key, CancellationToken cancellationToken)
    {
        var value = JsonSerializer.Serialize(new StoredInitialisation(
            key.Build, key.Configuration, key.Seed, key.Stamp, clock.UtcNow));

        for (var attempt = 1; ; attempt++)
        {
            var row = await database.DivisionSettings.FirstOrDefaultAsync(setting => setting.Key == SettingKey, cancellationToken);
            if (row is null)
            {
                row = new DivisionSetting { Key = SettingKey };
                database.DivisionSettings.Add(row);
            }

            row.ValueJson = value;
            row.UpdatedAt = clock.UtcNow;

            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt == 1)
            {
                // Another process initialised at the same moment and wrote the row between our read and our insert. Its
                // mark is as true as ours; ours is written over it, as an update this time.
                database.ChangeTracker.Clear();
            }
        }
    }
}

/// <summary>
/// What a start is: the code it runs, the configuration it runs with, and the seed files it would apply. Every input of
/// the steps the mark lets a start skip is in one of the three (note 2026-09-28-il-marcatore-d-inizializzazione, §3).
/// </summary>
/// <param name="Build">The module version ids of the hub's own assemblies: a deterministic build gives a new one exactly
/// when the code changed, migrations included, whatever the version number says.</param>
/// <param name="Configuration">The effective division options, the enabled modules and the environment.</param>
/// <param name="Seed">The files of <c>seed/</c>, by name and content.</param>
/// <param name="Stamp">The build as a person reads it, <c>0.2.5+4d424f9</c>: in the mark, for <c>starts.txt</c>.</param>
public sealed record InitialisationKey(string Build, string Configuration, string Seed, string Stamp)
{
    public static InitialisationKey Compute(
        BuildInfo build,
        IEnumerable<Assembly> code,
        DivisionOptions division,
        IEnumerable<string> enabledModules,
        string environment,
        string seedDirectory)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(division);
        ArgumentNullException.ThrowIfNull(enabledModules);

        var assemblies = code
            .Distinct()
            .Select(assembly => $"{assembly.GetName().Name} {assembly.ManifestModule.ModuleVersionId:N}")
            .Order(StringComparer.Ordinal);

        // The options as they are bound, not the file: the installation's own settings can add to division.json.
        var configuration = string.Join(
            '\n',
            JsonSerializer.Serialize(division),
            string.Join(',', enabledModules.Order(StringComparer.Ordinal)),
            environment);

        return new InitialisationKey(
            Hash(string.Join('\n', [build.Stamp, build.Commit, .. assemblies])),
            Hash(configuration),
            HashOfFolder(seedDirectory),
            build.Stamp);
    }

    /// <summary>What differs from the mark, in words; empty when nothing does and the initialisation can be skipped.</summary>
    public IReadOnlyList<string> ChangesSince(StoredInitialisation? stored)
    {
        if (stored is null)
        {
            return ["no marker"];
        }

        List<string> changes = [];
        if (!string.Equals(stored.Build, Build, StringComparison.Ordinal))
        {
            changes.Add($"another build (the marker is of {stored.Stamp})");
        }

        if (!string.Equals(stored.Configuration, Configuration, StringComparison.Ordinal))
        {
            changes.Add("the configuration changed");
        }

        if (!string.Equals(stored.Seed, Seed, StringComparison.Ordinal))
        {
            changes.Add("seed/ changed");
        }

        return changes;
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Every file under the folder, by its path from there and its content; the same value on every system.</summary>
    private static string HashOfFolder(string directory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (Directory.Exists(directory))
        {
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(file => (Path: file, Name: Path.GetRelativePath(directory, file).Replace('\\', '/')))
                .OrderBy(file => file.Name, StringComparer.Ordinal);

            foreach (var (path, name) in files)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(name + "\n"));
                hash.AppendData(SHA256.HashData(File.ReadAllBytes(path)));
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

/// <summary>The mark as <c>hub_division_settings</c> holds it: the key of the start that wrote it, and when.</summary>
public sealed record StoredInitialisation(string Build, string Configuration, string Seed, string Stamp, DateTime At);

/// <summary>What a start did about the initialisation.</summary>
/// <param name="Skipped">True when the mark matched and the steps were not run.</param>
/// <param name="Changes">Why a full initialisation ran; empty when skipped.</param>
/// <param name="Marker">The mark the start found.</param>
public sealed record InitialisationOutcome(bool Skipped, IReadOnlyList<string> Changes, StoredInitialisation? Marker)
{
    /// <summary>The words of <c>starts.txt</c>: <c>initialisation skipped (…): migrations, content</c> or <c>initialisation full: …</c>.</summary>
    public string Describe(IEnumerable<string> skippableSteps) => Skipped
        ? string.Create(
            CultureInfo.InvariantCulture,
            $"initialisation skipped (marker of {Marker!.Stamp}, {Marker.At:yyyy-MM-dd HH:mm:ss'Z'}): {string.Join(", ", skippableSteps)}")
        : $"initialisation full: {string.Join("; ", Changes)}";
}
