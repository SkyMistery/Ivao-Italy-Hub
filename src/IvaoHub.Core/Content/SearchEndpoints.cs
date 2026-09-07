using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>One hit. What it is and where it lives; the page itself is fetched by following it.</summary>
/// <param name="SourceModule"><c>core</c> for the editorial core, otherwise the module key.</param>
/// <param name="SourceId">Stable identifier of the row behind it, for example <c>content:42</c>.</param>
/// <param name="Kind">What the row is, so a result can be grouped and given an icon.</param>
/// <param name="Url">Where to follow it.</param>
/// <param name="OwnerDepartment">Whose row it is.</param>
/// <param name="Title">Its title, in the language searched.</param>
/// <param name="Snippet">
/// The few words around the first place the query turns up, as <b>text</b>. Which terms to mark is
/// the browser's business: it is the only one that knows the language on screen, and a server that
/// returned markup would be deciding how a page looks (design M1 section 7).
/// </param>
public sealed record SearchHitDto(
    string SourceModule,
    string SourceId,
    string Kind,
    string Url,
    Department OwnerDepartment,
    string Title,
    string Snippet);

/// <summary>
/// What a search answers with: one page of hits, in the envelope every list of the hub uses, plus
/// the one thing this particular list sometimes has to <b>say</b>.
/// </summary>
/// <param name="Results">The page of hits, in the envelope every list of the hub answers with.</param>
/// <param name="Notice">
/// An i18n key, or null when there is nothing to explain. Today it has one value: the query was made
/// only of words shorter than the index holds, so the empty answer is about the question and not
/// about the site. It is here and not inside <c>PagedResult</c> because it is a property of the
/// query rather than of the page, and every other list of the hub would have carried a null.
/// </param>
public sealed record SearchResponseDto(PagedResult<SearchHitDto> Results, string? Notice);

/// <summary>
/// Searching the site. It is a <b>different mechanism</b> from the <c>?q=</c> of a back office
/// list, and the difference is the point: that one is a <c>LIKE</c> over the columns of one table,
/// for a coordinator looking through their own rows; this one reads <c>cms_search_index</c>, the
/// projection the interceptor rewrites for every publishable row of every module, through the
/// FULLTEXT index (design M0 section 3.6).
/// <para>Anonymous, and safe to be: the index rows declare an owner and a visibility, so the global
/// query filter narrows them exactly as it narrows the pages they point at. There is no
/// <c>IgnoreQueryFilters</c> anywhere near this file and there must never be one.</para>
/// <para>M0 stops at the endpoint. The screen that uses it is M1, with the public site.</para>
/// </summary>
public static class SearchEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/search";

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    /// <summary>
    /// What the answer says when every word of the query is shorter than the index holds. A key and
    /// not a sentence: the browser resolves it, from the same language files as everything else.
    /// </summary>
    public const string TermsTooShort = "search.termsTooShort";

    /// <summary>No hits, and the reason when there is one.</summary>
    private static SearchResponseDto Nothing(int page, int pageSize, string? notice) =>
        new(new PagedResult<SearchHitDto>([], page, pageSize, Total: 0), notice);

    public static IEndpointRouteBuilder MapSearchEndpoint(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, async (
                string? q,
                int? page,
                int? pageSize,
                string? locale,
                HubDbContext database,
                ICurrentUser user,
                IOptions<DivisionOptions> division,
                CancellationToken cancellationToken) =>
            {
                var options = division.Value;

                // Which language to search in: what the caller asked for when the division speaks
                // it, otherwise the language of the reader. A division that does not publish in a
                // language has no index rows in it, so honouring the request would be a silent
                // empty page rather than an answer.
                var chosen = locale is not null
                    && options.Locales.Contains(locale, StringComparer.OrdinalIgnoreCase)
                        ? locale
                        : user.Locale;

                var requestedSize = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;
                var requestedPage = page is > 0 ? page.Value : 1;

                if (string.IsNullOrWhiteSpace(q))
                {
                    // An empty box is not an error and it is not the whole site either.
                    return TypedResults.Ok(Nothing(requestedPage, requestedSize, notice: null));
                }

                // ⚠️ The one thing the code cannot fix, so the one thing it has to say. InnoDB does
                // not index words shorter than three letters and the setting is global on a shared
                // server, so a query made only of those finds nothing however much is published —
                // and an empty page with no explanation reads as an answer about the site.
                if (FullTextSearch.EveryTermIsTooShort(q))
                {
                    return TypedResults.Ok(Nothing(requestedPage, requestedSize, TermsTooShort));
                }

                var matches = database.SearchIndex.Matching(chosen, q);

                var total = await matches.CountAsync(cancellationToken);

                // The text comes back only for the rows of this page, and the extract is cut here
                // rather than in SQL: it is twenty strings, and MariaDB has no idea what a word is
                // in the language the reader is using.
                var rows = await matches
                    .Skip((requestedPage - 1) * requestedSize)
                    .Take(requestedSize)
                    .Select(entry => new
                    {
                        entry.SourceModule,
                        entry.SourceId,
                        entry.Kind,
                        entry.Url,
                        entry.OwnerDepartment,
                        entry.Title,
                        entry.Text,
                    })
                    .ToListAsync(cancellationToken);

                var hits = rows
                    .Select(row => new SearchHitDto(
                        row.SourceModule,
                        row.SourceId,
                        row.Kind,
                        row.Url,
                        row.OwnerDepartment,
                        row.Title,
                        SearchSnippet.Of(row.Text, q)))
                    .ToList();

                return TypedResults.Ok(new SearchResponseDto(
                    new PagedResult<SearchHitDto>(hits, requestedPage, requestedSize, total),
                    Notice: null));
            })
            .WithName("Search")
            .WithTags("Search")
            .AllowAnonymous();

        return app;
    }
}
