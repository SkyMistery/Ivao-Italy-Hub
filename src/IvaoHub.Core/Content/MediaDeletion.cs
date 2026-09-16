using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// What deleting a file of the library means, said once (M2, T4, note 2026-09-15-file-con-scadenza
/// §3): the delete of the library and the expiry job both come here, so "deleted" cannot mean one
/// thing by hand and another at night.
/// <list type="bullet">
/// <item>refused while a published page shows the file — the library offers the archive instead;</item>
/// <item>refused while a row of a module still needs it;</item>
/// <item>otherwise the row is marked deleted and <b>stays</b>, because an older version of a page may
/// name the identifier, and the file leaves the disk only when no version shows it any more.</item>
/// </list>
/// It does not save: the caller does, so the mark lands in the caller's unit of work and the
/// interceptor writes the audit row with whoever the caller is — somebody, or the job.
/// </summary>
public sealed class MediaDeletion(
    HubDbContext database,
    ContentReferenceIndex references,
    MediaStorage storage,
    IClock clock)
{
    public async Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(media);

        var now = clock.UtcNow;
        var uses = await references.UsesOfMediaAsync(media.Id, cancellationToken);

        if (uses.Pages.Count > 0)
        {
            throw new DomainRefusalException("id", "errors.media.inUse");
        }

        if (uses.IsInUse(now))
        {
            throw new DomainRefusalException("id", "errors.media.inUseByModule");
        }

        media.DeletedAt = now;

        var stillShown = await database.ContentVersions
            .ShowingMedia(media.Id)
            .AnyAsync(cancellationToken);

        if (!stillShown && storage.Delete(media.StoredName))
        {
            media.HasFile = false;
        }
    }
}
