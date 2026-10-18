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
/// interceptor puts in the core's queue in the same transaction; rejecting it keeps the rows and makes them point nobody out, and the core
/// withdraws the signals still waiting. Every signal the report ever produced keeps its row, so «was an award assigned from it?» sees them all.
/// <para><b>Once an award was assigned from one of them, the report's decision no longer changes</b> (Carmine's limit on #287): whether one was
/// is the core's answer (<see cref="AwardSignalQueries"/>, E10x), read under the lock of the signals' rows inside the decision's transaction,
/// so that an assignment in the same instant passes before or after it, never in between.</para>
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
    /// Whether an award was assigned from a signal the report ever produced — every row of it, pointing out or not —, through the core's one
    /// read of the queue (<see cref="AwardSignalQueries.AssignedAsync"/>, E10x; Carmine's answer 3 on #293). Called inside the decision's
    /// transaction, it holds the signals' rows until the decision commits, so that an assignment in the same instant passes before or after it.
    /// </summary>
    public async Task<bool> AssignedAsync(long reportId, CancellationToken cancellationToken)
    {
        var sources = (await database.ReportAwards.AsNoTracking()
                .Where(award => award.ReportId == reportId)
                .Select(award => award.Id)
                .ToListAsync(cancellationToken))
            .Select(EventReportAward.SourceOf)
            .ToList();

        return (await AwardSignalQueries.AssignedAsync(database, EventsModule.ModuleKey, sources, cancellationToken)).Count > 0;
    }

    /// <summary>The reports among <paramref name="reportIds"/> an award was assigned from: their decision is not offered any more.</summary>
    public async Task<IReadOnlySet<long>> AssignedAmongAsync(IReadOnlyCollection<long> reportIds, CancellationToken cancellationToken)
    {
        var awards = await database.ReportAwards.AsNoTracking()
            .Where(award => reportIds.Contains(award.ReportId))
            .Select(award => new { award.Id, award.ReportId })
            .ToListAsync(cancellationToken);
        var assigned = await AwardSignalQueries.AssignedAsync(
            database, EventsModule.ModuleKey, [.. awards.Select(award => EventReportAward.SourceOf(award.Id))], cancellationToken);

        return awards.Where(award => assigned.Contains(EventReportAward.SourceOf(award.Id))).Select(award => award.ReportId).ToHashSet();
    }
    /// <summary>
    /// Writes what the report's new state points out, into the tracked context, for the save of the decision (the review of #293, point 1).
    /// <b>A row is never deleted</b>: it is the source of a signal the core keeps whatever happens to it once somebody looked at it — dismissed or
    /// assigned —, so the row stays, and says whether it points its member out (<see cref="EventReportAward.PointsOut"/>). Accepted, the rules of
    /// its event its verified items meet point out — a rule met again finds its own row, so a signal dismissed stays dismissed —, the others do
    /// not; rejected, none does, and the core withdraws the signals still waiting.
    /// </summary>
    public async Task ApplyAsync(EventReport report, Event row, DateTime now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(row);

        var rows = await database.ReportAwards.Where(award => award.ReportId == report.Id).ToListAsync(cancellationToken);
        var met = report.State == ReportState.Accepted ? await MetAsync(report, row, cancellationToken) : [];
        foreach (var award in rows)
        {
            award.PointsOut = met.Any(rule => rule.Id == award.RuleId);
        }

        var locale = division.Value.DefaultLocale;
        var title = row.Title.Resolve(locale, locale) ?? string.Empty;
        foreach (var rule in met.Where(rule => rows.All(award => award.RuleId != rule.Id)))
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
                PointsOut = true,
                CreatedAt = now,
                OwnerDepartment = report.OwnerDepartment,
                OwnerDepartmentMask = report.OwnerDepartmentMask,
            });
        }
    }

    /// <summary>The rules of the event the report's verified items meet, of its kind.</summary>
    private async Task<IReadOnlyList<EventAwardRule>> MetAsync(EventReport report, Event row, CancellationToken cancellationToken)
    {
        var rules = await database.AwardRules.AsNoTracking()
            .Where(rule => rule.EventId == row.Id && rule.Kind == report.Kind)
            .OrderBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            return [];
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

        return [.. rules.Where(rule => AwardRules.IsMet(rule, report.Kind, items, types))];
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
