using System.Text;
using System.Text.Json.Nodes;
using IvaoHub.Core.Data;

namespace IvaoHub.Core.Content;

/// <summary>
/// One node of a body, with where it was found. The path is what a validation error has to name,
/// for example <c>sections[0].blocks[2]</c>.
/// </summary>
public sealed record BlockDocumentNode(JsonObject Node, string Path, int Depth)
{
    public string? Type => Node["type"]?.GetValue<string>();

    public string? Id => Node["id"]?.GetValue<string>();
}

/// <summary>One thing wrong with a body. The message is an i18n key, never prose.</summary>
public sealed record BlockDocumentError(string Key, string Path);

/// <summary>
/// A translated value inside a body that does not carry every language of the division, and where
/// it sits. Publication refuses a page holding any of these, naming each one, because a visitor
/// reading in the other language would be shown a hole.
/// </summary>
public sealed record BlockDocumentMissingLocale(string Path, IReadOnlyList<string> Locales);

/// <summary>
/// A file of the library that a body shows, and the path that names it. Publication checks each
/// one against the visibility of the page, because a picture the reader may not be served is a
/// hole in the page exactly like a missing translation is.
/// </summary>
public sealed record BlockDocumentMedia(string Path, long Id);

/// <summary>The outcome of validating an envelope.</summary>
public sealed record BlockDocumentValidation(IReadOnlyList<BlockDocumentError> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static BlockDocumentValidation Valid { get; } = new([]);
}

/// <summary>
/// Reads a body of sections and blocks knowing nothing about any block in particular: the envelope
/// (id, type, version, props, renderMode, frozen) is the only contract the backend has, and
/// <c>props</c> stays opaque (plan section 16.5, design M0 section 5.3).
/// <para>Search, publication and validation all go through this one walker, so "what is the text
/// of this page" has a single answer.</para>
/// </summary>
public sealed class BlockDocumentWalker(IReadOnlyCollection<string> locales)
{
    /// <summary>Bodies larger than this are refused: a page is text, not an upload channel.</summary>
    public const int MaxBodyBytes = 1024 * 1024;

    /// <summary>How deeply sections may nest. Three is what the editor can still show sensibly.</summary>
    public const int MaxDepth = 3;

    /// <summary>The only envelope version M0 knows how to read.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>
    /// How a section arranges its blocks. A closed set: with <c>stacked</c> the blocks follow one
    /// another, with any other layout each block says which column it is in.
    /// </summary>
    public static readonly IReadOnlyList<string> Layouts = ["stacked", "1/2+1/2", "1/3+2/3", "2/3+1/3", "3x1/3"];

    /// <summary>
    /// What a data block does when the page is read: ask the provider now, or show what was
    /// captured when the page was published. A content block carries neither.
    /// </summary>
    public static readonly IReadOnlyList<string> RenderModes = ["live", "frozen"];

    /// <summary>
    /// What sits behind a section. A closed set like the layouts, and checked for the same reason:
    /// the renderer reads a background it does not know as no background at all, so a value nobody
    /// refused would be a section that quietly loses its ground on the published page.
    /// <para>The other half is <c>BACKGROUNDS</c> in <c>web/src/blocks/envelope.ts</c>. The two
    /// agree by hand — they are values inside an opaque document, which the OpenAPI contract cannot
    /// carry — and the integration test that posts a background the server does not know is what
    /// keeps them agreeing.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> Backgrounds = ["none", "muted", "accent", "image"];

    private static readonly string[] TemplateOnlyKeys = ["required", "locked"];

    private readonly HashSet<string> _locales = new(locales, StringComparer.OrdinalIgnoreCase);

    /// <summary>Every block of the body, in reading order, nested sections included.</summary>
    public IEnumerable<BlockDocumentNode> EnumerateBlocks(JsonNode? body) =>
        EnumerateBlocksBySection(body).Select(pair => pair.Block);

    /// <summary>
    /// The same blocks, each with the section it sits in. A block is only readable next to its
    /// section for the things the section decides -- which column it may claim -- so the pairing is
    /// the enumeration and <see cref="EnumerateBlocks"/> is the half of it most callers want.
    /// </summary>
    public IEnumerable<(BlockDocumentNode Section, BlockDocumentNode Block)> EnumerateBlocksBySection(JsonNode? body)
    {
        if (body is not JsonObject root)
        {
            yield break;
        }

        foreach (var section in EnumerateSections(root["sections"] as JsonArray, "sections", depth: 1))
        {
            if (section.Node["blocks"] is not JsonArray blocks)
            {
                continue;
            }

            for (var index = 0; index < blocks.Count; index++)
            {
                if (blocks[index] is JsonObject block)
                {
                    yield return (
                        section,
                        new BlockDocumentNode(block, $"{section.Path}.blocks[{index}]", section.Depth));
                }
            }
        }
    }

