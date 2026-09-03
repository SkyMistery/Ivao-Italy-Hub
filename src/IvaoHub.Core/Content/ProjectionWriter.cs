using IvaoHub.Core.Auth;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>One row to project, and what it wants to look like. A null snapshot removes it.</summary>
public sealed record ProjectionRequest(string SourceModule, string SourceId, ProjectionSnapshot? Snapshot);

/// <summary>
/// Everything the projections of one save need to read, read once.
/// <para>The rows are loaded ignoring the query filters on purpose: the projection tables carry a
/// visibility of their own, and a writer that could only see what the current user may read would
/// fail to find the row it is meant to rewrite and insert a duplicate instead.</para>
/// </summary>
public sealed class ProjectionState
{
    /// <summary>Built only by the writer that reads it back; a caller just carries it across.</summary>
    internal ProjectionState(
        ILookup<(string Module, string Id), SearchIndexEntry> search,
        Dictionary<(string Module, string Id), CalendarEntry> calendar,
        ILookup<(string Module, string Id), AwardSignal> awards)
    {
        Search = search;
        Calendar = calendar;
        Awards = awards;
    }

    internal ILookup<(string Module, string Id), SearchIndexEntry> Search { get; }

    internal Dictionary<(string Module, string Id), CalendarEntry> Calendar { get; }

    internal ILookup<(string Module, string Id), AwardSignal> Awards { get; }
}

/// <summary>
/// Turns snapshots into rows of <c>cms_search_index</c>, <c>cms_calendar_entries</c> and
/// <c>cms_award_signals</c>. It only ever adds, updates and removes: a projection is rewritten in
/// full for its source key, so it can never drift away from the row it mirrors.
/// <para>Called by the save changes interceptor inside the transaction of the write itself, never
/// by an endpoint or a job.</para>
/// <para>Reading and writing are separate on purpose. Reading is three queries for the <b>whole</b>
/// save, not three per row: a save that touches fifty rows used to issue a hundred and fifty
/// queries inside the write transaction, which is a lock held for as long as the round trips take.
/// Splitting them also gives the synchronous path a real synchronous implementation instead of
/// blocking on an asynchronous one, without duplicating any of the reasoning below.</para>
/// </summary>
public sealed class ProjectionWriter(IClock clock, ICurrentUser currentUser)
{
    /// <summary>True when the context of this write actually holds the projection tables.</summary>
    public static bool CanProject(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Model.FindEntityType(typeof(SearchIndexEntry)) is not null;
    }

    /// <summary>Reads what these sources have projected so far. Three queries per source module.</summary>
    public static async Task<ProjectionState> LoadAsync(
        DbContext context,
        IReadOnlyList<ProjectionRequest> requests,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requests);

        var search = new List<SearchIndexEntry>();
        var calendar = new List<CalendarEntry>();
        var awards = new List<AwardSignal>();

        foreach (var (module, ids) in ByModule(requests))
        {
            search.AddRange(await SearchOf(context, module, ids).ToListAsync(cancellationToken));
            calendar.AddRange(await CalendarOf(context, module, ids).ToListAsync(cancellationToken));
            awards.AddRange(await AwardsOf(context, module, ids).ToListAsync(cancellationToken));
        }

        return Build(search, calendar, awards);
    }

    /// <summary>The same, for a caller that saved synchronously.</summary>
    public static ProjectionState Load(DbContext context, IReadOnlyList<ProjectionRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requests);

        var search = new List<SearchIndexEntry>();
        var calendar = new List<CalendarEntry>();
        var awards = new List<AwardSignal>();

        foreach (var (module, ids) in ByModule(requests))
        {
            search.AddRange(SearchOf(context, module, ids).ToList());
            calendar.AddRange(CalendarOf(context, module, ids).ToList());
            awards.AddRange(AwardsOf(context, module, ids).ToList());
        }

        return Build(search, calendar, awards);
    }

    /// <summary>
    /// Applies every snapshot against what was read. Pure bookkeeping over the change tracker: no
    /// query, no save, so the two paths above share all of it.
    /// </summary>
    public void Apply(
        DbContext context,
        ProjectionState state,
        IReadOnlyList<ProjectionRequest> requests,
        ProjectionContext projection)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(projection);

        foreach (var request in requests)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceModule);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceId);

            var key = (request.SourceModule, request.SourceId);

            ApplySearch(context, request, [.. state.Search[key]], projection);
            ApplyCalendar(context, request, state.Calendar.GetValueOrDefault(key));
            ApplyAwardSignals(context, request, [.. state.Awards[key]]);
        }
    }

    private static IEnumerable<(string Module, string[] Ids)> ByModule(IReadOnlyList<ProjectionRequest> requests) =>
        requests
            .GroupBy(request => request.SourceModule, StringComparer.Ordinal)
            .Select(group => (group.Key, group.Select(request => request.SourceId).Distinct(StringComparer.Ordinal).ToArray()));

    private static IQueryable<SearchIndexEntry> SearchOf(DbContext context, string module, string[] ids) =>
        context.Set<SearchIndexEntry>().IgnoreQueryFilters()
            .Where(row => row.SourceModule == module && ids.Contains(row.SourceId));

    private static IQueryable<CalendarEntry> CalendarOf(DbContext context, string module, string[] ids) =>
        context.Set<CalendarEntry>().IgnoreQueryFilters()
            .Where(row => row.SourceModule == module && ids.Contains(row.SourceId));

    private static IQueryable<AwardSignal> AwardsOf(DbContext context, string module, string[] ids) =>
        context.Set<AwardSignal>()
            .Where(row => row.SourceModule == module && ids.Contains(row.SourceId));

    private static ProjectionState Build(
        List<SearchIndexEntry> search,
        List<CalendarEntry> calendar,
        List<AwardSignal> awards) => new(
            search.ToLookup(row => (row.SourceModule, row.SourceId)),
            calendar.ToDictionary(row => (row.SourceModule, row.SourceId)),
            awards.ToLookup(row => (row.SourceModule, row.SourceId)));

    private static void ApplySearch(
        DbContext context,
        ProjectionRequest request,
        List<SearchIndexEntry> existing,
        ProjectionContext projection)
    {
        var search = request.Snapshot?.Search;

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
                row = new SearchIndexEntry
                {
                    SourceModule = request.SourceModule,
                    SourceId = request.SourceId,
                    Locale = locale,
                };
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

    private void ApplyCalendar(DbContext context, ProjectionRequest request, CalendarEntry? existing)
    {
        var calendar = request.Snapshot?.Calendar;

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
                SourceModule = request.SourceModule,
                SourceId = request.SourceId,
                CreatedAt = clock.UtcNow,
                CreatedBy = Author,
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
        existing.UpdatedBy = Author;
    }

    private void ApplyAwardSignals(DbContext context, ProjectionRequest request, List<AwardSignal> existing)
    {
        var signals = request.Snapshot?.AwardSignals ?? [];

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
                SourceModule = request.SourceModule,
                SourceId = request.SourceId,
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

    /// <summary>
    /// Who caused the projection. A calendar entry is <c>IAuditable</c>, but the interceptor does
    /// not stamp what it writes in its own second pass, so the writer fills the columns from the
    /// same identity the interceptor used. Zero for a background job, as everywhere else.
    /// </summary>
    private int Author => currentUser.IsAuthenticated ? currentUser.Vid : 0;

    private static string Resolve(Localized<string> value, string locale, string fallback) =>
        value.Resolve(locale, fallback) ?? string.Empty;
}
