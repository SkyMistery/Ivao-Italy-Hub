using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IvaoHub.Core.Data;

namespace IvaoHub.Core.Content;

/// <summary>
/// One node of a body, with where it was found. The path is what a validation error has to name,
/// for example <c>sections[0].blocks[2]</c>.
/// </summary>
public sealed record BlockDocumentNode(JsonObject Node, string Path, int Depth)
{
    public string? Type => TextOf(Node["type"]);

    public string? Id => TextOf(Node["id"]);

    /// <summary>
    /// A member of the envelope read as text, or <c>null</c> when it is absent or is not text. The body is
    /// whatever the client sent: <c>GetValue&lt;string&gt;()</c> on <c>"id": 5</c> throws, and a throw inside
    /// the validation is a 500 where a refusal is owed (#321, point 1).
    /// </summary>
    public static string? TextOf(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
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

/// <summary>
/// An address a block leads to, and the path that names it: a property called <c>href</c>, or a link
/// inside a piece of Markdown. Publication checks those of this site (design of the editor, phase 3, section 5.9).
/// </summary>
public sealed record BlockDocumentLink(string Path, string Address);

/// <summary>The code an interactive block carries on its envelope, and where the block is.</summary>
/// <param name="Id">The identifier of the block.</param>
/// <param name="Path">Its path in the body, as the errors of the envelope name it.</param>
/// <param name="Source">The code.</param>
public sealed record BlockDocumentSource(string Id, string Path, string Source);

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

    /// <summary>
    /// How deeply sections may nest: a section, a row in it, a row in that, and one more — four,
    /// since 11 September 2026 (Carmine, composing: "a section in a section in a section in a
    /// section"). Three had been the ceiling since M1, and the editor offered two.
    /// </summary>
    public const int MaxDepth = 4;

    /// <summary>The only envelope version M0 knows how to read.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>
    /// How much source an interactive block may carry, and how much of it a whole page may
    /// (12 September 2026, <c>decisions/2026-09-12-il-blocco-interattivo.md</c>).
    /// <para>⚠️ The server knows the field and not what is in it: <c>source</c> lives on the
    /// envelope, beside <c>renderMode</c> and <c>frozen</c>, precisely so that reading it is not
    /// reading inside <c>props</c> — which stays opaque (plan section 16.5). What is checked here is
    /// that it is a string and that it is not enormous; what it means is the browser's business.</para>
    /// <para>The numbers are a choice and not physics: a runway drawn in SVG is 5 to 15 KB and a
    /// widget with four scenes fits inside 64. They are here, in one place, so that raising them is
    /// a line rather than an archaeology.</para>
    /// </summary>
    public const int MaxBlockSourceBytes = 64 * 1024;

    public const int MaxBodySourceBytes = 256 * 1024;

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
    public static readonly IReadOnlyList<string> Backgrounds =
        ["none", "muted", "accent", "brand", "deep", "dark", "aurora", "image"];

    /// <summary>The background that needs a file of the library, named by the section's own <c>mediaId</c>.</summary>
    private const string BackgroundImage = "image";

    /// <summary>
    /// What an address inside a block is called, as a file is called <see cref="JsonQuery.MediaKey"/>
    /// (<c>docs/UI-GUIDELINES.md</c>, "How a block names an address").
    /// </summary>
    private const string HrefKey = "href";

    /// <summary>The refusal of an address that is not one to follow (<see cref="SiteDestinations.IsFollowable"/>).</summary>
    public const string UnfollowableAddressKey = "errors.url.unsafe";

    private static readonly string[] TemplateOnlyKeys = ["required", "locked"];

    private readonly HashSet<string> _locales = new(locales, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The code of every interactive block of the body that carries some, read off the envelope (note
    /// 2026-09-12-il-blocco-interattivo): what the save compares, to ask for <c>Content.EmbedCode</c> when it
    /// changes (#318). A block of any other type carries none: the envelope refuses it.
    /// </summary>
    public IReadOnlyList<BlockDocumentSource> Sources(JsonNode? body) =>
    [
        .. EnumerateBlocks(body)
            .Where(block => string.Equals(block.Type, CoreBlocks.Interactive, StringComparison.Ordinal)
                && block.Id is not null)
            .Select(block => (block, Source: (block.Node["source"] as JsonValue)?.TryGetValue<string>(out var source) == true
                ? source
                : null))
            .Where(read => read.Source is not null)
            .Select(read => new BlockDocumentSource(read.block.Id!, read.block.Path, read.Source!)),
    ];

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
    /// The <b>prose</b> of the body in one language, which is what ends up in the search index and,
    /// cut short, in the snippet a reader is shown.
    /// <para>⚠️ Prose means <b>the values inside a translated map, and nothing else</b>. It used to
    /// mean every string found under <c>props</c>, and the snippet of <c>/start</c> read
    /// "… quattro semplici passi. <c>left muted</c> Prima di tutto…": <c>align</c> and <c>tone</c>
    /// of the <c>hero</c> block, two enumerations stored as strings. CLAUDE.md section 4 already
    /// forbade a non-prose string inside <c>props</c>, and forbidding did not work — nobody could
    /// see it until a snippet contained real prose. This is the rule that makes it structural, and
    /// it needs <b>no knowledge of any block schema</b> (design M0 section 5.3): prose in a block is
    /// always <c>Localized</c>, an enumeration is a bare string, and so is a URL, which has no
    /// business in a search index either.</para>
    /// <para>⚠️ And it costs something, written here rather than discovered later: a searchable
    /// string that is deliberately <b>not</b> translated stops being indexed. Two exist — the name
    /// of a partner in <c>logoWall</c> and the author of a <c>testimonial</c>, both of them a
    /// person or a company, the same word in every language. Making them translated would change
    /// the shape of props that pages already hold, which is a decision of its own.</para>
    /// </summary>
    public string ExtractText(JsonNode? body, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var text = new StringBuilder();
        foreach (var block in EnumerateBlocks(body))
        {
            AppendText(block.Node["props"], locale, text, insideTranslation: false);
        }

        return text.ToString();
    }

    /// <summary>
    /// The Markdown taken back out of a piece of prose, because a reader of a snippet should not be
    /// shown <c>**IVAO Italia** is the community…</c> — and somebody searching for a word should not
    /// miss it for an asterisk stuck to its front.
    /// <para>It is deliberately small and undoes only what an editor can write: a link keeps its
    /// text and loses its address, a colour keeps its words and loses its name, a heading, a quote or
    /// a bullet loses its marker, and the characters that carry emphasis, strikethrough and code go. Anything cleverer would be a Markdown parser in
    /// the search path, and the renderer already owns the one that matters.</para>
    /// </summary>
    internal static string Prose(string markdown)
    {
        var text = LinkPattern.Replace(markdown, "$1");
        text = ColourPattern.Replace(text, "$1");
        text = LineMarkerPattern.Replace(text, string.Empty);
        text = EmphasisPattern.Replace(text, string.Empty);

        return WhitespacePattern.Replace(text, " ").Trim();
    }

    /// <summary>A ceiling on every pattern here: a body is user written, and a search must not hang.</summary>
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    /// <summary>
    /// <c>[text](/somewhere)</c> and <c>![alt](/picture.png)</c> keep the half a reader reads. The text may hold brackets of
    /// its own, one level deep, as CommonMark allows (<c>[see [here]](/somewhere)</c>): without them a link went unread by
    /// the checks (#321, point 5, the review of #493).
    /// </summary>
    private static readonly Regex LinkPattern =
        new(@"!?\[((?:[^\[\]]|\[[^\[\]]*\])*)\]\((?<target>[^)]*)\)", RegexOptions.Compiled, OneSecond);

    /// <summary>
    /// The definition of a link by reference, on a line of its own: <c>[r]: /somewhere "Title"</c>, used as <c>[text][r]</c>.
    /// Its address is a link's like any other (#321, point 5, the review of #493). The whole line, title included, or it is
    /// not a definition: <c>[Nota]: questo è il testo</c> is a line of prose, for CommonMark as for this pattern.
    /// </summary>
    private static readonly Regex ReferencePattern = new(
        @"^[ ]{0,3}\[[^\]]+\]:[ \t]*(?:<(?<target>[^>\n]*)>|(?<target>\S+))(?:[ \t]+(?:""[^""\n]*""|'[^'\n]*'|\([^)\n]*\)))?[ \t]*\r?$",
        RegexOptions.Compiled | RegexOptions.Multiline,
        OneSecond);

    /// <summary>
    /// <c>:ocean[parola]</c> keeps its words, whatever the name (design of the editor, phase 3, §4.6): the renderer draws a
    /// name outside the set as plain text, so the words of both are what a reader reads. Only a name of letters, as the
    /// renderer reads it: <c>10:30</c> is a time. After the links, so that a link inside a colour has already lost its
    /// brackets.
    /// </summary>
    private static readonly Regex ColourPattern =
        new(@":[a-z]+\[((?:[^\[\]\n]|\[[^\]\n]*\])*)\]", RegexOptions.Compiled, OneSecond);

    /// <summary>
    /// What starts a heading, a quote or a list item, at the start of a line and nowhere else: a
    /// hyphen in the middle of a sentence is a hyphen.
    /// </summary>
    private static readonly Regex LineMarkerPattern = new(
        @"^[ \t]*(?:#{1,6}|>|[-+*]|\d+\.)[ \t]+",
        RegexOptions.Compiled | RegexOptions.Multiline,
        OneSecond);

    /// <summary>
    /// Emphasis and code. Underscores are left alone: <c>snake_case</c> is a word, and an italic
    /// written with them is rare enough not to be worth breaking one.
    /// </summary>
    private static readonly Regex EmphasisPattern = new(@"[*`~]", RegexOptions.Compiled, OneSecond);

    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled, OneSecond);

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
            CheckKey(section, errors);
            CheckTemplateOnlyKeys(section, isTemplate, errors);
            CheckLayout(section, errors);
            CheckBackground(section, errors);
        }

        var sourceBytes = 0;

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
            CheckSpan(block, errors);
            CheckAlign(block, errors);
            sourceBytes += CheckSource(block, errors);
        }

