using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// Why a page could not be published, in the shape a form reads: an i18n key per path, and the
/// languages that are missing next to it, so the editor can say "Italian is missing in the hero"
/// instead of "invalid".
/// </summary>
public sealed record ContentPublishFailure(
    IReadOnlyDictionary<string, string[]> Errors,
    IReadOnlyDictionary<string, string[]> MissingLocales);

/// <summary>
/// Turning a draft into what the public sees (design M0 section 5.5).
/// <para>Four things happen, in this order and only together. Every translated value has to be
/// written in every language of the division. Every data block asking to be <c>frozen</c> is
/// resolved now and its answer stored in the version, so the page keeps saying what it said on the
/// day it was published even when the underlying rows move. A new
/// <see cref="ContentVersion"/> is written. And the row itself becomes <c>Published</c>, which is
/// what makes the interceptor put it into the search index and the visibility filter let a visitor
/// through.</para>
/// <para>The draft is not rewritten: the captured data lives in the version. Publish again and it
/// is captured again, which is the whole of what "republish to refresh" means.</para>
/// </summary>
public sealed class ContentPublishService(
    HubDbContext database,
    BlockDocumentWalker walker,
    BlockRegistry blocks,
    DataBlockProviders providers,
    ICurrentUser currentUser,
    IClock clock,
    IOptions<DivisionOptions> division)
{
    /// <summary>The cover of a news item, named as the form names it.</summary>
    private const string CoverField = "coverMediaId";

    /// <summary>The file a document is, named as the form names it.</summary>
    private const string FileField = "fileMediaId";

    /// <summary>The draft as the back office sees it: filters off, because a draft is invisible.</summary>
    public Task<ContentEntry?> FindAsync(long id, CancellationToken cancellationToken) =>
        CrudSource.BackOffice<ContentEntry>(database).FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    /// <summary>
    /// What stands between this row and the public, without touching it: the same checks
    /// <see cref="PublishAsync"/> runs, and <c>null</c> when there is nothing in the way.
    /// <para>It exists so that the editor can say what is missing <b>before</b> somebody presses
    /// publish and is told no. The alternative was the client working it out for itself, which
    /// would be the rules of publication written a second time — and the second copy is the one
    /// that goes stale (plan §16.E, rule (b)). So the answer comes from the one place that
    /// decides, and the screen only draws it.</para>
    /// </summary>
    public async Task<ContentPublishFailure?> ProblemsAsync(
        ContentEntry content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.IsTemplate)
        {
            return new ContentPublishFailure(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["isTemplate"] = ["errors.content.templateNotPublishable"],
                },
                new Dictionary<string, string[]>(StringComparer.Ordinal));
        }

        var body = JsonNode.Parse(content.BodyJson) ?? new JsonObject();

        return Incomplete(content, body)
            ?? await PicturesTheReaderCannotSeeAsync(content, body, cancellationToken);
    }

    /// <summary>
    /// Publishes the row, or says what stopped it. A template is never published: it is a tool of
    /// the staff, it has no address of its own and nobody reads it.
    /// </summary>
    public async Task<ContentPublishFailure?> PublishAsync(
        ContentEntry content,
        string? changelog,
        CancellationToken cancellationToken,
        int? approvedBy = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (await ProblemsAsync(content, cancellationToken) is { } failure)
        {
            return failure;
        }

        var body = JsonNode.Parse(content.BodyJson) ?? new JsonObject();

        await FreezeAsync(body, DataBlockContext.Publishing(content.Visibility, content.OwnerDepartment), cancellationToken);

        var now = clock.UtcNow;
        var version = new ContentVersion
        {
            ContentId = content.Id,
            Version = await NextVersionAsync(content.Id, cancellationToken),
            Title = content.Title,
            BodyJson = body.ToJsonString(),
            SchemaVersion = content.SchemaVersion,
            Changelog = changelog,
            PublishedAt = now,
            PublishedBy = currentUser.Vid,
            ApprovedBy = approvedBy,
        };

        // One transaction for both saves. The interceptor joins the one it finds rather than
        // opening its own, so the projection of the search index lands inside it too; a failure
        // half way leaves neither a version nor a published row (design M0 section 3.4).
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        content.Status = PublishStatus.Published;
        content.PublishedAt = now;
        database.ContentVersions.Add(version);
        await database.SaveChangesAsync(cancellationToken);

        // Only now does the version have an identifier to point at.
        content.PublishedVersionId = version.Id;
        await database.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return null;
    }

    /// <summary>
    /// The published version of a row, which is the only thing the public site ever reads.
    /// </summary>
    public Task<ContentVersion?> PublishedVersionAsync(ContentEntry content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        return content.PublishedVersionId is not { } versionId
            ? Task.FromResult<ContentVersion?>(null)
            : database.ContentVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(version => version.Id == versionId, cancellationToken)!;
    }

    /// <summary>
    /// The title of the row and every translated value inside the blocks, checked against the
    /// languages of the division. A visitor reading in the other language must not find a hole.
    /// </summary>
    private ContentPublishFailure? Incomplete(ContentEntry content, JsonNode body)
    {
        var locales = division.Value.Locales;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var missing = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!content.Title.HasAll(locales))
        {
            errors["title"] = ["errors.localized.missing"];
            missing["title"] = [.. locales.Where(locale => string.IsNullOrWhiteSpace(content.Title.Get(locale)))];
        }

        foreach (var gap in walker.MissingLocales(body))
        {
            var field = $"body.{gap.Path}";
            errors[field] = ["errors.localized.missing"];
            missing[field] = [.. gap.Locales];
        }

        return errors.Count == 0 ? null : new ContentPublishFailure(errors, missing);
    }

    /// <summary>
    /// Every picture the page shows, checked against its readers.
    /// <para>⚠️ This is the defect the demo of M1 found: a file arrives in the library visible to
    /// the staff and becomes public because somebody says so, so a picture uploaded and dropped
    /// into a page is staff-only — and the page went out with it. Nothing refused, nothing warned,
    /// and a visitor got a broken picture, because the address of a file they may not see answers
    /// 404 by design.</para>
    /// <para>The rule is the one that already exists: <see cref="VisibilityCeiling"/>, the same
    /// ceiling a frozen data block is captured under. A page may only carry what its own readers
    /// may be served — and a picture is exactly that, a row of another table copied into what
    /// somebody else opens.</para>
    /// <para>Refused rather than repaired: publishing a page must not quietly make a file public.
    /// A file becomes public because somebody said so, which is the rule the upload already
    /// follows, and this says so where the editor can act on it.</para>
    /// <para>The rows are read past the query filter on purpose. The question is not what the
    /// person publishing may see — they may well see more than the page's readers — but what the
    /// reader will be served, and <c>HasFile</c> is that: a row whose bytes retention has already
    /// taken away answers 404 whatever its visibility says.</para>
    /// </summary>
    private async Task<ContentPublishFailure?> PicturesTheReaderCannotSeeAsync(
        ContentEntry content,
        JsonNode body,
        CancellationToken cancellationToken)
    {
        // The body, plus the two a row names in a column of its own: the cover of a news item and
        // the file attached to a document. Three ways of saying "this page shows that file", and a
        // reader is served all three the same way — which is why the check is one and not three.
        // The names are the fields of the form, so a refusal lands on the control that holds them.
        var shown = walker.MediaReferences(body)
            .Select(reference => (Field: $"body.{reference.Path}", reference.Id))
            .ToList();

        if (content.CoverMediaId is { } cover)
        {
            shown.Add((CoverField, cover));
        }

        if (content.FileMediaId is { } file)
        {
            shown.Add((FileField, file));
        }

        if (shown.Count == 0)
        {
            return null;
        }

        var identifiers = shown.Select(reference => reference.Id).Distinct().ToArray();

        var rows = await CrudSource.BackOffice<MediaAsset>(database)
            .Where(media => identifiers.Contains(media.Id))
            .Select(media => new
            {
                media.Id,
                media.Visibility,
                media.OwnerDepartment,
                media.HasFile,
            })
            .ToDictionaryAsync(media => media.Id, cancellationToken);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (field, id) in shown)
        {
            if (!rows.TryGetValue(id, out var media) || !media.HasFile)
            {
                errors[field] = ["errors.content.mediaMissing"];
            }
            else if (!VisibilityCeiling.Allows(
                content.Visibility,
                content.OwnerDepartment,
                media.Visibility,
                media.OwnerDepartment))
            {
                errors[field] = ["errors.content.mediaNotVisible"];
            }
        }

        return errors.Count == 0
            ? null
            : new ContentPublishFailure(errors, new Dictionary<string, string[]>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Captures what every data block asking to be frozen says right now, and clears the capture of
    /// every block that does not: a block switched back to live must stop showing what it caught
    /// last time, or "change it to live and republish" would change nothing.
    /// <para>The provider is told which page the answer is going into, and stops at what that page
    /// may show. A capture outlives the person who made it: it is read by whoever opens the page,
    /// and it must not carry rows only the publisher could see.</para>
    /// </summary>
    private async Task FreezeAsync(
        JsonNode body,
        DataBlockContext context,
        CancellationToken cancellationToken)
    {
        foreach (var block in walker.EnumerateBlocks(body))
        {
            var descriptor = blocks.Find(block.Type);
            var frozen = descriptor is { AlwaysLive: false }
                && string.Equals(
                    (block.Node["renderMode"] as JsonValue)?.GetValue<string>(),
                    "frozen",
                    StringComparison.Ordinal);

            if (!frozen || providers.For(descriptor) is not { } provider)
            {
                block.Node["frozen"] = null;
                continue;
            }

            // The properties travel to the provider exactly as the editor wrote them; nothing here
            // reads them (plan section 16.5).
            var resolved = await provider.ResolveAsync(
                block.Node["props"]?.DeepClone(),
                context,
                cancellationToken);
            block.Node["frozen"] = resolved;
        }
    }

    private async Task<int> NextVersionAsync(long contentId, CancellationToken cancellationToken)
    {
        var highest = await database.ContentVersions
            .AsNoTracking()
            .Where(version => version.ContentId == contentId)
            .Select(version => (int?)version.Version)
            .MaxAsync(cancellationToken);

        return (highest ?? 0) + 1;
    }
}
