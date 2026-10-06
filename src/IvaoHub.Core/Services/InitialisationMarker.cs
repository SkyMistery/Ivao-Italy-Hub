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
using MySqlConnector;

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
/// once and only once, so doing it again is only time.</para>
/// <para>"Once and only once" holds for one process after another, not for two at the same moment: so a start that has
/// to initialise takes <see cref="InitialisationLock"/> first, and reads the mark again with the lock in its hands. Of two
/// processes that start together without a valid mark, one initialises and writes the mark; the other waits, finds that
/// mark and skips everything, as any later start would (note 2026-10-05-l-inizializzazione-sotto-blocco). A start that
/// cannot take the lock initialises without it, as every start did before: then two may still initialise together, and
/// both write the mark.</para>
/// <para>Anything that fails while reading it means a full initialisation, never a failed start: on the very first start
/// the table does not exist yet. Anything that fails while writing it is a warning, never a failed start either: the
/// initialisation has succeeded, and a mark not written only means that the next start does it again.</para>
/// </remarks>
public sealed class InitialisationMarker(HubDbContext database, IClock clock, ILogger<InitialisationMarker> logger)
{
    /// <summary>The row of <c>hub_division_settings</c> that holds the mark.</summary>
    public const string SettingKey = "startup.initialised";

    /// <summary>How many times the mark is written before giving up, when every failure is another process writing it too.</summary>
    private const int WriteAttempts = 5;

    /// <summary>How long a start waits for another one's initialisation before it goes on without the lock.</summary>
    public TimeSpan LockWait { get; set; } = InitialisationLock.DefaultWait;

    /// <summary>
    /// Runs <paramref name="initialise"/> unless the mark says that a start with this very <paramref name="key"/> has
    /// already done it, then writes the mark; says which of the two happened and why. One process at a time: the
    /// initialisation runs under <see cref="InitialisationLock"/>, and a start that waited for it reads the mark again.
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

        // Only a start that has something to do pays for the lock: a plain wake has returned above. Held until the mark
        // is written, so that whoever waits behind it finds the mark.
        await using var held = await InitialisationLock.TakeAsync(
            ConnectionString(), LockWait, logger, cancellationToken);
        timings?.Step("initialisation lock");

        if (held.Held)
        {
            // Another start may have done everything while this one waited: the mark it read before the lock is old.
            stored = await ReadAsync(cancellationToken);
            timings?.Step("marker read again");

            changes = key.ChangesSince(stored);
            if (changes.Count == 0)
            {
                logger.LogInformation(
                    "Another start initialised the database while this one waited {Waited:0} ms for the initialisation lock.",
                    held.Report.Waited.TotalMilliseconds);
                return new InitialisationOutcome(Skipped: true, changes, stored) { Lock = held.Report };
            }
        }

        logger.LogInformation("A full initialisation: {Changes}.", string.Join("; ", changes));
        await initialise(cancellationToken);

        var written = await WriteAsync(key, cancellationToken);
        timings?.Step(written ? "marker written" : "marker not written");

        return new InitialisationOutcome(Skipped: false, changes, stored) { Lock = held.Report };
    }

    /// <summary>The database the mark is in, for the lock's own connection; null when the context cannot say.</summary>
    private string? ConnectionString()
    {
        try
        {
            return database.Database.GetConnectionString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
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

    /// <summary>
    /// Writes the mark; says whether it did. A mark not written is never a failed start (note
    /// 2026-09-29-il-marcatore-che-non-si-scrive): the initialisation has already succeeded, and the next start only does it
    /// again.
    /// </summary>
    private async Task<bool> WriteAsync(InitialisationKey key, CancellationToken cancellationToken)
    {
        var value = JsonSerializer.Serialize(new StoredInitialisation(
            key.Build, key.Configuration, key.Seed, key.Stamp, clock.UtcNow));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var row = await database.DivisionSettings.FirstOrDefaultAsync(setting => setting.Key == SettingKey, cancellationToken);
                if (row is null)
                {
                    row = new DivisionSetting { Key = SettingKey };
                    database.DivisionSettings.Add(row);
                }

                row.ValueJson = value;
                row.UpdatedAt = clock.UtcNow;

                await database.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (Exception exception) when (exception is DbException or DbUpdateException or InvalidOperationException
                && !cancellationToken.IsCancellationRequested)
            {
                // Whatever the database did with our changes, the next attempt reads the row again.
                database.ChangeTracker.Clear();

                if (attempt < WriteAttempts && IsAnotherWriter(exception))
                {
                    // Another process initialised at the same moment and wrote the row first: its mark is as true as ours,
                    // and ours is written over it, as an update this time. On MariaDB the two can also meet as a deadlock --
                    // both inserts wait on a row just deleted and not yet purged -- and the database has already rolled us
                    // back. A moment apart, so that two losers do not meet again.
                    await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(10, 50) * attempt), cancellationToken);
                    continue;
                }

                logger.LogWarning(
                    exception,
                    "The initialisation marker could not be written ({Reason}): the initialisation succeeded, and the next start does it again.",
                    exception.GetBaseException().Message);
                return false;
            }
        }
    }

    /// <summary>
    /// A duplicate key or a deadlock: another process wrote the same row at the same moment, and trying again resolves it.
    /// EF reports a deadlock wrapped, as a transient failure, so the whole chain is looked at.
    /// </summary>
    private static bool IsAnotherWriter(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry or MySqlErrorCode.LockDeadlock })
            {
                return true;
            }
        }

        return false;
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
    /// <summary>
    /// What the start got when it asked for the initialisation lock; null when it did not ask, because the mark matched
    /// at the first reading.
    /// </summary>
    public InitialisationLockReport? Lock { get; init; }

    /// <summary>
    /// The words of <c>starts.txt</c>: <c>initialisation skipped (…): migrations, content</c> or <c>initialisation full: …</c>.
    /// A start that skipped because another one initialised meanwhile says how long it waited for it; one that
    /// initialised without the lock says so, and why.
    /// </summary>
    public string Describe(IEnumerable<string> skippableSteps)
    {
        if (!Skipped)
        {
            var unlocked = Lock is { Held: false } ? $"; without the initialisation lock ({Lock.Refusal})" : string.Empty;
            return $"initialisation full: {string.Join("; ", Changes)}{unlocked}";
        }

        var waited = Lock is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"; waited {Lock.Waited.TotalMilliseconds:0} ms for the initialisation lock");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"initialisation skipped (marker of {Marker!.Stamp}, {Marker.At:yyyy-MM-dd HH:mm:ss'Z'}): {string.Join(", ", skippableSteps)}{waited}");
    }
}
