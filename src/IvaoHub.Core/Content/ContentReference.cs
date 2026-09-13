using System.Globalization;
using System.Text.Json.Nodes;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>What a published page points at, in the index.</summary>
public enum ContentReferenceKind
{
    /// <summary>A collection a data block lists: <c>Document:AOD:guides</c>.</summary>
    Collection,

    /// <summary>A file of the library the page shows: its identifier, as text.</summary>
    Media,
}

/// <summary>
/// One line of the index of what published pages point at (G20, note
/// 2026-09-13-contenuti-centralizzati §3.3–3.4): the collections their blocks list and the files they
/// show. It answers "where does this document appear?", "which pages use this collection?" and "may
/// this file be deleted?" without reading a body.
/// <para>It is <b>derived</b>: written by publication for the version it publishes, in the same
/// transaction, and never by anybody else. A page published again replaces its lines; a page deleted
/// takes them with it. Nothing reads a draft from here — a draft can still change.</para>
/// <para>Not owned, not visible, not audited: it is a projection of rows that are, and every question
/// asked of it goes back to those rows for what the reader may see.</para>
/// </summary>
public sealed class ContentReference
{
    public long Id { get; set; }

    public long ContentId { get; set; }

    /// <summary>The published version the line was read from.</summary>
    public long VersionId { get; set; }

    public ContentVersion? Version { get; set; }

    public ContentReferenceKind Kind { get; set; }

    public string Target { get; set; } = string.Empty;
}

/// <summary>A published page, as a list of where something appears names it.</summary>
public sealed record ContentAppearanceDto(long Id, string Path, Department OwnerDepartment, Localized<string> Title);

/// <summary>Writing and asking the index of <see cref="ContentReference"/>.</summary>
public sealed class ContentReferenceIndex(
    HubDbContext database,
    BlockDocumentWalker walker,
    BlockRegistry blocks,
    DataBlockProviders providers)
{
    /// <summary>The longest target the column holds.</summary>
    public const int MaxTargetLength = 128;

    /// <summary>How the index names a collection: kind, department (<c>*</c> for any) and key.</summary>
    public static string CollectionTarget(ContentKind kind, Department? department, string key) =>
        $"{kind}:{department?.ToString() ?? "*"}:{key}";

    /// <summary>
    /// Replaces the lines of a row with those of the version just published. Called by publication
    /// before it saves, so the lines land in its transaction.
    /// </summary>
    public async Task RecordAsync(ContentEntry content, ContentVersion version, JsonNode body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(version);

        // Loaded and removed rather than deleted in bulk: every write of the hub goes through the
        // interceptor, and a page has a handful of lines.
        database.ContentReferences.RemoveRange(await database.ContentReferences
            .Where(reference => reference.ContentId == content.Id)
            .ToListAsync(cancellationToken));

        foreach (var (kind, target) in Read(content, body).Distinct())
        {
            database.ContentReferences.Add(new ContentReference
            {
                ContentId = content.Id,
                Version = version,
                Kind = kind,
                Target = target,
            });
        }
    }

    /// <summary>
    /// The published pages that list one of the collections of this row. Titles and addresses only,
    /// read past the query filter: that a page of another department lists this document is a fact
    /// about this document, and the page is public or will be read by its own department anyway.
    /// </summary>
    public async Task<IReadOnlyList<ContentAppearanceDto>> AppearancesAsync(ContentEntry content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var targets = content.Collections
            .SelectMany(key => new[]
            {
                CollectionTarget(content.Kind, content.OwnerDepartment, key),
                CollectionTarget(content.Kind, department: null, key),
            })
            .ToArray();

        return await PagesAsync(ContentReferenceKind.Collection, targets, cancellationToken);
    }

    /// <summary>The published pages that list a collection of the vocabulary.</summary>
    public Task<IReadOnlyList<ContentAppearanceDto>> UsesOfCollectionAsync(ContentCategory collection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);

        return PagesAsync(
            ContentReferenceKind.Collection,
            [
                CollectionTarget(collection.Kind, collection.OwnerDepartment, collection.Key),
                CollectionTarget(collection.Kind, department: null, collection.Key),
            ],
            cancellationToken);
    }

    /// <summary>The published pages that show a file.</summary>
    public Task<IReadOnlyList<ContentAppearanceDto>> UsesOfMediaAsync(long mediaId, CancellationToken cancellationToken) =>
        PagesAsync(ContentReferenceKind.Media, [MediaTarget(mediaId)], cancellationToken);

    /// <summary>The files a published version shows, by identifier.</summary>
    public async Task<IReadOnlyList<long>> MediaOfVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        var targets = await database.ContentReferences
            .AsNoTracking()
            .Where(reference => reference.VersionId == versionId && reference.Kind == ContentReferenceKind.Media)
            .Select(reference => reference.Target)
            .ToListAsync(cancellationToken);

        return [.. targets.Select(target => long.Parse(target, CultureInfo.InvariantCulture))];
    }

    private async Task<IReadOnlyList<ContentAppearanceDto>> PagesAsync(
        ContentReferenceKind kind,
        string[] targets,
        CancellationToken cancellationToken)
    {
        if (targets.Length == 0)
        {
            return [];
        }

        var ids = await database.ContentReferences
            .AsNoTracking()
            .Where(reference => reference.Kind == kind && targets.Contains(reference.Target))
            .Select(reference => reference.ContentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var pages = await CrudSource.BackOffice<ContentEntry>(database)
            .AsNoTracking()
            .Where(row => ids.Contains(row.Id) && row.PublishedVersionId != null)
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);

        return [.. pages.Select(row => new ContentAppearanceDto(row.Id, row.Path, row.OwnerDepartment, row.Title))];
    }

    private IEnumerable<(ContentReferenceKind Kind, string Target)> Read(ContentEntry content, JsonNode body)
    {
        foreach (var block in walker.EnumerateBlocks(body))
        {
            if (blocks.Find(block.Type) is { } descriptor && providers.For(descriptor) is { } provider)
            {
                foreach (var collection in provider.Collections(block.Node["props"]))
                {
                    if (collection.Length <= MaxTargetLength)
                    {
                        yield return (ContentReferenceKind.Collection, collection);
                    }
                }
            }
        }

        // The files the body shows, plus the two a row names in a column of its own: the same three
        // ways publication already checks against the visibility of the page.
        foreach (var media in walker.MediaReferences(body))
        {
            yield return (ContentReferenceKind.Media, MediaTarget(media.Id));
        }

        if (content.CoverMediaId is { } cover)
        {
            yield return (ContentReferenceKind.Media, MediaTarget(cover));
        }

        if (content.FileMediaId is { } file)
        {
            yield return (ContentReferenceKind.Media, MediaTarget(file));
        }
    }

    private static string MediaTarget(long id) => id.ToString(CultureInfo.InvariantCulture);
}
