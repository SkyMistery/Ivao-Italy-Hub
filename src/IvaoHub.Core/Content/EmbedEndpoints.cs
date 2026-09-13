using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// The frame of an interactive block: one document, built from the shell and the source the block
/// carries, served with a policy of its own (12 September 2026,
/// <c>decisions/2026-09-12-il-blocco-interattivo.md</c>).
///
/// <para>⚠️ This is the second endpoint of M1 written by hand, and the PR says so. It exists because
/// of the policy: a <c>srcdoc</c> frame inherits the policy of the page that holds it, so running a
/// coordinator's script inside one would mean opening <c>script-src</c> on the whole site. A document
/// served from here carries its **own** headers — <c>default-src 'none'</c>, no origin, no network —
/// and the page around it keeps the strict policy of
/// <c>decisions/2026-09-12-gli-header-di-sicurezza.md</c>.</para>
///
/// <para>What it knows about a block is <b>one field of the envelope</b>: <c>source</c>, beside
/// <c>renderMode</c> and <c>frozen</c>. It never looks inside <c>props</c>, which stays opaque here
/// as everywhere (plan section 16.5).</para>
/// </summary>
public static class EmbedEndpoints
{
    /// <summary>
    /// <c>/embed/{content}/{version}/{block}</c>, where the version is the number of a published
    /// version or the word below. The version is in the address so that what it answers can never
    /// change: a published version is immutable, so the answer is cacheable for a year.
    /// </summary>
    public const string Pattern = "/embed/{contentId:long}/{version}/{blockId}";

    /// <summary>The draft of the row, which only somebody who may edit it is shown.</summary>
    public const string DraftVersion = "draft";

    private const string FramePolicy =
        "default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; "
        + "frame-ancestors 'self'; sandbox allow-scripts";

    /// <summary>Where the editor's "download the guidelines" link points.</summary>
    public const string GuidelinesPattern = "/embed/guidelines";

    /// <summary>Where the editor's "download the local preview" link points.</summary>
    public const string PreviewPattern = "/embed/preview";

    private static readonly string Shell = Read("EmbedShell.html");

    /// <summary>
    /// The guidelines, with the shell quoted inside them. ⚠️ Composed and not written twice: what
    /// the document promises about tokens, `HUB` and the policy is the file that keeps the promise,
    /// injected at the end, so the two cannot drift apart the way a document and its subject
    /// usually do.
    /// </summary>
    private static readonly string Guidelines = Read("EmbedGuidelines.md")
        .Replace("{{shell}}", Read("EmbedShell.html"), StringComparison.Ordinal);

    /// <summary>
    /// The local preview, with the shell put inside it — for whoever writes an animation on their own
    /// computer, before it goes into a draft (12 September 2026). The shell goes in as a **JSON
    /// string**, whose default encoding escapes <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c>: the shell
    /// carries script elements of its own, and a closing tag of one of them written raw into the
    /// preview's script would end that script halfway through.
    /// </summary>
    private static readonly byte[] Preview = System.Text.Encoding.UTF8.GetBytes(
        Read("EmbedPreview.html")
            .Replace("{{shellJson}}", JsonSerializer.Serialize(Shell), StringComparison.Ordinal));

