using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>What an address looks like to the form that is composing it.</summary>
public enum ContentAddressState
{
    /// <summary>Nobody has it, and it may be used.</summary>
    Free,

    /// <summary>Another row of the same kind has it. <c>Suggestion</c> is the first free one.</summary>
    Taken,

    /// <summary>The application already answers for it: <c>/news</c>, <c>/staff</c>, <c>/api</c>.</summary>
    Reserved,

    /// <summary>A fourth level, or the pages under this one would end up at a fourth.</summary>
    TooDeep,

    /// <summary>The parent chosen is not a page this one can sit under.</summary>
    InvalidParent,

    /// <summary>At the top of the site, which needs <c>Content.Approve</c>.</summary>
    TopLevelNotAllowed,
}

/// <summary>The answer to "where would this page be, and may it be there?".</summary>
/// <param name="Path">The whole address, with its leading slash.</param>
/// <param name="State">Whether it may be used.</param>
/// <param name="Suggestion">The first free slug, when the one asked is taken.</param>
public sealed record ContentAddressDto(string Path, ContentAddressState State, string? Suggestion);

/// <summary>
/// The address of a page (note 2026-09-13-contenuti-centralizzati, 3.7): a page sits under another
/// page, up to three levels; its last segment is its slug; the first segment may not be one the
/// application already answers for; the top of the site is for whoever holds
/// <see cref="CorePermissions.ContentApprove"/>; and a published page that moves leaves its old
/// address behind, so a visitor arriving there is sent on.
/// <para>One class, used by the CRUD engine before a write (<c>CrudOptions.BeforeSave</c>), by
/// "new from a template", and by the check the form asks while somebody is typing — so the form
/// and the save cannot give two answers.</para>
/// </summary>
public sealed class ContentAddresses(HubDbContext database, ICurrentUser currentUser)
{
    /// <summary>Levels of the address of a page: <c>/a/b/c</c>.</summary>
    public const int MaxDepth = 3;

    /// <summary>The parent's address: two slugs and a slash.</summary>
    public const int MaxParentPathLength = (ContentWriteDtoValidator.MaxSlugLength * (MaxDepth - 1)) + (MaxDepth - 2);

    /// <summary>A whole address: three slugs and two slashes.</summary>
    public const int MaxPathLength = (ContentWriteDtoValidator.MaxSlugLength * MaxDepth) + (MaxDepth - 1);

    /// <summary>The shadow property of the stored, indexed column the database computes.</summary>
    public const string StoredPath = "StoredPath";

    /// <summary>
    /// The first segments the application answers for itself, so no page may take them. Read by a
    /// test of the front end, which compares them with the router's own top level routes and with
    /// `BACKEND_PATHS`: a route added on either side and forgotten here fails a build, not a visitor.
    /// </summary>
    public static readonly IReadOnlyList<string> ReservedSegments =
    [
        "api", "auth", "health", "media", "embed", "openapi", "scalar", "sitemap.xml", "robots.txt",
        "news", "documents", "calendar", "search", "forbidden", "login-error", "me", "contact", "staff",
    ];

    /// <summary>
    /// Everything <c>BeforeSave</c> does for a content row: refuses an address that may not be
    /// used, writes <see cref="ContentEntry.ParentPath"/>, and moves the pages under a page that
    /// moved, leaving the old addresses of the published ones behind.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> PrepareAsync(
        ContentEntry content,
        bool isNew,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var entry = database.Entry(content);
        var oldPath = isNew ? null : OriginalPath(entry);
        var oldParentId = isNew ? null : entry.Property(row => row.ParentId).OriginalValue;

        var answer = await CheckAsync(
            content.Kind,
            content.IsTemplate,
            content.OwnerDepartment,
            content.ParentId,
            content.Slug,
            isNew ? null : content.Id,
            asksForTopLevel: content.ParentId is null && (isNew || oldParentId is not null),
            cancellationToken);

