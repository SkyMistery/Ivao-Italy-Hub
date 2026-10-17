using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Events.Reports;

/// <summary>
/// The awards a report points its member out for (design M4 §5.4, E14b; note 2026-10-17-le-regole-di-award, readings 3–5): accepting it
/// evaluates the rules of its event on its verified items and writes a row per rule met (<see cref="EventReportAward"/>), whose signal the
/// interceptor puts in the core's queue in the same transaction; rejecting it deletes them, and the core withdraws the signals still waiting.
/// <para><b>Once an award was assigned from one of them, the report's decision no longer changes</b> (Carmine's limit on #287): whether one was
/// is the core's queue, which the module's context maps — read under the lock of the signals' rows inside the decision's transaction, so that an
/// assignment in the same instant passes before or after it, never in between.</para>
/// </summary>
public sealed class ReportAwards(
    EventsDbContext database,
    IAtcPositionDirectory positions,
    LocaleCatalog catalog,
    IOptions<DivisionOptions> division)
{
    /// <summary>The refusal of a decision whose report gave an award already.</summary>
    public const string AssignedKey = "events:errors.reportAwardAssigned";

    /// <summary>The sentence of a signal: the event and the criterion met.</summary>
    public const string ReasonKey = "events:awards.ruleMet";

    /// <summary>
    /// Whether an award was assigned from a signal of the report, taking the lock of those signals' rows when <paramref name="locking"/>: inside
    /// a transaction, the decision that follows is the last word on them until it commits.
    /// </summary>
    public async Task<bool> AssignedAsync(long reportId, bool locking, CancellationToken cancellationToken)
    {
        var sources = (await database.ReportAwards.AsNoTracking()
                .Where(award => award.ReportId == reportId)
                .Select(award => award.Id)
                .ToListAsync(cancellationToken))
            .Select(EventReportAward.SourceOf)
            .ToList();

        var module = EventsModule.ModuleKey;
        var handled = nameof(AwardSignalStatus.Handled);
        foreach (var source in sources)
        {
            var statuses = locking
                ? await database.Database
                    .SqlQuery<string>($"SELECT status AS `Value` FROM cms_award_signals WHERE source_module = {module} AND source_id = {source} FOR UPDATE")
                    .ToListAsync(cancellationToken)
                : await database.Database
                    .SqlQuery<string>($"SELECT status AS `Value` FROM cms_award_signals WHERE source_module = {module} AND source_id = {source}")
                    .ToListAsync(cancellationToken);
            if (statuses.Contains(handled, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The reports among <paramref name="reportIds"/> an award was assigned from: their decision is not offered any more.</summary>
    public async Task<IReadOnlySet<long>> AssignedAmongAsync(IReadOnlyCollection<long> reportIds, CancellationToken cancellationToken)
    {
        var awards = await database.ReportAwards.AsNoTracking()
            .Where(award => reportIds.Contains(award.ReportId))
            .Select(award => new { award.Id, award.ReportId })
            .ToListAsync(cancellationToken);
        if (awards.Count == 0)
        {
            return new HashSet<long>();
        }

        var sources = awards.Select(award => EventReportAward.SourceOf(award.Id)).ToList();
        var handled = (await database.Set<AwardSignal>().AsNoTracking()
                .Where(signal => signal.SourceModule == EventsModule.ModuleKey
                    && sources.Contains(signal.SourceId)
                    && signal.Status == AwardSignalStatus.Handled)
                .Select(signal => signal.SourceId)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        return awards.Where(award => handled.Contains(EventReportAward.SourceOf(award.Id))).Select(award => award.ReportId).ToHashSet();
    }

    /// <summary>
    /// Writes what the report's new state points out, into the tracked context, for the save of the decision: accepted, a row per rule of its
    /// event its verified items meet; rejected, none.
    /// </summary>
    public async Task ApplyAsync(EventReport report, Event row, DateTime now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(row);

        database.ReportAwards.RemoveRange(await database.ReportAwards
            .Where(award => award.ReportId == report.Id)
            .ToListAsync(cancellationToken));
        if (report.State != ReportState.Accepted)
        {
            return;
        }

        var rules = await database.AwardRules.AsNoTracking()
            .Where(rule => rule.EventId == row.Id && rule.Kind == report.Kind)
            .OrderBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            return;
        }

        var items = await CrudSource.BackOffice<EventReportItem>(database).AsNoTracking()
            .Where(item => item.ReportId == report.Id)
            .ToListAsync(cancellationToken);

        // The kind of each position the report names, asked of the core only when a rule asks for one.
        IReadOnlyDictionary<string, string> types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rules.Any(rule => rule.PositionType is not null))
        {
            var found = await positions.FindAsync([.. items.Select(item => item.Callsign).Distinct(StringComparer.OrdinalIgnoreCase)], cancellationToken);
            types = found.ToDictionary(pair => pair.Key, pair => pair.Value.Type, StringComparer.OrdinalIgnoreCase);
        }

        var locale = division.Value.DefaultLocale;
        var title = row.Title.Resolve(locale, locale) ?? string.Empty;
        foreach (var rule in rules.Where(rule => AwardRules.IsMet(rule, report.Kind, items, types)))
        {
            var reason = catalog.Resolve(locale, ReasonKey)
                .Replace("{{event}}", title, StringComparison.Ordinal)
                .Replace("{{criterion}}", catalog.Resolve(locale, CriterionKey(rule.Criterion)), StringComparison.Ordinal);

            database.ReportAwards.Add(new EventReportAward
            {
                ReportId = report.Id,
                RuleId = rule.Id,
                EventId = row.Id,
                Vid = report.Vid,
                AwardId = rule.AwardId,
                Reason = reason.Length <= EventReportAward.MaxReasonLength ? reason : reason[..EventReportAward.MaxReasonLength],
                CreatedAt = now,
                OwnerDepartment = report.OwnerDepartment,
                OwnerDepartmentMask = report.OwnerDepartmentMask,
            });
        }
    }

    /// <summary>The words of a criterion, the ones the tab of the rules shows: a key each, so that the server asks for keys it can name.</summary>
    private static string CriterionKey(AwardCriterion criterion) => criterion switch
    {
        AwardCriterion.AnyFlight => "events:awardRules.options.criterion.AnyFlight",
        AwardCriterion.MinFlights => "events:awardRules.options.criterion.MinFlights",
        AwardCriterion.Route => "events:awardRules.options.criterion.Route",
        AwardCriterion.RouteTimes => "events:awardRules.options.criterion.RouteTimes",
        AwardCriterion.MinLegDistance => "events:awardRules.options.criterion.MinLegDistance",
        AwardCriterion.AnyShift => "events:awardRules.options.criterion.AnyShift",
        _ => "events:awardRules.options.criterion.MinAtcMinutes",
    };
}
