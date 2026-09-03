using System.Text;
using System.Text.Json.Nodes;

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

    private static readonly string[] TemplateOnlyKeys = ["required", "locked"];

    private readonly HashSet<string> _locales = new(locales, StringComparer.OrdinalIgnoreCase);

    /// <summary>Every block of the body, in reading order, nested sections included.</summary>
    public IEnumerable<BlockDocumentNode> EnumerateBlocks(JsonNode? body)
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
                    yield return new BlockDocumentNode(block, $"{section.Path}.blocks[{index}]", section.Depth);
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
        }

        foreach (var block in EnumerateBlocks(root))
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
        }

        return errors.Count == 0 ? BlockDocumentValidation.Valid : new BlockDocumentValidation(errors);
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
