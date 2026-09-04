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
public sealed record SearchHitDto(
    string SourceModule,
    string SourceId,
    string Kind,
    string Url,
    Department OwnerDepartment,
    string Title);

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
                    return TypedResults.Ok(new PagedResult<SearchHitDto>([], requestedPage, requestedSize, Total: 0));
                }

                var matches = database.SearchIndex.Matching(chosen, q);

                var total = await matches.CountAsync(cancellationToken);
                var hits = await matches
                    .Skip((requestedPage - 1) * requestedSize)
                    .Take(requestedSize)
                    .Select(entry => new SearchHitDto(
                        entry.SourceModule,
                        entry.SourceId,
                        entry.Kind,
                        entry.Url,
                        entry.OwnerDepartment,
                        entry.Title))
                    .ToListAsync(cancellationToken);

                return TypedResults.Ok(new PagedResult<SearchHitDto>(hits, requestedPage, requestedSize, total));
            })
            .WithName("Search")
            .WithTags("Search")
            .AllowAnonymous();

        return app;
    }
}