    /// <summary>Every section of the body, outer ones first, with the depth they sit at.</summary>
    public IEnumerable<BlockDocumentNode> EnumerateSections(JsonNode? body)
    {
        if (body is not JsonObject root)
        {
            return [];
        }

        return EnumerateSections(root["sections"] as JsonArray, "sections", depth: 1);
    }

    /// <summary>
    /// All the leaf text of the body in one language: the properties of every block, resolving the
    /// localized objects found inside them. It is what ends up in the search index.
    /// </summary>
    public string ExtractText(JsonNode? body, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var text = new StringBuilder();
        foreach (var block in EnumerateBlocks(body))
        {
            AppendText(block.Node["props"], locale, text);
        }

        return text.ToString();
    }

    /// <summary>
    /// Checks the envelope and nothing else: version, size, unique identifiers, depth, block types
    /// the registry knows, and the structural keys only a template may carry.
    /// </summary>
    /// <param name="body">The parsed body.</param>
    /// <param name="knownBlockTypes">
    /// The types the registry declares, or <c>null</c> when the caller has no registry to check
    /// against: the registry is composed by the modules and does not exist yet in F4.
    /// </param>
    /// <param name="isTemplate">Whether the row being validated is a template.</param>
    public BlockDocumentValidation ValidateEnvelope(
        JsonNode? body,
        IReadOnlyCollection<string>? knownBlockTypes = null,
        bool isTemplate = false)
    {
        if (body is not JsonObject root)
        {
            return new BlockDocumentValidation([new BlockDocumentError("errors.body.notAnObject", "$")]);
        }

        var errors = new List<BlockDocumentError>();

        if (Encoding.UTF8.GetByteCount(root.ToJsonString()) > MaxBodyBytes)
        {
            errors.Add(new BlockDocumentError("errors.body.tooLarge", "$"));
        }

        if (root["schemaVersion"] is not JsonValue version
            || !version.TryGetValue<int>(out var schemaVersion)
            || schemaVersion != SupportedSchemaVersion)
        {
            errors.Add(new BlockDocumentError("errors.body.schemaVersion", "schemaVersion"));
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in EnumerateSections(root["sections"] as JsonArray, "sections", depth: 1))
        {
            if (section.Depth > MaxDepth)
            {
                errors.Add(new BlockDocumentError("errors.body.tooDeep", section.Path));
            }

            CheckIdentifier(section, identifiers, errors);
            CheckTemplateOnlyKeys(section, isTemplate, errors);
            CheckLayout(section, errors);
            CheckBackground(section, errors);
        }

        foreach (var (section, block) in EnumerateBlocksBySection(root))
        {
            CheckIdentifier(block, identifiers, errors);

            if (block.Type is null)
            {
                errors.Add(new BlockDocumentError("errors.body.blockTypeMissing", block.Path));
            }
            else if (knownBlockTypes is not null && !knownBlockTypes.Contains(block.Type))
            {
                errors.Add(new BlockDocumentError("errors.body.blockTypeUnknown", block.Path));
            }

            CheckRenderMode(block, errors);
            CheckColumn(section, block, errors);
        }

        return errors.Count == 0 ? BlockDocumentValidation.Valid : new BlockDocumentValidation(errors);
    }

    /// <summary>
    /// Every translated value inside the body that is not written in all the languages of the
    /// division, with the path that names it. It is the second half of the rule publication
    /// enforces, the first being the title of the row itself (design M0 section 5.5).
    /// <para>A draft is allowed to be incomplete, so nothing calls this on a write: it is asked
    /// once, when somebody is about to show the page to the public.</para>
    /// </summary>
    public IReadOnlyList<BlockDocumentMissingLocale> MissingLocales(JsonNode? body)
    {
        var missing = new List<BlockDocumentMissingLocale>();

        foreach (var block in EnumerateBlocks(body))
        {
            CollectMissingLocales(block.Node["props"], $"{block.Path}.props", missing);
        }

        return missing;
    }

