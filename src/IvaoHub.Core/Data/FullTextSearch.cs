using System.Linq.Expressions;
using IvaoHub.Core.Content;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Data;

/// <summary>
/// The one place that asks MariaDB a <c>MATCH … AGAINST</c> question. It is here rather than in an
/// endpoint for the same reason <c>LocalizedQuery</c> is: a construct the provider only half knows
/// gets written once, is tested once, and does not end up copied with one detail changed
/// (implementation plan section E).
/// <para>The index it reads is <c>cms_search_index</c>, one row per source row and per language,
/// which is what makes a FULLTEXT index possible without a column hardcoded per language. The row
/// carries an owner and a visibility, so the global query filter applies to this query like to any
/// other: a search can never return something the reader may not see, and nothing here has to
/// remember that.</para>
/// </summary>
public static class FullTextSearch
{
    /// <summary>
    /// Longest query the endpoint accepts. A FULLTEXT search is not a place to send a paragraph,
    /// and the cap is what stops one from being sent.
    /// </summary>
    public const int MaxQueryLength = 128;

    /// <summary>
    /// The rows of one language whose title or text match, most relevant first is <b>not</b> what
    /// this returns: ordering by relevance would need the score as a second expression, and in
    /// natural language mode MariaDB already returns rows in relevance order for a plain
    /// <c>MATCH</c> in the <c>WHERE</c>. The caller pages it as it would page anything else.
    /// </summary>
    /// <param name="index">The search index, as read through the visibility filter.</param>
    /// <param name="locale">The language of the reader: one row of the index per language.</param>
    /// <param name="query">What was typed. Never interpolated: it becomes a parameter.</param>
    public static IQueryable<SearchIndexEntry> Matching(
        this IQueryable<SearchIndexEntry> index,
        string locale,
        string query)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var trimmed = query.Trim();
        if (trimmed.Length > MaxQueryLength)
        {
            trimmed = trimmed[..MaxQueryLength];
        }

        return index
            .Where(entry => entry.Locale == locale)
            // Natural language mode, which is what a search box means: no operators to learn, and
            // a stray + or " from somebody typing is text rather than a syntax error.
            .Where(Matches(trimmed));
    }

    /// <summary>
    /// The predicate on its own, so that the two columns of the FULLTEXT index are named in exactly
    /// one place. They have to match the index declared in <c>CmsSchemaConfiguration</c>, or
    /// MariaDB answers "can't find FULLTEXT index matching the column list".
    /// </summary>
    private static Expression<Func<SearchIndexEntry, bool>> Matches(string query) =>
        // MATCH answers with a relevance and not with a yes: in natural language mode a row that
        // has nothing to do with the query scores exactly zero, which is what "no match" means.
        entry => EF.Functions.Match(
            new[] { entry.Title, entry.Text },
            query,
            MySqlMatchSearchMode.NaturalLanguage) > 0;
}
