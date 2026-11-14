using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// Who may read what an address of the site leads to. A screen anybody may be sent to answers
/// <see cref="Visibility.Public"/>; <see cref="Department"/> only means something next to
/// <see cref="Visibility.Department"/>.
/// </summary>
public sealed record SiteDestination(Visibility Visibility, Department Department);

/// <summary>
/// Whether an address leads somewhere this site owns (design of the editor, phase 3, section 5.9): the one answer,
/// for the menu and for publication. The two ask it differently, on purpose. A menu entry is written before its page
/// comes out, so a draft counts, and it stays a closed set. A page going out is what visitors click today, so only what
/// is online counts, and with it everything else the application answers for.
/// <para>Read past the query filter: the question is what the destination is, not whether whoever is writing may see
/// it. A link of another department is a perfectly good destination.</para>
/// </summary>
public sealed class SiteDestinations(HubDbContext database, ContentAddresses addresses)
{
    /// <summary>
    /// The addresses of this application that are not rows: they are screens of the single page
    /// application, so no table can be asked about them.
    /// <para>⚠️ The other half is <c>SITE_SCREENS</c> in
    /// <c>web/src/features/content/siteAddresses.ts</c>, and the two agree **by hand** — a
    /// route of the client is not something the contract can carry. The integration test that posts
    /// a screen the server does not know is what keeps them agreeing, exactly as it does for the
    /// backgrounds of a section.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> Screens =
        ["/", "/calendar", "/news", "/documents", "/search", "/contact"];

    /// <summary>The back office: a link to it from a page for everybody is the case the check exists for.</summary>
    private const string StaffSegment = "staff";

    /// <summary>
    /// The schemes an address written into the content may name: the web, a mail, a phone. Anything else
    /// (<c>javascript:</c>, <c>data:</c>, a scheme nobody knows) is not an address somebody follows.
    /// </summary>
    private static readonly string[] FollowableSchemes =
        [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto, "tel"];

    /// <summary>
    /// Whether an address written into the content may be drawn as a link: nothing yet, a path of this site, an anchor
    /// of the page, or a whole address of the web, a mail or a phone (#321, point 5). Everything else is refused when it
    /// is written, rather than sanitised later, as a link of the library and a menu entry already are: a
    /// <c>javascript:</c> address, and a path that leaves the site while looking like one (<c>//elsewhere.example</c>,
    /// <c>/\elsewhere.example</c>, or a path with a tab or a line break in it, which the browser drops).
    /// <para>The one rule, for every <c>href</c> of a body, its Markdown links and the address of an entry of the
    /// calendar. Whether an address of this site arrives is <see cref="ReachableAsync"/>'s, at publication.</para>
    /// </summary>
    public static bool IsFollowable(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return true;
        }

        if (address.Any(char.IsControl))
        {
            return false;
        }

        if (address.StartsWith('/'))
        {
            return address.Length == 1 || (address[1] != '/' && address[1] != '\\');
        }

