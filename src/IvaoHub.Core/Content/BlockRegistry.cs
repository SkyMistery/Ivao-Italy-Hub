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
/// The blocks of the core. M0 declared the five that prove the mechanism; M1 adds the set a public
/// site is actually written with (design M1 section 1.2). Modules bring their own.
/// <para>Every name here also exists in <c>web/src/blocks/core.ts</c>, and the two halves say
/// different things about the same block: this one is the type and its kind, which is all the server
/// is allowed to know, and the other is the schema, the component and the icon, which live only in
/// TypeScript (plan section 16.5). What keeps them from drifting is
/// <c>web/src/features/admin/registryDiff.ts</c>, drawn at the top of the ui-kit, and the fact that
/// a body holding a type this list has not got is refused on save.</para>
/// </summary>
public static class CoreBlocks
{
    public const string Heading = "heading";
    public const string Text = "text";
    public const string Callout = "callout";
    public const string Cta = "cta";
    public const string LinkList = "linkList";

    public const string Hero = "hero";
    public const string Image = "image";
    public const string Video = "video";
    public const string Embed = "embed";
    public const string Timeline = "timeline";
    public const string Table = "table";
    public const string CardGrid = "cardGrid";
    public const string IconGrid = "iconGrid";
    public const string Gallery = "gallery";
    public const string LogoGrid = "logoGrid";
    public const string Tabs = "tabs";
    public const string Accordion = "accordion";
    public const string Testimonial = "testimonial";
    public const string ButtonGroup = "buttonGroup";
    public const string Spacer = "spacer";
    public const string Divider = "divider";

    public static readonly IReadOnlyList<IBlockDescriptor> All =
    [
        new BlockDescriptor(Heading, Version: 1, BlockKind.Content),
        new BlockDescriptor(Text, Version: 1, BlockKind.Content),
        new BlockDescriptor(Callout, Version: 1, BlockKind.Content),
        new BlockDescriptor(Cta, Version: 1, BlockKind.Content),
        new BlockDescriptor(LinkList, Version: 1, BlockKind.Data, ProviderKey: LinkList),

        new BlockDescriptor(Hero, Version: 1, BlockKind.Content),
        new BlockDescriptor(Image, Version: 1, BlockKind.Content),
        new BlockDescriptor(Video, Version: 1, BlockKind.Content),
        new BlockDescriptor(Embed, Version: 1, BlockKind.Content),
        new BlockDescriptor(Timeline, Version: 1, BlockKind.Content),
        new BlockDescriptor(Table, Version: 1, BlockKind.Content),
        new BlockDescriptor(CardGrid, Version: 1, BlockKind.Content),
        new BlockDescriptor(IconGrid, Version: 1, BlockKind.Content),
        new BlockDescriptor(Gallery, Version: 1, BlockKind.Content),
        new BlockDescriptor(LogoGrid, Version: 1, BlockKind.Content),
        new BlockDescriptor(Tabs, Version: 1, BlockKind.Content),
        new BlockDescriptor(Accordion, Version: 1, BlockKind.Content),
        new BlockDescriptor(Testimonial, Version: 1, BlockKind.Content),
        new BlockDescriptor(ButtonGroup, Version: 1, BlockKind.Content),
        new BlockDescriptor(Spacer, Version: 1, BlockKind.Content),
        new BlockDescriptor(Divider, Version: 1, BlockKind.Content),
    ];
}