    /// <summary>
    /// Every file of the library the body shows, wherever it is named: in the properties of a
    /// block, inside a list of them, or on a section that has a picture for its background.
    /// <para>The two property names are <see cref="JsonQuery.MediaKey"/> and
    /// <see cref="JsonQuery.MediaListKey"/>, taken from the one place that already knows them —
    /// the query that asks "which pages show this file". The two ask the same question of the same
    /// document, one in SQL and one in memory, and a second copy of the names is how they would
    /// start disagreeing (design M1 section 2, plan section 16.5).</para>
    /// <para>Like <see cref="MissingLocales"/>, this is asked when somebody is about to show the
    /// page to the public and never on a write: a draft may well point at a picture that is not
    /// ready.</para>
    /// </summary>
    public IReadOnlyList<BlockDocumentMedia> MediaReferences(JsonNode? body)
    {
        var found = new List<BlockDocumentMedia>();
        CollectMedia(body, string.Empty, found);
        return found;
    }

    /// <summary>
    /// True when every key of the object is a language of the division: that, and only that, is
    /// what tells a translated value apart from a property that happens to have short keys.
    /// </summary>
    public bool IsLocalizedObject(JsonObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Count > 0 && value.All(pair => _locales.Contains(pair.Key));
    }

    private static IEnumerable<BlockDocumentNode> EnumerateSections(JsonArray? sections, string path, int depth)
    {
        for (var index = 0; index < (sections?.Count ?? 0); index++)
        {
            if (sections![index] is not JsonObject section)
            {
                continue;
            }

            var sectionPath = $"{path}[{index}]";
            yield return new BlockDocumentNode(section, sectionPath, depth);

            foreach (var nested in EnumerateSections(
                section["sections"] as JsonArray,
                $"{sectionPath}.sections",
                depth + 1))
            {
                yield return nested;
            }
        }
    }

    private void AppendText(JsonNode? node, string locale, StringBuilder text)
    {
        switch (node)
        {
            case JsonValue value:
                if (value.TryGetValue<string>(out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    if (text.Length > 0)
                    {
                        text.Append(' ');
                    }

                    text.Append(raw);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    AppendText(item, locale, text);
                }

                break;

            // A translated value contributes only the language being extracted: the row of the
            // search index for "en" must not carry the Italian text as well.
            case JsonObject localized when IsLocalizedObject(localized):
                if (localized.TryGetPropertyValue(locale, out var translated))
                {
                    AppendText(translated, locale, text);
                }

                break;

            case JsonObject obj:
                foreach (var pair in obj)
                {
                    AppendText(pair.Value, locale, text);
                }

                break;

            default:
                break;
        }
    }

    private static void CheckIdentifier(
        BlockDocumentNode node,
        HashSet<string> identifiers,
        List<BlockDocumentError> errors)
    {
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            errors.Add(new BlockDocumentError("errors.body.idMissing", node.Path));
        }
        else if (!identifiers.Add(node.Id))
        {
            errors.Add(new BlockDocumentError("errors.body.idDuplicated", node.Path));
        }
    }

    /// <summary>
    /// How many columns a layout has. Anything but <c>stacked</c> is a row of columns, and the
    /// count is what a block's <c>column</c> is checked against.
    /// </summary>
    public static int ColumnsOf(string? layout) => layout switch
    {
        null or "stacked" => 1,
        "3x1/3" => 3,
        _ => 2,
    };

    private static void CheckLayout(BlockDocumentNode section, List<BlockDocumentError> errors)
    {
        if (section.Node["layout"] is JsonValue value
            && value.TryGetValue<string>(out var layout)
            && !Layouts.Contains(layout, StringComparer.Ordinal))
        {
            errors.Add(new BlockDocumentError("errors.body.layoutUnknown", $"{section.Path}.layout"));
        }
    }

    /// <summary>
    /// A background nobody refused would be drawn as no background at all: the renderer looks the
    /// value up in a table, and a miss is an empty class name. The section would lose its ground on
    /// the published page and nothing would have said so.
    /// </summary>
    private static void CheckBackground(BlockDocumentNode section, List<BlockDocumentError> errors)
    {
        if (section.Node["background"] is JsonValue value
            && value.TryGetValue<string>(out var background)
            && !Backgrounds.Contains(background, StringComparer.Ordinal))
        {
            errors.Add(new BlockDocumentError("errors.body.backgroundUnknown", $"{section.Path}.background"));
        }
    }