        return address.StartsWith('#')
            || (Uri.TryCreate(address, UriKind.Absolute, out var parsed)
                && FollowableSchemes.Contains(parsed.Scheme, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The address as this site reads it, without what the router does not look at (<c>?</c>, <c>#</c>, a last slash);
    /// null when it is not an address of this site. <c>//elsewhere.example</c> leaves the site while looking like a path.
    /// </summary>
    public static string? OnThisSite(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)
            || !address.StartsWith('/')
            || address.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        var end = address.IndexOfAny(['?', '#']);
        var path = (end < 0 ? address : address[..end]).TrimEnd('/');

        return path.Length == 0 ? "/" : path;
    }

    /// <summary>
    /// Where a menu entry may lead, and it is a **closed** set (decided by Carmine on 8 September
    /// 2026, while running the demo): a page of this site, a screen of the application, or a link
    /// of the library — and nothing else.
    /// <para>The point of it is not the menu: it is that <b>every address that leaves this site
    /// lives in one table</b>. A menu that could carry any URL is a site with addresses scattered
    /// through it, each of them a thing nobody maintains; with this rule, changing where the forum
    /// lives is one row of <c>cms_links</c> and the menu follows.</para>
    /// <para>A page that is still a draft counts, on purpose: writing the entry before publishing
    /// the page is how a menu is actually built, and the entry can wait, switched off, until the
    /// page is public.</para>
    /// </summary>
    public async Task<bool> MenuMayLeadToAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (Screens.Contains(path, StringComparer.Ordinal))
        {
            return true;
        }

        if (path.StartsWith('/'))
        {
            // The whole address, since pages sit under pages (note 2026-09-13-contenuti-centralizzati).
            var address = path[1..];

            return await CrudSource.BackOffice<ContentEntry>(database)
                .AnyAsync(
                    row => !row.IsTemplate
                        && row.Kind == ContentKind.Page
                        && EF.Property<string>(row, ContentAddresses.StoredPath) == address,
                    cancellationToken);
        }

        return await CrudSource.BackOffice<Link>(database)
            .AnyAsync(row => row.IsActive && row.Url == path, cancellationToken);
    }

    /// <summary>
    /// What each address of the site leads to for a page about to go out, and nothing for one that leads nowhere: the
    /// addresses come from <see cref="OnThisSite"/>.
    /// <para>A screen; a first segment the application or a module answers for (<c>/tours/{slug}</c>: what is behind it
    /// is the module's, and the core cannot ask); a page, news item or document online; or an address a page online has
    /// left behind, which still arrives. <paramref name="self"/> is the row being published, which is online by the time
    /// anybody follows a link to itself.</para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, SiteDestination>> ReachableAsync(
        IReadOnlyCollection<string> paths,
        long self,
        CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, SiteDestination>(StringComparer.OrdinalIgnoreCase);
        var reserved = addresses.AllReservedSegments;
        var pages = new List<string>();
        var news = new List<string>();
        var documents = new List<string>();

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var segments = path[1..].Split('/');

            if (Screens.Contains(path, StringComparer.Ordinal))
            {
                found[path] = new SiteDestination(Visibility.Public, default);
            }
            else if (segments is ["news", var newsSlug])
            {
                news.Add(newsSlug);
            }
            else if (segments is ["documents", var documentSlug])
            {
                documents.Add(documentSlug);
            }
            else if (segments[0] is "news" or "documents")
            {
                // Nothing sits under a news item or a document.
            }
            else if (reserved.Contains(segments[0], StringComparer.OrdinalIgnoreCase))
            {
                var staff = string.Equals(segments[0], StaffSegment, StringComparison.OrdinalIgnoreCase);
                found[path] = new SiteDestination(staff ? Visibility.Staff : Visibility.Public, default);
            }
            else
            {
                pages.Add(path[1..]);
            }
        }

        if (pages.Count + news.Count + documents.Count == 0)
        {
            return found;
        }

        var rows = await CrudSource.BackOffice<ContentEntry>(database)
            .AsNoTracking()
            .Where(row => !row.IsTemplate
                && (row.PublishedVersionId != null || row.Id == self)
                && ((row.Kind == ContentKind.Page && pages.Contains(EF.Property<string>(row, ContentAddresses.StoredPath)))
                    || (row.Kind == ContentKind.News && news.Contains(row.Slug))
                    || (row.Kind == ContentKind.Document && documents.Contains(row.Slug))))
            .Select(row => new { row.Kind, row.Slug, row.ParentPath, row.OwnerDepartment, row.Visibility })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            found[ContentEntry.UrlOf(row.Kind, row.Slug, row.ParentPath, row.OwnerDepartment)] =
                new SiteDestination(row.Visibility, row.OwnerDepartment);
        }

        await AddLeftBehindAsync(pages.Where(page => !found.ContainsKey($"/{page}")).ToList(), found, cancellationToken);

        return found;
    }

    /// <summary>
    /// A page that moved keeps its old address and sends a visitor on, so a link to it arrives. Few pages ever move, so
    /// the rows with any history are few.
    /// </summary>
    private async Task AddLeftBehindAsync(
        IReadOnlyList<string> pages,
        Dictionary<string, SiteDestination> found,
        CancellationToken cancellationToken)
    {
        if (pages.Count == 0)
        {
            return;
        }

        var moved = await CrudSource.BackOffice<ContentEntry>(database)
            .AsNoTracking()
            .Where(row => row.Kind == ContentKind.Page
                && !row.IsTemplate
                && row.PublishedVersionId != null
                && row.PreviousPathsJson != null)
            .Select(row => new { row.PreviousPathsJson, row.OwnerDepartment, row.Visibility })
            .ToListAsync(cancellationToken);

        foreach (var row in moved)
        {
            var left = JsonSerializer.Deserialize<List<string>>(row.PreviousPathsJson!) ?? [];

            foreach (var page in pages.Where(page => left.Contains(page, StringComparer.Ordinal)))
            {
                found[$"/{page}"] = new SiteDestination(row.Visibility, row.OwnerDepartment);
            }
        }
    }
}
