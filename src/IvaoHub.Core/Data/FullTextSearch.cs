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
    /// The shortest word InnoDB puts in a FULLTEXT index (<c>innodb_ft_min_token_size</c>, whose
    /// default is three). ⚠️ It is written here as a constant because on a shared MariaDB the
    /// setting **is not ours to change** (design M1 section 7): the hub shares its server, the
    /// variable is global, and changing it would need every FULLTEXT index on the machine rebuilt.
    /// <para>So the limit is not worked around, it is <b>said</b>: a query made only of words this
    /// short finds nothing, and the endpoint tells the reader why instead of answering with an
    /// empty page that looks like an opinion about their question.</para>
    /// </summary>
    public const int MinimumTermLength = 3;

    /// <summary>What separates one word from another for the purpose of the rule above.</summary>
    private static readonly char[] TermSeparators = [' ', '\t', '\n', '\r', ',', ';', '.', ':', '!', '?', '"', '\''];

    /// <summary>
    /// True when every word of the query is shorter than the index will hold, so the answer would
    /// be empty whatever is published. A query with one long word and three short ones is **not**
    /// this case: MariaDB ignores the short ones and answers on the rest, which is a real answer.
    /// </summary>
    public static bool EveryTermIsTooShort(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var terms = query.Split(TermSeparators, StringSplitOptions.RemoveEmptyEntries);
        return terms.Length > 0 && terms.All(term => term.Length < MinimumTermLength);
    }

    /// <summary>
    /// The rows of one language that match, **most relevant first** and, among equals, most
    /// recently changed first (design M1 section 7).
    /// <para>The score is selected as a column and the ordering reads that column: the
    /// <c>MATCH</c> is written <b>once</b> in this file, which is the point — the same expression
    /// spelled twice is two places to keep identical, and the second one is always the one that
    /// gets forgotten.</para>
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

        // The one MATCH of this hub, built once and then used twice: as the ordering, and — greater
        // than zero — as the filter. The two columns are the ones the FULLTEXT index declares in
        // `CmsSchemaConfiguration`, or MariaDB answers "can't find FULLTEXT index matching the
        // column list".
        //
        // ⚠️ Why an expression built by hand rather than a score selected as a column and read back
        // by name: EF Core cannot filter on a member of a projection — `Select(… new { entry, score })`
        // followed by `Where(x => x.Score > 0)` is answered with "the LINQ expression could not be
        // translated", measured against a real MariaDB rather than assumed. So the expression is the
        // thing that is written once, instead of the column.
        Expression<Func<SearchIndexEntry, double>> score = entry => EF.Functions.Match(
            new[] { entry.Title, entry.Text },
            trimmed,
            MySqlMatchSearchMode.NaturalLanguage);

        var matches = Expression.Lambda<Func<SearchIndexEntry, bool>>(
            Expression.GreaterThan(score.Body, Expression.Constant(0d)),
            score.Parameters);

        return index
            .Where(entry => entry.Locale == locale)
            // In natural language mode a row that has nothing to do with the query scores exactly
            // zero, so "greater than zero" is what "matches" means.
            .Where(matches)
            .OrderByDescending(score)
            // Among equals, the most recently changed first (design M1 section 7).
            .ThenByDescending(entry => entry.UpdatedAt);
    }
}