    public static void MapEmbedEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, ServeAsync)
            .WithName("EmbedFrame")
            .WithTags(CorePermissions.ContentArea)
            .ExcludeFromDescription()
            .AllowAnonymous();

        // What whoever writes an animation is handed, and what an assistant is given to read. Behind
        // the permission that lets somebody add one: it is not a secret, but it is not a page of the
        // site either.
        app.MapGet(GuidelinesPattern, () => Results.Text(Guidelines, "text/markdown; charset=utf-8"))
            .WithName("EmbedGuidelines")
            .WithTags(CorePermissions.ContentArea)
            .ExcludeFromDescription()
            .RequireAuthorization(CorePermissions.ContentEmbedCode);

        // ⚠️ A **download** and never a page of this site: `Results.File` with a name sends it as an
        // attachment, and if somebody opened it here anyway the site's own policy (`script-src
        // 'self'`) would refuse its inline script. It is meant to be opened from a disk, where it
        // runs a pasted fragment in a sandboxed frame with the shell's own policy — which is the only
        // place running a pasted fragment is harmless.
        app.MapGet(PreviewPattern, () => Results.File(Preview, "text/html; charset=utf-8", "interactive-preview.html"))
            .WithName("EmbedPreview")
            .WithTags(CorePermissions.ContentArea)
            .ExcludeFromDescription()
            .RequireAuthorization(CorePermissions.ContentEmbedCode);
    }

    private static async Task<IResult> ServeAsync(
        long contentId,
        string version,
        string blockId,
        string? lang,
        HubDbContext database,
        IAuthorizationService authorization,
        HttpContext http)
    {
        var draft = string.Equals(version, DraftVersion, StringComparison.Ordinal);

        // ⚠️ Two reads, and the difference is the whole of who may see what.
        //
        // **Published**: through the context, so the global query filter answers first — it hides a
        // row that is not published and a row whose visibility excludes whoever is asking, which is
        // why a members-only page cannot leak its animation to somebody who could not read the page.
        //
        // **Draft**: `CrudSource.BackOffice`, which is how everything in this hub reads a row the way
        // the back office reads it. The filter hides every unpublished row from everybody, editors
        // included — a page being written for the first time has no published version at all, and its
        // author has to see the frame they are composing. What takes the filter's place is the
        // authorization handler below, asked the question the back office asks of every row.
        //
        // ⚠️ And it is `CrudSource` rather than an `IgnoreQueryFilters` written here, because an
        // architecture test says that call belongs to one folder — which is how a public endpoint
        // never gets written with the filters quietly switched off. CI said so, and it was right to.
        var content = await (draft ? CrudSource.BackOffice<ContentEntry>(database) : database.Contents)
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == contentId, http.RequestAborted);

        if (content is null)
        {
            return Results.NotFound();
        }

        string? body;

        if (draft)
        {
            // ⚠️ The one authorization handler, asked the one question. Nothing here decides who may
            // read a draft: that answer lives in a single place and this endpoint is a caller of it
            // like every screen of the back office.
            if (!(await authorization.AuthorizeAsync(http.User, content, CorePermissions.ContentEdit)).Succeeded)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            body = content.BodyJson;
        }
        else if (int.TryParse(version, out var number))
        {
            body = await database.ContentVersions
                .AsNoTracking()
                .Where(row => row.ContentId == contentId && row.Version == number)
                .Select(row => row.BodyJson)
                .FirstOrDefaultAsync(http.RequestAborted);
        }
        else
        {
            return Results.NotFound();
        }

        if (body is null || SourceOf(body, blockId) is not { } source)
        {
            return Results.NotFound();
        }

        // A published version never changes, so it is cacheable for as long as a browser will keep
        // it; a draft changes every ten seconds and is nobody else's business.
        http.Response.Headers.CacheControl = draft
            ? "no-store"
            : "public, max-age=31536000, immutable";

        // Its own policy, and the reason the frame is an endpoint at all. ⚠️ And `X-Frame-Options`
        // goes: the page sends `DENY` on everything, which would stop our own page from framing
        // this. `frame-ancestors 'self'` says the same thing in the language that allows it.
        http.Response.Headers.ContentSecurityPolicy = FramePolicy;
        http.Response.Headers.Remove("X-Frame-Options");

        return Results.Content(Compose(source, lang, content), "text/html; charset=utf-8");
    }

    /// <summary>
    /// The source of one block of a body, found by identifier and read off the envelope.
    /// <para>The walker is what enumerates, so a block nested four sections deep is found the same
    /// way the search index finds its text: nothing here knows what a body looks like.</para>
    /// </summary>
    private static string? SourceOf(string bodyJson, string blockId)
    {
        var walker = new BlockDocumentWalker([]);

        foreach (var block in walker.EnumerateBlocks(JsonNode.Parse(bodyJson)))
        {
            if (string.Equals(block.Id, blockId, StringComparison.Ordinal))
            {
                return (block.Node["source"] as JsonValue)?.GetValue<string>();
            }
        }

        return null;
    }

    private static string Compose(string source, string? lang, ContentEntry content)
    {
        // The language is asked for by the page, because the page is the only one that knows which
        // of the division's languages the reader is reading in. Two letters at most, and a letter is
        // all it is allowed to be: it lands in an attribute of the document.
        var locale = lang is not null && lang.Length <= 5 && lang.All(char.IsLetter)
            ? lang
            : "en";

        return Shell
            .Replace("{{lang}}", locale, StringComparison.Ordinal)
            .Replace("{{title}}", Escape(content.Slug), StringComparison.Ordinal)
            .Replace("{{source}}", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// The only value of the three that is not already a letter or the author's own markup, so the
    /// only one that has to be made safe for an element that holds text.
    /// </summary>
    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string Read(string file)
    {
        // Embedded resources and not files: there is no way to deploy the application without them,
        // and no way for an installation to hold a shell that does not match the code that fills it.
        var assembly = typeof(EmbedEndpoints).GetTypeInfo().Assembly;
        var name = $"IvaoHub.Core.Content.{file}";

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing from the package: {name}.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
