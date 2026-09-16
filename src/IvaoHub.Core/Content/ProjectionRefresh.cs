using IvaoHub.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// Projects rows again without writing them: for a row whose projection depends on the time as well as on
/// its columns. A tour is the first (M2, T6): a tour that is ready but not yet released is the staff's, and at
/// its release it becomes everybody's without anybody saving it — so the search index and the calendar would
/// keep saying "staff" for good.
/// <para>It goes through the interceptor and not around it: the rows are projected exactly as a save would
/// project them, the rule that a draft keeps only its files included, in a transaction of their own. Nothing
/// else about them is stamped, audited or saved, because nothing about them changed.</para>
/// <para>A job calls it, never an endpoint: an endpoint writes the row, and the write projects.</para>
/// </summary>
public sealed class ProjectionRefresh(HubSaveChangesInterceptor interceptor)
{
    /// <summary>Rewrites the projections of these rows, read through <paramref name="context"/>.</summary>
    public async Task RefreshAsync(
        DbContext context,
        IReadOnlyCollection<IProjectable> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return;
        }

        interceptor.ProjectAgain(context, rows);
        await context.SaveChangesAsync(cancellationToken);
    }
}
