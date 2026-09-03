using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// Turns a snapshot into rows of <c>cms_search_index</c>, <c>cms_calendar_entries</c> and
/// <c>cms_award_signals</c>. It only ever adds, updates and removes: a projection is rewritten in
/// full for its source key, so it can never drift away from the row it mirrors.
/// <para>Called by the save changes interceptor inside the transaction of the write itself, never
/// by an endpoint or a job.</para>
/// </summary>
public sealed class ProjectionWriter(IClock clock)
{
    /// <summary>
    /// Applies one snapshot. A <c>null</c> snapshot removes what the source had projected, which
    /// is what a deleted row and a draft both come down to.
    /// </summary>
    public async Task ApplyAsync(
        DbContext context,
        string sourceModule,
        string sourceId,
        ProjectionSnapshot? snapshot,
        ProjectionContext projection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        await ApplySearchAsync(context, sourceModule, sourceId, snapshot?.Search, projection, cancellationToken);
        await ApplyCalendarAsync(context, sourceModule, sourceId, snapshot?.Calendar, cancellationToken);
        await ApplyAwardSignalsAsync(context, sourceModule, sourceId, snapshot?.AwardSignals ?? [], cancellationToken);
    }

    /// <summary>True when the context of this write actually holds the projection tables.</summary>
    public static bool CanProject(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Model.FindEntityType(typeof(SearchIndexEntry)) is not null;
    }

    private static async Task ApplySearchAsync(
        DbContext context,
        string sourceModule,
        string sourceId,
        SearchProjection? search,
        ProjectionContext projection,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<SearchIndexEntry>()
            .Where(row => row.SourceModule == sourceModule && row.SourceId == sourceId)
            .ToListAsync(cancellationToken);

        if (search is null)
        {
            context.Set<SearchIndexEntry>().RemoveRange(existing);
            return;
        }

        foreach (var locale in projection.Locales)
        {
            var row = existing.Find(candidate => string.Equals(candidate.Locale, locale, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new SearchIndexEntry { SourceModule = sourceModule, SourceId = sourceId, Locale = locale };
                context.Set<SearchIndexEntry>().Add(row);
            }

            row.Kind = search.Kind;
            row.Url = search.Url;
            row.OwnerDepartment = search.OwnerDepartment;
            row.Visibility = search.Visibility;
            row.Title = Resolve(search.Title, locale, projection.DefaultLocale);
            row.Text = Resolve(search.Text, locale, projection.DefaultLocale);
        }

        // A language the division dropped leaves its row behind; the projection is the whole truth
        // for this source, so what is no longer part of it goes.
        var stale = existing.Where(row => !projection.Locales.Contains(row.Locale, StringComparer.OrdinalIgnoreCase));
        context.Set<SearchIndexEntry>().RemoveRange(stale);
    }

    private async Task ApplyCalendarAsync(
        DbContext context,
        string sourceModule,
        string sourceId,
        CalendarProjection? calendar,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<CalendarEntry>()
            .FirstOrDefaultAsync(row => row.SourceModule == sourceModule && row.SourceId == sourceId, cancellationToken);

        if (calendar is null)
        {
            if (existing is not null)
            {
                context.Set<CalendarEntry>().Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            existing = new CalendarEntry
            {
                SourceModule = sourceModule,
                SourceId = sourceId,
                CreatedAt = clock.UtcNow,
            };
            context.Set<CalendarEntry>().Add(existing);
        }

        existing.Kind = calendar.Kind;
        existing.StartsAtUtc = calendar.StartsAtUtc;
        existing.EndsAtUtc = calendar.EndsAtUtc;
        existing.AllDay = calendar.AllDay;
        existing.OwnerDepartment = calendar.OwnerDepartment;
        existing.Visibility = calendar.Visibility;
        existing.Url = calendar.Url;
        existing.Title = calendar.Title;
        existing.Description = calendar.Description;
        existing.UpdatedAt = clock.UtcNow;
    }

    private async Task ApplyAwardSignalsAsync(
        DbContext context,
        string sourceModule,
        string sourceId,
        IReadOnlyList<AwardSignalProjection> signals,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<AwardSignal>()
            .Where(row => row.SourceModule == sourceModule && row.SourceId == sourceId)
            .ToListAsync(cancellationToken);

        foreach (var signal in signals)
        {
            if (existing.Exists(row => row.Vid == signal.Vid))
            {
                // A signal somebody has already looked at is never rewritten: the queue is a record
                // of what was decided, not a mirror of the source row.
                continue;
            }

            context.Set<AwardSignal>().Add(new AwardSignal
            {
                SourceModule = sourceModule,
                SourceId = sourceId,
                Vid = signal.Vid,
                Reason = signal.Reason,
                Status = AwardSignalStatus.Pending,
                CreatedAt = clock.UtcNow,
            });
        }

        // What the source no longer signals disappears only while nobody has handled it.
        var withdrawn = existing.Where(row =>
            row.Status == AwardSignalStatus.Pending && !signals.Any(signal => signal.Vid == row.Vid));
        context.Set<AwardSignal>().RemoveRange(withdrawn);
    }

    private static string Resolve(Localized<string> value, string locale, string fallback) =>
        value.Resolve(locale, fallback) ?? string.Empty;
}
