using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Core.Content;

/// <summary>
/// Once a day, the files of the library whose every use has ended are deleted (M2, T4, note
/// 2026-09-15-file-con-scadenza): the banner of a tour closed a month ago, and nothing else. Carmine
/// wants the space back on the shared disk; the tours and, later, the events say how long they need
/// a file through their projection, and this job is the only thing that acts on it.
/// <list type="bullet">
/// <item>a file with <b>at least one</b> use that ends, and <b>no</b> use without an end or not yet
/// ended, is a candidate — one use still alive, by anybody, keeps it;</item>
/// <item>a file no module ever declared is never touched: the job is not a clean-up of the library;</item>
/// <item>it is deleted by <see cref="MediaDeletion"/>, the very delete of the library, so a published
/// page showing it still refuses, and the file stays on the disk while an old version names it;</item>
/// <item>the ended uses of a file it deleted go with it, and those of a file already deleted by hand.</item>
/// </list>
/// Each deletion is a save of its own, so it is a row in the audit log by nobody — the job — and one
/// file refused does not hold the others back. It never throws: a failure is a row in
/// <c>hub_jobs_log</c>, like the other jobs.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MediaExpiryJob(
    HubDbContext database,
    MediaDeletion deletion,
    IClock clock,
    ILogger<MediaExpiryJob> logger) : IJob
{
    /// <summary>Name under which the runs are recorded.</summary>
    public const string JobName = "media-expiry";

    /// <summary>Longest failure message kept on the log row; the log file has the rest.</summary>
    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many files were deleted on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        database.JobsLog.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        try
        {
            var (deleted, kept) = await ExpireAsync(cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(
                CultureInfo.InvariantCulture,
                $"{deleted} file(s) deleted, {kept} kept because a published page shows them");
            await database.SaveChangesAsync(cancellationToken);

            return deleted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The media expiry job failed.");

            database.ChangeTracker.Clear();
            database.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength
                ? exception.Message
                : exception.Message[..MaxMessageLength];
            await database.SaveChangesAsync(cancellationToken);

            return 0;
        }
    }

    private async Task<(int Deleted, int Kept)> ExpireAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        // Every use of the file has an end, and every end is behind us. Grouped in SQL: the table is
        // one row per file per row of a module, and the answer is a handful of identifiers.
        var due = await database.MediaUses
            .AsNoTracking()
            .GroupBy(use => use.MediaId)
            .Where(group => group.Count(use => use.UsedUntil == null || use.UsedUntil > now) == 0)
            .Select(group => group.Key)
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);

        var deleted = 0;
        var kept = 0;

        foreach (var mediaId in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Past the query filter on purpose: the job is nobody, and a file of the staff is still a
            // file on the disk. A file already deleted is not found here, and only its uses go.
            var media = await CrudSource.BackOffice<MediaAsset>(database)
                .FirstOrDefaultAsync(row => row.Id == mediaId && row.DeletedAt == null, cancellationToken);

            if (media is not null)
            {
                try
                {
                    await deletion.DeleteAsync(media, cancellationToken);
                }
                catch (DomainRefusalException)
                {
                    // A published page shows it: the page wins, as it does by hand. The refusal comes
                    // before anything is marked, and the uses stay, so the file is looked at again
                    // once the page lets it go.
                    kept++;
                    continue;
                }

                deleted++;
            }

            database.MediaUses.RemoveRange(await database.MediaUses
                .Where(use => use.MediaId == mediaId)
                .ToListAsync(cancellationToken));

            await database.SaveChangesAsync(cancellationToken);
        }

        return (deleted, kept);
    }
}
