using System.Globalization;

namespace IvaoHub.Core.Content;

/// <summary>
/// The few words around the first place a query turns up in a page, so that a result says **why**
/// it is a result (design M1 section 7, point 2).
/// <para>⚠️ It answers with <b>text</b> and never with markup. Which terms to mark and how is the
/// browser's business — it is the only one that knows the language on screen — and a server that
/// returned HTML would be a server deciding how a page looks, plus a string nobody can escape
/// safely afterwards.</para>
/// <para>It is a plain function and not a query: it runs over the rows of one page, in memory,
/// after the database has already decided which twenty rows those are.</para>
/// </summary>
public static class SearchSnippet
{
    /// <summary>How much of the text comes back. Two lines on a phone, one on a desktop.</summary>
    public const int MaxLength = 180;

    /// <summary>How much of it sits before the word that was found, when there is room.</summary>
    private const int Lead = 60;

    /// <summary>What separates one word from another, both in the query and in the text.</summary>
    private static readonly char[] Separators =
        [' ', '\t', '\n', '\r', ',', ';', '.', ':', '!', '?', '"', '\'', '(', ')', '[', ']'];

    /// <summary>
    /// An extract of <paramref name="text"/> around the first term of <paramref name="query"/> that
    /// appears in it, or the beginning of the text when none does — which is what a row matched on
    /// its title looks like, and is still worth showing.
    /// <para>The comparison ignores case and accents, because a reader who types "cita" means to
    /// find "Città": that is the same rule the highlighting on the client follows, and the two are
    /// tested against the same examples.</para>
    /// </summary>
    public static string Of(string? text, string? query)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var collapsed = Collapse(text);
        var at = FirstOccurrence(collapsed, query);

        if (at < 0)
        {
            return Cut(collapsed, 0);
        }

        // Backed up to a word boundary, so the extract does not start in the middle of a word.
        var from = Math.Max(0, at - Lead);
        while (from > 0 && !char.IsWhiteSpace(collapsed[from - 1]))
        {
            from--;
        }

        return Cut(collapsed, from);
    }

    /// <summary>
    /// Where the first term of the query turns up, ignoring case and accents; -1 when none does.
    /// </summary>
    private static int FirstOccurrence(string text, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return -1;
        }

        var comparer = CompareInfo.GetCompareInfo(CultureInfo.InvariantCulture.Name);
        var first = -1;

        foreach (var term in query.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var at = comparer.IndexOf(text, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
            if (at >= 0 && (first < 0 || at < first))
            {
                first = at;
            }
        }

        return first;
    }

    /// <summary>The extract itself, with an ellipsis on the side that was cut.</summary>
    private static string Cut(string text, int from)
    {
        if (from == 0 && text.Length <= MaxLength)
        {
            return text;
        }

        var length = Math.Min(MaxLength, text.Length - from);
        var extract = text.Substring(from, length);

        // Trimmed back to a word boundary at the end too, so the last word is a word.
        var lastSpace = extract.LastIndexOf(' ');
        if (from + length < text.Length && lastSpace > MaxLength / 2)
        {
            extract = extract[..lastSpace];
        }

        return (from > 0 ? "… " : string.Empty) + extract.Trim() + (from + length < text.Length ? " …" : string.Empty);
    }

    /// <summary>
    /// The text of a page as one line. What the walker extracted is every string of every block
    /// joined together, so it carries the newlines of whatever markdown somebody wrote; an extract
    /// of it belongs on one line.
    /// </summary>
    private static string Collapse(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