        // Every address the blocks lead to, a button's or a link in a piece of Markdown, by one rule (#321, point 5).
        foreach (var link in Links(root))
        {
            if (!SiteDestinations.IsFollowable(link.Address))
            {
                errors.Add(new BlockDocumentError(UnfollowableAddressKey, link.Path));
            }
        }

        if (sourceBytes > MaxBodySourceBytes)
        {
            errors.Add(new BlockDocumentError("errors.body.sourceTooLarge", "$"));
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
    /// The sections that ask for a picture behind them and name none. Read off the envelope and not out of
    /// <c>props</c>, so it needs no schema: <c>background</c> and <c>mediaId</c> are the section's own
    /// (note 2026-11-07-la-foto-rimasta-da-scegliere). The path is the one <see cref="MediaReferences"/> gives a
    /// picture that is there.
    /// </summary>
    public IReadOnlyList<string> SectionsWithoutTheirPicture(JsonNode? body) =>
    [
        .. EnumerateSections(body)
            .Where(section => section.Node["background"] is JsonValue background
                && background.TryGetValue<string>(out var name)
                && string.Equals(name, BackgroundImage, StringComparison.Ordinal)
                && Identifier(section.Node[JsonQuery.MediaKey]) is null)
            .Select(section => $"{section.Path}.{JsonQuery.MediaKey}"),
    ];

    /// <summary>
    /// The files the blocks of the body declare they cannot do without (<see cref="IBlockDescriptor.RequiredMedia"/>) and
    /// nobody has chosen, each with the path of the field. Only those paths are looked at, and nothing else of
    /// <c>props</c>. A list that is empty or absent has no picture to choose; an entry without its file has.
    /// </summary>
    public IReadOnlyList<string> BlocksWithoutTheirPictures(JsonNode? body, BlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var found = new List<string>();

        foreach (var block in EnumerateBlocks(body))
        {
            foreach (var declared in blocks.Find(block.Type)?.RequiredMedia ?? [])
            {
                CollectUnchosen(block.Node["props"], declared.Split('.'), 0, $"{block.Path}.props", found);
            }
        }

        return found;
    }

    /// <summary>
    /// Every address the blocks of the body lead to, wherever it is named, inside or outside this site: which ones to
    /// ask about is for the caller. Nothing here knows what a button is: a string under the name <c>href</c> is an
    /// address at whatever depth it turns up, as a number under <c>mediaId</c> is a file, and any other string may be
    /// Markdown. A picture (<c>![alt](/file.png)</c>) is not a link. Only <c>props</c>: a capture of a data block is
    /// what the provider answered, and nobody wrote it.
    /// </summary>
    public IReadOnlyList<BlockDocumentLink> Links(JsonNode? body)
    {
        var found = new List<BlockDocumentLink>();

        foreach (var block in EnumerateBlocks(body))
        {
            CollectLinks(block.Node["props"], $"{block.Path}.props", found);
        }

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

    /// <summary>Walks one value of a block, appending whatever prose it holds.</summary>
    /// <param name="node">The value being walked.</param>
    /// <param name="locale">The language being extracted.</param>
    /// <param name="text">Where the prose is collected.</param>
    /// <param name="insideTranslation">
    /// Whether the walk has passed through a translated map on its way here. Only then is a string
    /// prose; outside one it is an enumeration, an identifier or a URL, and the index is better
    /// without it.
    /// </param>
    private void AppendText(JsonNode? node, string locale, StringBuilder text, bool insideTranslation)
    {
        switch (node)
        {
            case JsonValue value:
                if (insideTranslation
                    && value.TryGetValue<string>(out var raw)
                    && Prose(raw) is { Length: > 0 } prose)
                {
                    if (text.Length > 0)
                    {
                        text.Append(' ');
                    }

                    text.Append(prose);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    AppendText(item, locale, text, insideTranslation);
                }

                break;

            // A translated value contributes only the language being extracted: the row of the
            // search index for "en" must not carry the Italian text as well.
            case JsonObject localized when IsLocalizedObject(localized):
                if (localized.TryGetPropertyValue(locale, out var translated))
                {
                    AppendText(translated, locale, text, insideTranslation: true);
                }

                break;

            case JsonObject obj:
                foreach (var pair in obj)
                {
                    AppendText(pair.Value, locale, text, insideTranslation);
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

    /// <summary>
    /// The key a section keeps from its template is text or nothing. A number would be saved and then
    /// break every comparison with the published version (#321, point 1).
    /// </summary>
    private static void CheckKey(BlockDocumentNode section, List<BlockDocumentError> errors)
    {
        if (section.Node["key"] is { } key && BlockDocumentNode.TextOf(key) is null)
        {
            errors.Add(new BlockDocumentError("errors.body.keyNotText", $"{section.Path}.key"));
        }
    }

    /// <summary>
    /// Whether a member of the envelope that is either absent or one of a closed set of names is
    /// neither: a value that is not text counts as a name nobody knows.
    /// </summary>
    private static bool IsUnknown(JsonNode? node, IReadOnlyList<string> known) =>
        node is not null
        && (BlockDocumentNode.TextOf(node) is not { } name || !known.Contains(name, StringComparer.Ordinal));

    private static void CheckLayout(BlockDocumentNode section, List<BlockDocumentError> errors)
    {
        if (IsUnknown(section.Node["layout"], Layouts))
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
        if (IsUnknown(section.Node["background"], Backgrounds))
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
        if (IsUnknown(block.Node["renderMode"], RenderModes))
        {
            errors.Add(new BlockDocumentError("errors.body.renderModeUnknown", $"{block.Path}.renderMode"));
        }
    }

    /// <summary>
    /// The source of an interactive block: a string, and a small one. Returns how many bytes it
    /// added, because the page has a budget of its own.
    /// <para>A block that carries something other than a string is refused rather than ignored: the
    /// endpoint that serves the frame would have nothing to serve, and an empty frame in a published
    /// document is the kind of fault nobody reports because it looks like a missing picture.</para>
    /// <para>And only an interactive block carries one (#318): code on a block of any other type is code
    /// nobody sees in the editor and nothing asks <c>Content.EmbedCode</c> about.</para>
    /// </summary>
    private static int CheckSource(BlockDocumentNode block, List<BlockDocumentError> errors)
    {
        if (block.Node["source"] is not JsonNode node)
        {
            return 0;
        }

        if (!string.Equals(block.Type, CoreBlocks.Interactive, StringComparison.Ordinal))
        {
            errors.Add(new BlockDocumentError("errors.body.sourceNotInteractive", $"{block.Path}.source"));
            return 0;
        }

        if (node is not JsonValue value || !value.TryGetValue<string>(out var source))
        {
            errors.Add(new BlockDocumentError("errors.body.sourceNotText", $"{block.Path}.source"));
            return 0;
        }

        var bytes = Encoding.UTF8.GetByteCount(source);
        if (bytes > MaxBlockSourceBytes)
        {
            errors.Add(new BlockDocumentError("errors.body.sourceTooLarge", $"{block.Path}.source"));
        }

        return bytes;
    }

    /// <summary>
    /// With columns, a block says which one it is in; the number has to be one the layout of its
    /// own section actually has, or the block would be drawn nowhere.
    /// </summary>
    /// <summary>
    /// The widths a block may take on a dashboard, in columns of twelve: ¼, ⅓, ½, ⅔, ¾ and the whole
    /// row (note 2026-09-13-le-dashboard-a-tutto-schermo §3.5). On the envelope, like the column.
    /// </summary>
    public static readonly IReadOnlyList<int> Spans = [3, 4, 6, 8, 9, 12];

    /// <summary>A width that is not one of <see cref="Spans"/> would be a tile the grid cannot draw.</summary>
    private static void CheckSpan(BlockDocumentNode block, List<BlockDocumentError> errors)
    {
        if (block.Node["span"] is not JsonValue value)
        {
            return;
        }

        if (!value.TryGetValue<int>(out var span) || !Spans.Contains(span))
        {
            errors.Add(new BlockDocumentError("errors.body.spanUnknown", $"{block.Path}.span"));
        }
    }

    /// <summary>
    /// Where a block stands in its column: on the left, in the middle or on the right (design of the
    /// editor's phase 3, §5.6). On the envelope beside <c>column</c> and <c>span</c>, because it is
    /// where the block is put and not what it says; absent is the left. Which blocks it means
    /// something for is the browser's business (a block's registration declares it), as is how it is
    /// drawn: the server only keeps the value to one of the three.
    /// <para>The other half is <c>ALIGNS</c> in <c>web/src/blocks/envelope.ts</c>, and the two agree
    /// by hand like the layouts and the grounds do: the integration test that posts an alignment the
    /// server does not know is what keeps them agreeing.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> Aligns = ["left", "center", "right"];

    /// <summary>
    /// An alignment that is not one of <see cref="Aligns"/>. Anything else than one of the three
    /// strings is refused, a number or an object as much as a fourth word: the browser reads the
    /// envelope with the same closed set, and a body it cannot read is drawn as an empty page.
    /// </summary>
    private static void CheckAlign(BlockDocumentNode block, List<BlockDocumentError> errors)
    {
        if (block.Node["align"] is not JsonNode node)
        {
            return;
        }

        if (node is not JsonValue value
            || !value.TryGetValue<string>(out var align)
            || !Aligns.Contains(align, StringComparer.Ordinal))
        {
            errors.Add(new BlockDocumentError("errors.body.alignUnknown", $"{block.Path}.align"));
        }
    }

    private static void CheckColumn(
        BlockDocumentNode section,
        BlockDocumentNode block,
        List<BlockDocumentError> errors)
    {
        if (block.Node["column"] is not JsonValue value || !value.TryGetValue<int>(out var column))
        {
            return;
        }

        var layout = BlockDocumentNode.TextOf(section.Node["layout"]);
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

    /// <summary>
    /// Follows one declared path. The name it ends on has to hold a number; a list is walked entry by entry; an object
    /// on the way that is not there has nothing to ask, because only what holds it could make the file necessary.
    /// </summary>
    private static void CollectUnchosen(JsonNode? node, string[] parts, int at, string path, List<string> found)
    {
        var part = parts[at];

        if (part.EndsWith("[]", StringComparison.Ordinal))
        {
            var name = part[..^2];

            if ((node as JsonObject)?[name] is JsonArray list)
            {
                for (var index = 0; index < list.Count; index++)
                {
                    CollectUnchosen(list[index], parts, at + 1, $"{path}.{name}[{index}]", found);
                }
            }
        }
        else if (at == parts.Length - 1)
        {
            if (Identifier((node as JsonObject)?[part]) is null)
            {
                found.Add($"{path}.{part}");
            }
        }
        else if ((node as JsonObject)?[part] is JsonObject inner)
        {
            CollectUnchosen(inner, parts, at + 1, $"{path}.{part}", found);
        }
    }

    private static void CollectLinks(JsonNode? node, string path, List<BlockDocumentLink> found)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var pair in obj)
                {
                    if (string.Equals(pair.Key, HrefKey, StringComparison.Ordinal)
                        && pair.Value is JsonValue href
                        && href.TryGetValue<string>(out var address))
                    {
                        found.Add(new BlockDocumentLink($"{path}.{pair.Key}", address));
                        continue;
                    }

                    CollectLinks(pair.Value, $"{path}.{pair.Key}", found);
                }

                break;

            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    CollectLinks(array[index], $"{path}[{index}]", found);
                }

                break;

            case JsonValue value when value.TryGetValue<string>(out var text):
                foreach (Match link in LinkPattern.Matches(text))
                {
                    if (!link.Value.StartsWith('!'))
                    {
                        found.Add(new BlockDocumentLink(path, TargetOf(link.Groups["target"].Value)));
                    }
                }

                foreach (Match reference in ReferencePattern.Matches(text))
                {
                    found.Add(new BlockDocumentLink(path, reference.Groups["target"].Value));
                }

                break;

            default:
                break;
        }
    }

    /// <summary>What a Markdown link leads to, without its title: <c>/about "About us"</c>, <c>&lt;/about&gt;</c>.</summary>
    private static string TargetOf(string written)
    {
        var target = written.Trim();
        var space = target.IndexOfAny([' ', '\t']);

        return (space < 0 ? target : target[..space]).Trim('<', '>');
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
