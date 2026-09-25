using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Privacy;

/// <summary>
/// What an erasure does to <c>hub_audit_log</c>, the one table no other code changes (note
/// <c>2026-09-25-la-cancellazione-dei-dati-di-una-persona</c> §4). The record keeps saying that something happened, when and
/// with what authority; it stops saying it about the person.
/// <list type="number">
/// <item>The copies of a row the erasure deleted or emptied lose their data (<see cref="EmptyAsync"/>).</item>
/// <item>The rows the person wrote carry the pseudonym and no address (<see cref="ReplaceAsync"/>).</item>
/// <item>The rows about their user carry the pseudonym as their key, and no data.</item>
/// <item>Everywhere else, a property of the JSON that names a person (<see cref="PersonColumns.IsVidName"/>) and holds their
/// VID takes the pseudonym: the history of a page they wrote stays, with somebody nobody can name as its author.</item>
/// </list>
/// Tracked rows, saved by the caller; nothing here goes around the context.
/// </summary>
public static class AuditRedaction
{
    /// <summary>How the core's own tables are named in the log: a user's rows are keyed by their VID.</summary>
    public const string UsersTable = "hub_users";

    private const int Page = 500;

    /// <summary>Empties every audit row of these rows, in the context they were erased through.</summary>
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
                        && (row.BeforeJson != null || row.AfterJson != null))
                    .ToListAsync(cancellationToken);

                foreach (var row in rows)
                {
                    row.BeforeJson = null;
                    row.AfterJson = null;
                }
            }
        }
    }

    /// <summary>Everything else, over the whole log, through the core's context. Returns how many rows it changed.</summary>
    public static async Task<int> ReplaceAsync(HubDbContext database, int vid, int pseudonym, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var changed = 0;
        var text = vid.ToString(CultureInfo.InvariantCulture);
        var alias = pseudonym.ToString(CultureInfo.InvariantCulture);

        foreach (var row in await database.AuditLog.Where(row => row.Vid == vid).ToListAsync(cancellationToken))
        {
            row.Vid = pseudonym;
            row.Ip = null;
            changed++;
        }

        foreach (var row in await database.AuditLog
            .Where(row => row.Entity == UsersTable && row.EntityId == text)
            .ToListAsync(cancellationToken))
        {
            row.EntityId = alias;
            row.BeforeJson = null;
            row.AfterJson = null;
            changed++;
        }

        // The rows whose JSON mentions the number at all, a page at a time: the walker then decides which of those
        // mentions is a person. A LIKE is a scan of the log, once per erasure, which is rare enough to afford.
        var pattern = $"%{text}%";
        var last = 0L;
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

            foreach (var row in rows)
            {
                var before = Rewrite(row.BeforeJson, vid, pseudonym);
                var after = Rewrite(row.AfterJson, vid, pseudonym);
                if (before != row.BeforeJson || after != row.AfterJson)
                {
                    row.BeforeJson = before;
                    row.AfterJson = after;
                    changed++;
                }
            }

            last = rows[^1].Id;
        }

        return changed;
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
