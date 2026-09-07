using System.Globalization;
using System.Text;
using System.Xml;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The two files a search engine asks for. They are the whole of the server's half of SEO, and that
/// is deliberate: there is no prerendering here and there will not be (plan section 16.11), so what
/// this hub owes a crawler is an honest list of its addresses and a word about where not to go.
/// <para>⚠️ Both paths are excluded from the fallback of the single page application. Without that
/// the application would answer them with <c>index.html</c> and a crawler would read a page of
/// JavaScript where it asked for XML.</para>
/// </summary>
public static class SeoEndpoints
{
    public const string SitemapPattern = "/sitemap.xml";

    public const string RobotsPattern = "/robots.txt";

    /// <summary>
    /// What a crawler is asked to stay out of: the back office, a member's own pages, and the API.
    /// Not a security measure — none of them answer to somebody who is not signed in anyway — but a
    /// crawl budget spent on a login redirect is a crawl budget not spent on the site.
    /// </summary>
    private static readonly string[] Disallowed = ["/staff", "/me", "/api"];

    public static void MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(SitemapPattern, SitemapAsync).ExcludeFromDescription().AllowAnonymous();
        app.MapGet(RobotsPattern, Robots).ExcludeFromDescription().AllowAnonymous();
    }

    /// <summary>
    /// Every address a visitor can read, and nothing else.
    /// <para>Which rows those are is not decided here: the query runs as the caller, who is nobody,
    /// so the global query filter has already dropped every draft and everything not public. A
    /// sitemap that listed a members' page would be a sitemap that leaks the existence of one, and
    /// the reason it cannot is the same rule that stops the page itself being served.</para>
    /// </summary>
    private static async Task<IResult> SitemapAsync(
        HubDbContext database,
        IOptions<DivisionOptions> division,
        HttpContext http)
    {
        var rows = await database.Contents
            .AsNoTracking()
            .Where(content => !content.IsTemplate)
            .OrderBy(content => content.Slug)
            .Select(content => new { content.Kind, content.Slug, content.OwnerDepartment, content.PublishedAt })
            .ToListAsync(http.RequestAborted);

        var origin = $"https://{division.Value.Domain}";

        // ⚠️ A writer over a `StringBuilder` declares `encoding="utf-16"` whatever the settings say,
        // because that is what a .NET string is. The declaration is the one thing a crawler reads
        // before anything else, so the buffer says out loud what it will be sent as.
        using var xml = new Utf8StringWriter();

        using (var writer = XmlWriter.Create(
            xml,
            new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8, Async = false }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            // The front page, which is an address of the application rather than a slug of its own.
            WriteUrl(writer, origin + "/", null);

            foreach (var row in rows)
            {
                // The address is the entity's, so the sitemap and the search index cannot point at
                // two different places for one row.
                var url = new ContentEntry
                {
                    Kind = row.Kind,
                    Slug = row.Slug,
                    OwnerDepartment = row.OwnerDepartment,
                }.Url;

                WriteUrl(writer, origin + url, row.PublishedAt);
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return Results.Text(xml.ToString(), "application/xml", Encoding.UTF8);
    }

    /// <summary>A string buffer that says it holds UTF-8, so the declaration written into it agrees
    /// with the bytes the response is actually sent as.</summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    private static void WriteUrl(XmlWriter writer, string location, DateTime? lastModified)
    {
        writer.WriteStartElement("url");
        writer.WriteElementString("loc", location);

        if (lastModified is { } moment)
        {
            writer.WriteElementString(
                "lastmod",
                DateTime.SpecifyKind(moment, DateTimeKind.Utc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        writer.WriteEndElement();
    }

    private static IResult Robots(IOptions<DivisionOptions> division)
    {
        var text = new StringBuilder("User-agent: *\n");

        foreach (var path in Disallowed)
        {
            text.Append("Disallow: ").Append(path).Append('\n');
        }

        text.Append("\nSitemap: https://").Append(division.Value.Domain).Append(SitemapPattern).Append('\n');

        return Results.Text(text.ToString(), "text/plain", Encoding.UTF8);
    }
}