        if (answer.Error is { } error)
        {
            return error;
        }

        content.ParentPath = answer.ParentPath;

        if (oldPath is null || oldPath == content.Path || content.Kind != ContentKind.Page || content.IsTemplate)
        {
            return null;
        }

        // It moved. The pages under it move with it, and every published one of them — this one
        // included — keeps its old address, so a link somebody saved still arrives.
        var below = await CrudSource.BackOffice<ContentEntry>(database)
            .Where(row => row.Kind == ContentKind.Page
                && !row.IsTemplate
                && row.ParentPath != null
                && (row.ParentPath == oldPath || row.ParentPath.StartsWith(oldPath + "/")))
            .ToListAsync(cancellationToken);

        Remember(content, oldPath);

        foreach (var row in below)
        {
            var before = row.Path;
            row.ParentPath = content.Path + row.ParentPath![oldPath.Length..];
            Remember(row, before);
        }

        return null;
    }

    /// <summary>The check, without writing anything: what the form asks while somebody types.</summary>
    public async Task<ContentAddressDto> DescribeAsync(
        ContentKind kind,
        long? parentId,
        string slug,
        long? id,
        Division.Department department,
        CancellationToken cancellationToken)
    {
        var existing = id is null
            ? null
            : await CrudSource.BackOffice<ContentEntry>(database).AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

        var answer = await CheckAsync(
            kind,
            isTemplate: existing?.IsTemplate ?? false,
            existing?.OwnerDepartment ?? department,
            parentId,
            slug,
            id,
            asksForTopLevel: parentId is null && (existing is null || existing.ParentId is not null),
            cancellationToken);

        var path = "/" + (answer.ParentPath is null ? slug : $"{answer.ParentPath}/{slug}");
        return new ContentAddressDto(path, answer.State, answer.Suggestion);
    }

    private async Task<Checked> CheckAsync(
        ContentKind kind,
        bool isTemplate,
        Division.Department department,
        long? parentId,
        string slug,
        long? id,
        bool asksForTopLevel,
        CancellationToken cancellationToken)
    {
        // News, documents and a department's home have an address of their kind, under no page; and a
        // template is a tool of the staff, with no place in the tree of the site.
        if (kind != ContentKind.Page || isTemplate)
        {
            return parentId is null
                ? await FreeOrTakenAsync(kind, isTemplate, parentPath: null, slug, id, cancellationToken)
                : Refuse(ContentAddressState.InvalidParent, "parentId", "errors.content.address.notAPage");
        }

        if (parentId is null)
        {
            if (ReservedSegments.Contains(slug, StringComparer.OrdinalIgnoreCase))
            {
                return Refuse(ContentAddressState.Reserved, "slug", "errors.content.address.reserved");
            }

            // A template is a tool and not an address of the site; the installation — nobody signed
            // in — seeds the pages the division is born with.
            if (asksForTopLevel
                && !isTemplate
                && currentUser.IsAuthenticated
                && !currentUser.Has(CorePermissions.ContentApprove, department))
            {
                return Refuse(ContentAddressState.TopLevelNotAllowed, "parentId", "errors.content.address.topLevel");
            }

            return await FreeOrTakenAsync(kind, isTemplate, parentPath: null, slug, id, cancellationToken);
        }

        var parent = await CrudSource.BackOffice<ContentEntry>(database)
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == parentId, cancellationToken);

        if (parent is null || parent.Kind != ContentKind.Page || parent.IsTemplate || parent.Id == id)
        {
            return Refuse(ContentAddressState.InvalidParent, "parentId", "errors.content.address.invalidParent");
        }

        // Not under itself, however far down: a page moved beneath one of its own children would be
        // an address with no beginning.
        var ownPath = id is null
            ? null
            : await CrudSource.BackOffice<ContentEntry>(database)
                .Where(row => row.Id == id)
                .Select(row => row.ParentPath == null ? row.Slug : row.ParentPath + "/" + row.Slug)
                .FirstOrDefaultAsync(cancellationToken);

        if (ownPath is not null && (parent.Path == ownPath || parent.Path.StartsWith(ownPath + "/", StringComparison.Ordinal)))
        {
            return Refuse(ContentAddressState.InvalidParent, "parentId", "errors.content.address.invalidParent");
        }

        // Three levels, counting the ones under this page too: a page with children moved one level
        // down takes its children with it.
        var depth = Segments(parent.Path) + 1;
        var deepestBelow = 0;

        if (ownPath is not null)
        {
            var below = await CrudSource.BackOffice<ContentEntry>(database)
                .Where(row => row.Kind == ContentKind.Page && !row.IsTemplate && row.ParentPath != null
                    && (row.ParentPath == ownPath || row.ParentPath.StartsWith(ownPath + "/")))
                .Select(row => row.ParentPath!)
                .ToListAsync(cancellationToken);

            deepestBelow = below.Count == 0 ? 0 : below.Max(path => Segments(path) + 1 - Segments(ownPath));
        }

        if (depth + deepestBelow > MaxDepth)
        {
            return Refuse(ContentAddressState.TooDeep, "parentId", "errors.content.address.tooDeep");
        }

        return await FreeOrTakenAsync(kind, isTemplate, parent.Path, slug, id, cancellationToken);
    }

    private async Task<Checked> FreeOrTakenAsync(
        ContentKind kind,
        bool isTemplate,
        string? parentPath,
        string slug,
        long? id,
        CancellationToken cancellationToken)
    {
        var siblings = await CrudSource.BackOffice<ContentEntry>(database)
            .Where(row => row.Kind == kind
                && row.IsTemplate == isTemplate
                && row.ParentPath == parentPath
                && row.Id != (id ?? 0)
                && (row.Slug == slug || row.Slug.StartsWith(slug + "-")))
            .Select(row => row.Slug)
            .ToListAsync(cancellationToken);

        if (!siblings.Contains(slug, StringComparer.Ordinal))
        {
            return new Checked(parentPath, ContentAddressState.Free, null, null);
        }

        var suggestion = Enumerable.Range(2, 1000)
            .Select(number => $"{slug}-{number}")
            .First(candidate => !siblings.Contains(candidate, StringComparer.Ordinal));

        return new Checked(
            parentPath,
            ContentAddressState.Taken,
            suggestion,
            Errors("slug", "errors.content.address.taken"));
    }

    private static Checked Refuse(ContentAddressState state, string field, string key) =>
        new(null, state, null, Errors(field, key));

    private static Dictionary<string, string[]> Errors(string field, string key) =>
        new(StringComparer.Ordinal) { [field] = [key] };

    private static int Segments(string path) => path.Count(character => character == '/') + 1;

    private static string OriginalPath(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ContentEntry> entry)
    {
        var parentPath = entry.Property(row => row.ParentPath).OriginalValue;
        var slug = entry.Property(row => row.Slug).OriginalValue;
        return parentPath is null ? slug : $"{parentPath}/{slug}";
    }

    /// <summary>An old address a published page keeps, once, so a visitor arriving there is sent on.</summary>
    private static void Remember(ContentEntry row, string oldPath)
    {
        if (row.PublishedVersionId is null)
        {
            return;
        }

        var paths = row.PreviousPathsJson is null
            ? []
            : JsonSerializer.Deserialize<List<string>>(row.PreviousPathsJson) ?? [];

        if (!paths.Contains(oldPath, StringComparer.Ordinal))
        {
            paths.Add(oldPath);
        }

        paths.Remove(row.Path);
        row.PreviousPathsJson = JsonSerializer.Serialize(paths);
    }

    /// <summary>What a check found: where the page would be, and the refusal when it may not be.</summary>
    private sealed record Checked(
        string? ParentPath,
        ContentAddressState State,
        string? Suggestion,
        IReadOnlyDictionary<string, string[]>? Error);
}
