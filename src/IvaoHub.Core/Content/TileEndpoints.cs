using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace IvaoHub.Core.Content;

/// <summary>
/// The base map, served by the hub itself (note 2026-09-15-la-mappa): one PMTiles archive of the world, read by the
/// browser with <c>Range</c> requests a few kilobytes at a time. No tile provider, no key, no quota, and nobody but this
/// server sees who is looking at a map.
/// <para>⚠️ The file is <b>not</b> in the repository and not in the package: it is 179 MB of coastlines that change with
/// nothing we release, so it sits next to the uploads in the data folder and is put there once, over FTP
/// (<c>tools/basemap.mjs</c> writes the command, <c>FORKING.md</c> explains it). Without it every answer here is 404 and
/// the map draws the legs on a neutral ground — a fork that has not uploaded it yet sees a map without countries, never
/// an error.</para>
/// <para>⚠️ It is a path of the site outside <c>/api</c>, so it is in <c>web/backendPaths.ts</c> as well: the third time
/// that list has had to learn a prefix (<c>/media</c>, <c>/embed</c>), and the first where it was not found by Carmine
/// in the browser.</para>
/// </summary>
public static class TileEndpoints
{
    /// <summary>The prefix, for whoever has to keep the single page application off it.</summary>
    public const string Prefix = "/tiles";

    /// <summary>The one file the map asks for, and the name a fork uploads it under.</summary>
    public const string BaseMapName = "basemap.pmtiles";

    public const string Pattern = $"{Prefix}/{{name}}";

    /// <summary>What a PMTiles archive is served as; the client reads it with ranges and never all at once.</summary>
    private const string ContentType = "application/vnd.pmtiles";

    public static void MapTileEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, Serve)
            .WithName("BaseMap")
            .ExcludeFromDescription()
            .AllowAnonymous();
    }

    /// <summary>
    /// The archive, straight from Kestrel with ranges and a strong tag. Not compressed on purpose: a compressed answer
    /// makes Cloudflare ignore <c>Range</c>, and a client that has to download 179 MB to draw Italy is a client that
    /// draws nothing.
    /// </summary>
    private static IResult Serve(string name, HubPaths paths, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        // One directory, one kind of file, and a name that is a name: no path, no traversal, no listing.
        if (name != BaseMapName || Path.GetFileName(name) != name)
        {
            return Results.NotFound();
        }

        var file = new FileInfo(Path.Combine(paths.Tiles, name));
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        // The archive changes when a fork uploads a newer build of the world, which is rare and never
        // silent: the length and the instant of the file are what says "these bytes", cheaply.
        var tag = Fingerprint(file);

        // A month, revalidated: the world does not move, but neither does anybody want to wait a year
        // for a fork's second upload to be seen.
        http.Response.Headers.CacheControl = "public, max-age=2592000, must-revalidate";

        return Results.File(
            file.FullName,
            ContentType,
            lastModified: file.LastWriteTimeUtc,
            entityTag: new EntityTagHeaderValue($"\"{tag}\""),
            enableRangeProcessing: true);
    }

    private static string Fingerprint(FileInfo file)
    {
        var written = string.Create(
            CultureInfo.InvariantCulture,
            $"{file.Length}-{file.LastWriteTimeUtc.Ticks}");

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(written)))[..16];
    }
}
