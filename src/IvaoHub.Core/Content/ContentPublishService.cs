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
    /// <summary>The draft as the back office sees it: filters off, because a draft is invisible.</summary>
    public Task<ContentEntry?> FindAsync(long id, CancellationToken cancellationToken) =>
        CrudSource.BackOffice<ContentEntry>(database).FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    /// <summary>
    /// Publishes the row, or says what stopped it. A template is never published: it is a tool of
    /// the staff, it has no address of its own and nobody reads it.
    /// </summary>
    public async Task<ContentPublishFailure?> PublishAsync(
        ContentEntry content,
        string? changelog,
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

        if (Incomplete(content, body) is { } failure)
        {
            return failure;
        }

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
