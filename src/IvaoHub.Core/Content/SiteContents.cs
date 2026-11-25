using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>A row of the content as the site shows it to whoever is asking: where it is read, and under which title.</summary>
/// <param name="Id">The row of <c>cms_contents</c>.</param>
/// <param name="Kind">A page, a news item or a document.</param>
/// <param name="Url">Its address on the site, with the leading slash (<see cref="ContentEntry.UrlOf"/>).</param>
/// <param name="Title">The title of the version online, not of a draft waiting for approval.</param>
/// <param name="OwnerDepartment">Whose it is.</param>
/// <param name="Visibility">Who reads it.</param>
public sealed record SiteContent(
    long Id,
    ContentKind Kind,
    string Url,
    Localized<string> Title,
    Department OwnerDepartment,
    Visibility Visibility);

/// <summary>
/// What of the content the site shows whoever is asking, unpaged: the one read behind the addresses the writer may link to
/// (<c>GET /api/content/addresses</c>, note 2026-11-04-gli-indirizzi-del-sito-che-si-possono-leggere) and behind the map of
/// the site (the <c>siteMap</c> block, design <c>13-design-struttura-del-sito.md</c> §5.1). Until the map each wrote its own.
/// <para>On <c>database.Contents</c> and never on the back office's source: the query filter (<c>VisibilityQueryFilter</c>)
/// answers with the rows whoever runs the query may read, and only those with a version online. The title is that version's,
/// as the tree of the site reads it (the review of #493): a visitor is shown what was published (note
/// 2026-10-21-il-pubblico-legge-la-versione).</para>
/// <para>Unpaged on purpose: it is the site a reader may already open page by page, and an address left out is an address the
/// map does not draw and the instructions for Claude cannot use. A division has a few hundred of them.</para>
/// </summary>
public static class SiteContents
{
    /// <summary>The rows of these kinds the site shows whoever runs the query on <paramref name="database"/>, by address.</summary>
    public static async Task<IReadOnlyList<SiteContent>> ReadAsync(
        HubDbContext database,
        IReadOnlyCollection<ContentKind> kinds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(kinds);

        var wanted = kinds.ToList();

        var rows = await (
                from row in database.Contents.AsNoTracking()
                where !row.IsTemplate && wanted.Contains(row.Kind)
                join version in database.ContentVersions.AsNoTracking() on row.PublishedVersionId equals (long?)version.Id
                select new { row.Id, row.Kind, row.Slug, row.ParentPath, version.Title, row.OwnerDepartment, row.Visibility })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .Select(row => new SiteContent(
                    row.Id,
                    row.Kind,
                    ContentEntry.UrlOf(row.Kind, row.Slug, row.ParentPath, row.OwnerDepartment),
                    row.Title,
                    row.OwnerDepartment,
                    row.Visibility))
                .OrderBy(content => content.Kind)
                .ThenBy(content => content.Url, StringComparer.Ordinal),
        ];
    }
}
