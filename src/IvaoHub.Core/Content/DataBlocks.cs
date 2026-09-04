using System.Text.Json.Nodes;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// What answers a data block. The properties arrive exactly as they were written into the body and
/// are read by the provider alone: the backend never learns what a block means (plan section 16.5).
/// <para>A provider is not a small module. It is a service of the core or of a module, registered
/// for one key, and it reads through the same <c>ICurrentUser</c> and the same visibility filter
/// as everything else — which is why the answer differs for the public site and for the staff, and
/// why nobody has to remember to filter.</para>
/// </summary>
public interface IDataBlockProvider
{
    /// <summary>The <see cref="IBlockDescriptor.ProviderKey"/> this one answers for.</summary>
    string Key { get; }

    /// <summary>
    /// The data the block draws, in the shape its TypeScript component expects. Translated values
    /// travel whole: only the browser knows which language it is showing.
    /// </summary>
    Task<JsonNode> ResolveAsync(JsonNode? props, CancellationToken cancellationToken);
}

/// <summary>The providers, by key. One lookup, so the two callers cannot disagree.</summary>
public sealed class DataBlockProviders
{
    private readonly Dictionary<string, IDataBlockProvider> _byKey;

    public DataBlockProviders(IEnumerable<IDataBlockProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _byKey = new Dictionary<string, IDataBlockProvider>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            if (!_byKey.TryAdd(provider.Key, provider))
            {
                throw new InvalidOperationException(
                    $"Two data block providers answer for the key '{provider.Key}'.");
            }
        }
    }

    public IDataBlockProvider? Find(string? key) =>
        key is not null && _byKey.TryGetValue(key, out var provider) ? provider : null;

    /// <summary>The provider of a block, or null when the block is not a data block at all.</summary>
    public IDataBlockProvider? For(IBlockDescriptor? descriptor) =>
        descriptor is null || descriptor.Kind != BlockKind.Data
            ? null
            : Find(descriptor.ProviderKey ?? descriptor.Type);
}

/// <summary>
/// The links of the division as a block: the same rows the back office edits, read through the
/// visibility filter so a page never shows a link its reader is not meant to see.
/// </summary>
public sealed class LinkListProvider(HubDbContext database) : IDataBlockProvider
{
    /// <summary>Never more than this, whatever the block asks for: a block is not an export.</summary>
    public const int MaxItems = 50;

    /// <summary>What a block that does not say gets.</summary>
    public const int DefaultLimit = 10;

    public string Key => CoreBlocks.LinkList;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, CancellationToken cancellationToken)
    {
        var category = Text(props, "category");
        var department = Text(props, "department");
        var limit = Math.Clamp(Number(props, "limit") ?? DefaultLimit, 1, MaxItems);

        // No IgnoreQueryFilters: the global filter is what decides who sees which link, and this
        // is a reader like any other (design M0 section 3.5).
        var query = database.Links.AsNoTracking().Where(link => link.IsActive);

        if (category is not null)
        {
            query = query.Where(link => link.Category == category);
        }

        if (Enum.TryParse<Department>(department, ignoreCase: true, out var owner))
        {
            query = query.Where(link => link.OwnerDepartment == owner);
        }

        var links = await query
            .OrderBy(link => link.Sort)
            .ThenBy(link => link.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var items = new JsonArray();
        foreach (var link in links)
        {
            items.Add(new JsonObject
            {
                ["title"] = Translated(link.Title),
                ["url"] = link.Url,
                ["description"] = link.Description is null ? null : Translated(link.Description),
            });
        }

        return new JsonObject { ["items"] = items };
    }

    private static JsonObject Translated(Localization.Localized<string> value)
    {
        var written = new JsonObject();
        foreach (var (locale, text) in value)
        {
            written[locale] = text;
        }

        return written;
    }

    private static string? Text(JsonNode? props, string name) =>
        props?[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static int? Number(JsonNode? props, string name) =>
        props?[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;
}
