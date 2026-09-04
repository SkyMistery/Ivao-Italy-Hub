namespace IvaoHub.Core.Content;

/// <summary>
/// What a block is made of. A <see cref="Content"/> block draws what an editor typed into it; a
/// <see cref="Data"/> block draws what the hub knows, asked of a provider (design M0 section 5.4).
/// </summary>
public enum BlockKind
{
    Content,
    Data,
}

/// <summary>
/// What the backend knows about a block, which is everything except what the block looks like and
/// what its properties mean. The schema lives in TypeScript and nowhere else: the server checks the
/// envelope, publishes the registry so the client can tell it has a component for every type, and
/// hands <c>props</c> to a provider without reading them (CLAUDE.md section 2).
/// </summary>
public interface IBlockDescriptor
{
    /// <summary>The type as it appears in a body, for example <c>text</c> or <c>atc.roster</c>.</summary>
    string Type { get; }

    int Version { get; }

    BlockKind Kind { get; }

    /// <summary>
    /// True for a block that is meaningless captured — who is online, right now. Publication never
    /// freezes one and the editor does not offer the choice.
    /// </summary>
    bool AlwaysLive { get; }

    /// <summary>
    /// Which <see cref="IDataBlockProvider"/> answers for it. Null for a content block; for a data
    /// block it defaults to the type, and exists so that two blocks can share one provider.
    /// </summary>
    string? ProviderKey { get; }
}

/// <inheritdoc cref="IBlockDescriptor"/>
public sealed record BlockDescriptor(
    string Type,
    int Version,
    BlockKind Kind,
    bool AlwaysLive = false,
    string? ProviderKey = null) : IBlockDescriptor;

/// <summary>
/// Every block the installation knows: the ones of the core plus the ones the modules declare. It
/// is composed from the container, so a module adds a descriptor and nothing else has to be told.
/// <para>Two things read it: <c>ValidateEnvelope</c>, which refuses a body naming a type nobody
/// registered, and <c>/api/me</c>, which publishes it so the client can warn the staff about a
/// block the server knows and the browser cannot draw.</para>
/// </summary>
public sealed class BlockRegistry
{
    private readonly Dictionary<string, IBlockDescriptor> _byType;

    public BlockRegistry(IEnumerable<IBlockDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        _byType = new Dictionary<string, IBlockDescriptor>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            if (!_byType.TryAdd(descriptor.Type, descriptor))
            {
                // Two blocks answering to one name is a bug that only ever shows up as the wrong
                // thing drawn on a page, which is the kind that takes a day to find.
                throw new InvalidOperationException(
                    $"The block type '{descriptor.Type}' is registered twice.");
            }
        }

        All = [.. _byType.Values.OrderBy(descriptor => descriptor.Type, StringComparer.Ordinal)];
        Types = [.. All.Select(descriptor => descriptor.Type)];
    }

    /// <summary>Every descriptor, in a stable order so the bootstrap payload does not churn.</summary>
    public IReadOnlyList<IBlockDescriptor> All { get; }

    /// <summary>The type names, as the envelope validator wants them.</summary>
    public IReadOnlyCollection<string> Types { get; }

    public IBlockDescriptor? Find(string? type) =>
        type is not null && _byType.TryGetValue(type, out var descriptor) ? descriptor : null;
}

/// <summary>
/// The blocks of the core: the minimum that proves the mechanism rather than a library of them
/// (design M0 section 5.4). Four content blocks and one data block, which is what it takes to show
/// live and frozen side by side. Modules bring their own.
/// </summary>
public static class CoreBlocks
{
    public const string Heading = "heading";
    public const string Text = "text";
    public const string Callout = "callout";
    public const string Cta = "cta";
    public const string LinkList = "linkList";

    public static readonly IReadOnlyList<IBlockDescriptor> All =
    [
        new BlockDescriptor(Heading, Version: 1, BlockKind.Content),
        new BlockDescriptor(Text, Version: 1, BlockKind.Content),
        new BlockDescriptor(Callout, Version: 1, BlockKind.Content),
        new BlockDescriptor(Cta, Version: 1, BlockKind.Content),
        new BlockDescriptor(LinkList, Version: 1, BlockKind.Data, ProviderKey: LinkList),
    ];
}
