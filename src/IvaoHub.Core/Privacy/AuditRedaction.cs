using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IvaoHub.Core.Privacy;

/// <summary>
/// What an erasure does to <c>hub_audit_log</c>, the one table no other code changes (note
/// <c>2026-09-25-la-cancellazione-dei-dati-di-una-persona</c> §4). The record keeps saying that something happened, when and
/// with what authority; it stops saying it about the person.
/// <list type="number">
/// <item>The copies of a row the erasure deleted or emptied lose their data (<see cref="EmptyAsync"/>).</item>
/// <item>The copies of a row about the person that was gone before the erasure lose the columns marked as their data
/// (<see cref="EmptyGoneAsync"/>, M4, E10n).</item>
/// <item>The rows the person wrote carry the pseudonym and no address (<see cref="ReplaceAsync"/>).</item>
/// <item>The rows about their user carry the pseudonym as their key, and no data.</item>
/// <item>Everywhere else, a property of the JSON that names a person (<see cref="PersonColumns.IsVidName"/>) and holds their
/// VID takes the pseudonym: the history of a page they wrote stays, with somebody nobody can name as its author.</item>
/// </list>
/// Of these, a row that nobody signed in wrote loses its address as well (<see cref="ForgetSignIn"/>): a sign in writes about the
/// member before they are anybody, so the address there is theirs. The copies of a row a module keeps as it is
/// (<see cref="ErasureRequest.Keep"/>, a ban in force) stay on the VID, like the row, and lose only the address (<see cref="Kept"/>).
/// Outside an erasure, one more: a module that deletes rows for retention has the columns marked as a person's data emptied in
/// their copies (<see cref="EmptyDeletedAsync"/>, M4, E10s).
/// Tracked rows, saved by the caller; nothing here goes around the context.
/// </summary>
public static class AuditRedaction
{
    /// <summary>How the core's own tables are named in the log: a user's rows are keyed by their VID.</summary>
    public const string UsersTable = "hub_users";

    private const int Page = 500;

    /// <summary>Empties every audit row of these rows, in the context they were erased through, and forgets a sign in's address.</summary>
    public static async Task EmptyAsync(
        DbContext context,
        IReadOnlyCollection<(string Table, string Key)> erased,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(erased);

        foreach (var table in erased.GroupBy(row => row.Table, StringComparer.Ordinal))
        {
            foreach (var keys in table.Select(row => row.Key).Distinct(StringComparer.Ordinal).Chunk(Page))
            {
                var rows = await context.Set<AuditLogEntry>()
                    .Where(row => row.Entity == table.Key && keys.Contains(row.EntityId)
                        && (row.BeforeJson != null || row.AfterJson != null || (row.Vid == AuditLogEntry.Nobody && row.Ip != null)))
                    .ToListAsync(cancellationToken);

                foreach (var row in rows)
                {
                    row.BeforeJson = null;
                    row.AfterJson = null;
                    ForgetSignIn(row);
                }
            }
        }
    }

