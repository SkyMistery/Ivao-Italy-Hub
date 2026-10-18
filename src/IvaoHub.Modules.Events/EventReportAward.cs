using IvaoHub.Core.Content;
using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>
/// A rule of award an accepted report met (design M4 §5.4, E14b; note 2026-10-17-le-regole-di-award, question 1), <c>evt_report_awards</c>:
/// the report, the rule, the member and the award, written in the save that first accepts the report and <b>never deleted by a decision</b>: a
/// rejection turns it off (<see cref="PointsOut"/>); it goes only with its pilot's report, when <c>events-retention</c> deletes that (E15b). It is the
/// row that points the member out in the core's queue of awards: one signal of its own (<see cref="IProjectable"/>), since the core keeps one
/// signal per member per row, and a report that meets two rules points its member out for two awards. A person assigns, never the code
/// (plan §9.7).
/// <para>In the area of the reports, with the event's care and scope, as its report: written by whoever validates. One per report and
/// rule; the reason and the award are the ones of the moment the report first met it, whatever the rule says later (reading 4).</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.ReportsArea)]
public sealed class EventReportAward : IEventChild, IHasResourceScope, IProjectable
{
    /// <summary>The longest reason, as the core's queue keeps it.</summary>
    public const int MaxReasonLength = 256;

    public long Id { get; set; }

    public long ReportId { get; set; }

    /// <summary>The rule it met; unconstrained, so that deleting the rule later leaves what it pointed out.</summary>
    public long RuleId { get; set; }

    public long EventId { get; set; }

    /// <summary>The member, as on the report.</summary>
    public int Vid { get; set; }

    public long AwardId { get; set; }

    /// <summary>Why, in the division's language: the event and the criterion met. Nothing of the member.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Whether the row points its member out now: its report accepted and the rule met. A report rejected, or accepted again without meeting
    /// the rule, keeps the row and turns this off — the core withdraws a signal still waiting, and keeps one somebody already looked at.
    /// </summary>
    public bool PointsOut { get; set; }

    public DateTime CreatedAt { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public string ResourceScope => Event.ScopeOf(EventId);

    public string SourceModule => EventsModule.ModuleKey;

    public string SourceId => SourceOf(Id);

    /// <summary>The key of its signal in the core's queue.</summary>
    public static string SourceOf(long id) => $"report-award:{id}";

    public ProjectionSnapshot? Project(ProjectionContext context) =>
        new(null, [], PointsOut ? [new AwardSignalProjection(Vid, Reason, AwardId)] : [], []);
}