    /// <summary>
    /// A render mode the server does not know would be read by the renderer as "not frozen" and
    /// quietly turn a captured block back into a live one, which is the one thing publication is
    /// there to prevent.
    /// </summary>
    private static void CheckRenderMode(BlockDocumentNode block, List<BlockDocumentError> errors)
    {
        if (block.Node["renderMode"] is JsonValue value
            && value.TryGetValue<string>(out var mode)
            && !RenderModes.Contains(mode, StringComparer.Ordinal))
        {
            errors.Add(new BlockDocumentError("errors.body.renderModeUnknown", $"{block.Path}.renderMode"));
        }
    }

    /// <summary>
    /// With columns, a block says which one it is in; the number has to be one the layout of its
    /// own section actually has, or the block would be drawn nowhere.
    /// </summary>
    private static void CheckColumn(
        BlockDocumentNode section,
        BlockDocumentNode block,
        List<BlockDocumentError> errors)
    {
        if (block.Node["column"] is not JsonValue value || !value.TryGetValue<int>(out var column))
        {
            return;
        }

        var layout = (section.Node["layout"] as JsonValue)?.GetValue<string>();
        if (column < 0 || column >= ColumnsOf(layout))
        {
            errors.Add(new BlockDocumentError("errors.body.columnOutOfRange", $"{block.Path}.column"));
        }
    }

    private void CollectMissingLocales(JsonNode? node, string path, List<BlockDocumentMissingLocale> missing)
    {
        switch (node)
        {
            case JsonObject localized when IsLocalizedObject(localized):
                var absent = _locales
                    .Where(locale => !localized.TryGetPropertyValue(locale, out var written)
                        || written is not JsonValue value
                        || !value.TryGetValue<string>(out var text)
                        || string.IsNullOrWhiteSpace(text))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (absent.Length > 0)
                {
                    missing.Add(new BlockDocumentMissingLocale(path, absent));
                }

                break;

            case JsonObject obj:
                foreach (var pair in obj)
                {
                    CollectMissingLocales(pair.Value, $"{path}.{pair.Key}", missing);
                }

                break;

            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    CollectMissingLocales(array[index], $"{path}[{index}]", missing);
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Walks the whole document rather than the blocks, because a background picture belongs to a
    /// section and not to any block. Nothing here knows what a hero or a gallery is: a property
    /// with that name and a number in it is a file, at whatever depth it turns up.
    /// </summary>
    private static void CollectMedia(JsonNode? node, string path, List<BlockDocumentMedia> found)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var pair in obj)
                {
                    var childPath = path.Length == 0 ? pair.Key : $"{path}.{pair.Key}";

                    if (string.Equals(pair.Key, JsonQuery.MediaKey, StringComparison.Ordinal))
                    {
                        if (Identifier(pair.Value) is { } single)
                        {
                            found.Add(new BlockDocumentMedia(childPath, single));
                        }

                        continue;
                    }

                    if (string.Equals(pair.Key, JsonQuery.MediaListKey, StringComparison.Ordinal)
                        && pair.Value is JsonArray list)
                    {
                        for (var index = 0; index < list.Count; index++)
                        {
                            if (Identifier(list[index]) is { } item)
                            {
                                found.Add(new BlockDocumentMedia($"{childPath}[{index}]", item));
                            }
                        }

                        continue;
                    }

                    CollectMedia(pair.Value, childPath, found);
                }

                break;

            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    CollectMedia(array[index], $"{path}[{index}]", found);
                }

                break;

            default:
                break;
        }
    }

    /// <summary>The number under one of the two names, or nothing when it is null or not a number.</summary>
    private static long? Identifier(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<long>(out var id) ? id : null;

    private static void CheckTemplateOnlyKeys(BlockDocumentNode section, bool isTemplate, List<BlockDocumentError> errors)
    {
        if (isTemplate)
        {
            return;
        }

        // required, locked and allowedBlocks are what a template imposes on the pages made from it.
        // A page carrying them would be able to lift its own restrictions.
        foreach (var key in TemplateOnlyKeys)
        {
            if (section.Node[key] is JsonValue flag && flag.TryGetValue<bool>(out var raised) && raised)
            {
                errors.Add(new BlockDocumentError("errors.body.templateOnlyKey", $"{section.Path}.{key}"));
            }
        }

        if (section.Node["allowedBlocks"] is JsonArray)
        {
            errors.Add(new BlockDocumentError("errors.body.templateOnlyKey", $"{section.Path}.allowedBlocks"));
        }
    }
}