    /// <summary>
    /// The columns marked as a person's data (<see cref="ErasedWithAttribute"/>), emptied in the audit rows of the rows that named
    /// the person in the marked person column and no longer exist: a booking withdrawn the week before keeps its row in the log,
    /// and loses the flight its pilot wrote (M4, E10n, note <c>2026-10-11-i-dati-di-una-persona-nell-audit</c>). A row whose key
    /// is still in the table is its eraser's, whoever it is about, and is left as it is here: emptied whole by
    /// <see cref="EmptyAsync"/> if the eraser deleted or emptied it, kept if the module keeps it or it is somebody else's now.
    /// Another person's rows are not touched, and of a gone row only the copies written while it was the person's
    /// (<see cref="EmptyWhileHeld"/>).
    /// <para>Run in every context, for the audited tables the context rewrites, after the module's eraser — a row it deleted is
    /// gone, and <see cref="EmptyAsync"/> empties it whole afterwards — and before <see cref="ReplaceAsync"/> turns the VID in
    /// the log into the pseudonym, which is what finds the person here. A row is found by its full copies, which name the
    /// person (<c>created</c>, <c>deleted</c>); then every audit row of its key is read, an <c>updated</c> one included.</para>
    /// </summary>
    public static async Task EmptyGoneAsync(DbContext context, int vid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var entity in context.Model.GetEntityTypes().Where(entity => PersonColumns.RewrittenBy(context, entity)))
        {
            var marks = PersonColumns.ErasedWith(entity);
            if (marks.Count == 0)
            {
                continue;
            }

            foreach (var (person, columns) in marks)
            {
                var named = await NamedAsync(context, entity.GetTableName()!, person.Name, vid, cancellationToken);
                var names = columns.Select(column => column.Name).ToArray();
                await EmptyGoneCopiesAsync(context, entity, named, copies => EmptyWhileHeld(copies, person.Name, names, vid), cancellationToken);
            }
        }
    }

    /// <summary>
    /// The columns marked as a person's data (<see cref="ErasedWithAttribute"/>), emptied in every audit row of these rows of one
    /// entity, which a module has just deleted for retention: a registration whose answers are kept for some months after the
    /// event leaves no answer in the log either (M4, E10s, note <c>2026-10-16-la-conservazione-nell-audit</c>). Every copy —
    /// <c>created</c>, both sides of an <c>updated</c>, <c>deleted</c> — and every mark, whoever the row was about: retention goes by
    /// age, and the time the data was kept for is over for all of it. The rest of each copy stays: the action, who did it, when,
    /// the other columns, the person column.
    /// <para>The keys are the rows the module deleted. One whose row is still in the table — whoever it is about — is left as it is,
    /// as by <see cref="EmptyGoneAsync"/>: the same reading of the table and of the log. So the call comes <b>after the save that
    /// deleted the rows</b>, best in the same transaction (begin, delete, save, this, save, commit): a failure then takes back the
    /// delete and the emptying together, and the next run finds the rows again; a call after a commit of its own works too, but a
    /// failure between the two leaves copies nobody will look for again. Called with the delete not yet saved, it refuses, rather
    /// than finding every row still there and emptying nothing.</para>
    /// <para>Refuses an entity the context does not map, one with no marked column (it would empty nothing without a word), and
    /// keys of another type than the entity's. Tracked rows, saved by the caller.</para>
    /// </summary>
    public static async Task EmptyDeletedAsync<TEntity, TKey>(
        DbContext context,
        IReadOnlyCollection<TKey> keys,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(keys);

        var entity = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{context.GetType().Name} does not map {typeof(TEntity).Name}.");

        var marks = PersonColumns.ErasedWith(entity);
        if (marks.Count == 0)
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name} has no column marked as a person's data (ErasedWith): there is nothing to empty in its audit.");
        }

        var key = entity.FindPrimaryKey()!.Properties[0];
        if (key.ClrType != typeof(TKey))
        {
            throw new ArgumentException($"The key of {typeof(TEntity).Name} is a {key.ClrType.Name}, not a {typeof(TKey).Name}.", nameof(keys));
        }

        if (context.ChangeTracker.Entries<TEntity>().Any(entry => entry.State == EntityState.Deleted))
        {
            throw new InvalidOperationException(
                $"A delete of {typeof(TEntity).Name} is not saved yet: save it first, so that the rows are gone when the audit is emptied.");
        }

        var names = marks.SelectMany(mark => mark.Columns).Select(column => column.Name).Distinct(StringComparer.Ordinal).ToArray();
        var deleted = keys.Select(value => HubSaveChangesInterceptor.FormatKey([value])).ToHashSet(StringComparer.Ordinal);

        await EmptyGoneCopiesAsync(
            context,
            entity,
            deleted,
            copies =>
            {
                foreach (var row in copies)
                {
                    row.BeforeJson = Empty(row.BeforeJson, names);
                    row.AfterJson = Empty(row.AfterJson, names);
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// The one way both callers reach the log: of these keys of the entity's table, as the log writes them, the ones no row has any
    /// more (<see cref="ExistingKeysAsync"/>); then the audit rows of each, in the order they were written, handed to
    /// <paramref name="empty"/> one row of the table at a time.
    /// </summary>
    private static async Task EmptyGoneCopiesAsync(
        DbContext context,
        IReadOnlyEntityType entity,
        HashSet<string> keys,
        Action<IReadOnlyList<AuditLogEntry>> empty,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return;
        }

        var table = entity.GetTableName()!;
        var key = entity.FindPrimaryKey()!.Properties[0];
        var task = (Task<List<string>>)ExistingKeysMethod.MakeGenericMethod(entity.ClrType, key.ClrType)
            .Invoke(null, [context, key.Name, keys, cancellationToken])!;
        keys.ExceptWith(await task);

        foreach (var chunk in keys.Chunk(Page))
        {
            var rows = await context.Set<AuditLogEntry>()
                .Where(row => row.Entity == table && chunk.Contains(row.EntityId))
                .OrderBy(row => row.Id)
                .ToListAsync(cancellationToken);

            foreach (var copies in rows.GroupBy(row => row.EntityId, StringComparer.Ordinal))
            {
                empty([.. copies]);
            }
        }
    }

    /// <summary>
    /// The audit rows of one row, in the order they were written, with the properties emptied on each side that was written
    /// while the person column held the person: a row that changed hands keeps the other person's data, on the side of the
    /// change that is theirs and in every row after it (M4, E10n, after the review of #251).
    /// <para>Who the row was about is read from the copies themselves: a <c>created</c> or <c>deleted</c> row holds every column,
    /// an <c>updated</c> one the person column only when it changed, and between the two the row stays with whom it was. Before
    /// the first copy that names the person column — a history begun before the table was audited — the row was about whom
    /// that copy says it was about before.</para>
    /// </summary>
    public static void EmptyWhileHeld(IReadOnlyList<AuditLogEntry> rows, string person, IReadOnlyCollection<string> properties, int vid)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(properties);

        var held = rows
            .SelectMany(row => new[] { PersonIn(row.BeforeJson, person, vid), PersonIn(row.AfterJson, person, vid) })
            .FirstOrDefault(side => side.Found)
            .Held;

        foreach (var row in rows)
        {
            var before = PersonIn(row.BeforeJson, person, vid);
            var after = PersonIn(row.AfterJson, person, vid);
            var heldBefore = before.Found ? before.Held : held;
            var heldAfter = after.Found ? after.Held : row.Action != DeletedAction && heldBefore;

            if (heldBefore)
            {
                row.BeforeJson = Empty(row.BeforeJson, properties);
            }

            if (heldAfter)
            {
                row.AfterJson = Empty(row.AfterJson, properties);
            }

            held = heldAfter;
        }
    }

    // The interceptor's action for a row taken away: after it, the row is about nobody.
    private const string DeletedAction = "deleted";

    private static readonly MethodInfo ExistingKeysMethod = typeof(AuditRedaction)
        .GetMethod(nameof(ExistingKeysAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>The keys of the table's audit rows whose JSON holds the person in this property: the rows that named them, once.</summary>
    private static async Task<HashSet<string>> NamedAsync(
        DbContext context,
        string table,
        string property,
        int vid,
        CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var pattern = $"%{vid.ToString(CultureInfo.InvariantCulture)}%";
        var last = 0L;
        while (true)
        {
            var rows = await context.Set<AuditLogEntry>().AsNoTracking()
                .Where(row => row.Entity == table && row.Id > last
                    && (EF.Functions.Like(row.BeforeJson!, pattern) || EF.Functions.Like(row.AfterJson!, pattern)))
                .OrderBy(row => row.Id)
                .Take(Page)
                .Select(row => new { row.Id, row.EntityId, row.BeforeJson, row.AfterJson })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return keys;
            }

            foreach (var row in rows.Where(row => PersonIn(row.BeforeJson, property, vid).Held || PersonIn(row.AfterJson, property, vid).Held))
            {
                keys.Add(row.EntityId);
            }

            last = rows[^1].Id;
        }
    }

    /// <summary>
    /// Of these keys, as the log holds them, the ones a row of the table still has, whoever it is about: a row that is there is
    /// its eraser's — one that stayed after a first run that failed half way, with the pseudonym already in it, and one that
    /// changed hands included (after the review of #251). Only the key is read.
    /// </summary>
    private static async Task<List<string>> ExistingKeysAsync<TEntity, TKey>(
        DbContext context,
        string key,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // The log holds a key as text (the interceptor's FormatKey); a text that is no key of this table names no row of it.
        var converter = TypeDescriptor.GetConverter(typeof(TKey));
        var values = new List<TKey>();
        foreach (var text in keys)
        {
            try
            {
                values.Add((TKey)converter.ConvertFromInvariantString(text)!);
            }
            catch (Exception refused) when (refused is FormatException or ArgumentException or NotSupportedException)
            {
            }
        }

        var existing = new List<string>();
        foreach (var chunk in values.Chunk(Page))
        {
            // Every row, whoever is signed in: the back office's way of reading, as PersonColumnRewrite reads them next.
            var found = await CrudSource.BackOffice<TEntity>(context)
                .Where(row => chunk.Contains(EF.Property<TKey>(row, key)))
                .Select(row => EF.Property<TKey>(row, key))
                .ToListAsync(cancellationToken);

            existing.AddRange(found.Select(value => HubSaveChangesInterceptor.FormatKey([value])));
        }

        return existing;
    }

    /// <summary>Whether a copy says whom the row was about (its JSON has the person column), and whether that is the person.</summary>
    private static (bool Found, bool Held) PersonIn(string? json, string property, int vid)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (false, false);
        }

        try
        {
            return JsonNode.Parse(json) is JsonObject row && row.TryGetPropertyValue(property, out var value)
                ? (true, value is JsonValue held && Holds(held, vid))
                : (false, false);
        }
        catch (JsonException)
        {
            return (false, false);
        }
    }

    /// <summary>The JSON of one audit row with these properties emptied; the same text when it has none of them.</summary>
    public static string? Empty(string? json, IReadOnlyCollection<string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return json;
        }

        if (root is not JsonObject row)
        {
            return json;
        }

        var changed = false;
        foreach (var property in properties.Where(property => row[property] is not null))
        {
            row[property] = null;
            changed = true;
        }

        return changed ? row.ToJsonString() : json;
    }

    /// <summary>
    /// The rows a module keeps as they are (<see cref="ErasureRequest.Keep"/>) that this context tracks, as table and key: for
    /// <see cref="ReplaceAsync"/>, which leaves their copies on the VID. The pseudonym there, next to the row that still says the VID,
    /// would be the one line that ties the two together (issue #323, note <c>2026-10-21-le-tracce-della-persona-nell-audit</c>).
    /// </summary>
    public static IReadOnlyList<(string Table, string Key)> Kept(DbContext context, ErasureRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        return [.. context.ChangeTracker.Entries()
            .Where(entry => request.IsKept(entry.Entity))
            .Select(entry => (entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name, HubSaveChangesInterceptor.ReadKey(entry)))];
    }

    /// <summary>
    /// Everything else, over the whole log, through the core's context, except the copies of the <paramref name="kept"/> rows
    /// (<see cref="Kept"/>): they keep the VID, as author and in their JSON, and lose only the address. Returns how many rows it
    /// changed.
    /// </summary>
    public static async Task<int> ReplaceAsync(
        HubDbContext database,
        int vid,
        int pseudonym,
        IReadOnlyCollection<(string Table, string Key)> kept,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(kept);

        var keep = kept.ToHashSet();

        var changed = new HashSet<long>();
        var text = vid.ToString(CultureInfo.InvariantCulture);
        var alias = pseudonym.ToString(CultureInfo.InvariantCulture);

        foreach (var row in await database.AuditLog.Where(row => row.Vid == vid).ToListAsync(cancellationToken))
        {
            row.Vid = keep.Contains((row.Entity, row.EntityId)) ? vid : pseudonym;
            row.Ip = null;
            changed.Add(row.Id);
        }

        foreach (var row in await database.AuditLog
            .Where(row => row.Entity == UsersTable && row.EntityId == text)
            .ToListAsync(cancellationToken))
        {
            row.EntityId = alias;
            row.BeforeJson = null;
            row.AfterJson = null;
            ForgetSignIn(row);
            changed.Add(row.Id);
        }

        // The rows whose JSON mentions the number at all, a page at a time: the walker then decides which of those
        // mentions is a person. A LIKE is a scan of the log, once per erasure, which is rare enough to afford.
        var pattern = $"%{text}%";
        var last = 0L;
        var named = new HashSet<(string Entity, string Key)>();
        while (true)
        {
            var rows = await database.AuditLog
                .Where(row => row.Id > last
                    && (EF.Functions.Like(row.BeforeJson!, pattern) || EF.Functions.Like(row.AfterJson!, pattern)))
                .OrderBy(row => row.Id)
                .Take(Page)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                break;
            }

            foreach (var row in rows.Where(row => !keep.Contains((row.Entity, row.EntityId))))
            {
                var before = Rewrite(row.BeforeJson, vid, pseudonym);
                var after = Rewrite(row.AfterJson, vid, pseudonym);
                if (before != row.BeforeJson || after != row.AfterJson)
                {
                    row.BeforeJson = before;
                    row.AfterJson = after;
                    named.Add((row.Entity, row.EntityId));
                    changed.Add(row.Id);
                }
            }

            last = rows[^1].Id;
        }

        // By the key, not by the JSON: an updated copy holds only the columns that changed, so the suspension of a grant revoked
        // since says nothing of whose it was. Its created copy does.
        foreach (var table in named.GroupBy(row => row.Entity, StringComparer.Ordinal))
        {
            foreach (var keys in table.Select(row => row.Key).Chunk(Page))
            {
                foreach (var row in await database.AuditLog
                    .Where(row => row.Entity == table.Key && keys.Contains(row.EntityId)
                        && row.Vid == AuditLogEntry.Nobody && row.Ip != null)
                    .ToListAsync(cancellationToken))
                {
                    ForgetSignIn(row);
                    changed.Add(row.Id);
                }
            }
        }

        return changed.Count;
    }

    /// <summary>
    /// The address of an audit row about the person that nobody signed in wrote. A sign in writes its rows in the OIDC callback,
    /// before the cookie exists — their user, the grants it suspends or resumes — so their author is nobody and the address is the
    /// member's (issue #323): the rows of their user, of a row erased with them and of a row whose copies name them lose it. A row
    /// somebody signed in wrote about the person keeps the address, which is the author's; a job has none.
    /// </summary>
    private static void ForgetSignIn(AuditLogEntry row)
    {
        if (row.Vid == AuditLogEntry.Nobody)
        {
            row.Ip = null;
        }
    }

    /// <summary>The JSON of one audit row with the person's VID replaced wherever it names a person; the same text when it does not.</summary>
    public static string? Rewrite(string? json, int vid, int pseudonym)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return json;
        }

        return Walk(root, name: null, vid, pseudonym) ? root!.ToJsonString() : json;
    }

    private static bool Walk(JsonNode? node, string? name, int vid, int pseudonym)
    {
        var changed = false;

        switch (node)
        {
            case JsonObject item:
                foreach (var (key, child) in item.ToList())
                {
                    if (child is JsonValue value && PersonColumns.IsVidName(key) && Holds(value, vid))
                    {
                        item[key] = pseudonym;
                        changed = true;
                    }
                    else if (child is JsonValue list && PersonColumns.VidLists.Contains(key)
                        && list.TryGetValue<string>(out var inner) && Rewrite(inner, vid, pseudonym) is { } rewritten
                        && rewritten != inner)
                    {
                        item[key] = rewritten;
                        changed = true;
                    }
                    else
                    {
                        changed |= Walk(child, key, vid, pseudonym);
                    }
                }

                break;

            case JsonArray items:
                // A bare list of numbers is a list of VIDs when it is the whole row (the set of super administrators) or a
                // list of participants; any other list of numbers is left alone.
                var ofPeople = name is null || name.EndsWith("Vids", StringComparison.OrdinalIgnoreCase);
                for (var index = 0; index < items.Count; index++)
                {
                    if (items[index] is JsonValue value && ofPeople && Holds(value, vid))
                    {
                        items[index] = pseudonym;
                        changed = true;
                    }
                    else
                    {
                        changed |= Walk(items[index], name, vid, pseudonym);
                    }
                }

                break;

            default:
                break;
        }

        return changed;
    }

    /// <summary>Whether a JSON document names the person anywhere a person is named: a notification about them, not only to them.</summary>
    public static bool Mentions(string? json, int vid) => Rewrite(json, vid, int.MinValue) != json;

    // A VID is a number, but some writers put it in a string: the data of a notification about a thread does (ContactThreads).
    private static bool Holds(JsonValue value, int vid) =>
        (value.TryGetValue<int>(out var number) && number == vid)
        || (value.TryGetValue<string>(out var text) && text == vid.ToString(CultureInfo.InvariantCulture));
}
