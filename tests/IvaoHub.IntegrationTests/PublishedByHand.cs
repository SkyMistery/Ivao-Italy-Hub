using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The state publication leaves a content row in, written straight into the database by a test that is
/// about something else: a version of what the row says, and the row pointing at it. A row is online once
/// it has a published version, not because its status says so (note
/// 2026-10-21-il-pubblico-legge-la-versione), so a row marked <c>Published</c> by hand without one is a
/// state the hub never produces and the public never sees.
/// </summary>
internal static class PublishedByHand
{
    /// <summary>Publishes a row already saved, as <c>ContentPublishService</c> would, without its checks.</summary>
    public static async Task PublishAsync(
        HubDbContext database,
        ContentEntry content,
        CancellationToken cancellationToken,
        int publishedBy = 0)
    {
        var version = new ContentVersion
        {
            ContentId = content.Id,
            Version = 1,
            Title = content.Title,
            BodyJson = content.BodyJson,
            SchemaVersion = content.SchemaVersion,
            Summary = content.Summary,
            Seo = content.Seo,
            CollectionsJson = content.CollectionsJson,
            CoverMediaId = content.CoverMediaId,
            FileMediaId = content.FileMediaId,
            ShowFooter = content.ShowFooter,
            PublishedAt = content.PublishedAt ?? DateTime.UtcNow,
            PublishedBy = publishedBy,
        };

        database.ContentVersions.Add(version);
        await database.SaveChangesAsync(cancellationToken);

        content.PublishedVersionId = version.Id;
        content.Status = PublishStatus.Published;
        content.PublishedAt ??= version.PublishedAt;
        await database.SaveChangesAsync(cancellationToken);
    }
}
