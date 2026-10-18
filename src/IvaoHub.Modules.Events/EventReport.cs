using IvaoHub.Core.Division;
using IvaoHub.Core.Privacy;

namespace IvaoHub.Modules.Events;

/// <summary>What a report of support tells (design M4 §1.8): flights flown, or shifts controlled.</summary>
public enum ReportKind
{
    Pilot,
    Atc,
}

/// <summary>Who wrote a report (§1.8): its member by hand, or the check after the event for a member who asked it to (E14b).</summary>
public enum ReportSource
{
    Manual,
    Auto,
}

/// <summary>Where a report stands (§5.3): waiting for a validator, accepted, or rejected.</summary>
public enum ReportState
{
    Submitted,
    Accepted,
    Rejected,
}

/// <summary>
/// What the check found of a report's items (§5.3): every item found, some, none, or a source that did not answer for some item — never a
/// guess.
/// </summary>
public enum ReportVerification
{
    Valid,
    Partial,
    Invalid,
    Unavailable,
}

/// <summary>
/// A member's report of support for an event (design M4 §1.8, §5.2, §5.3, E14a; note 2026-10-17-il-pirep-di-supporto), <c>evt_reports</c>:
/// one per member per event per kind — the unique index is the database's —, with its items (<see cref="EventReportItem"/>), what the check
/// found of them, and the decision of whoever validates it.
/// <para>A row of a member, as a registration is: sent by them (<see cref="ISubmittedByMembers"/>) and about them
/// (<see cref="IHasStakeholder"/>), in the area of the reports with the event's departments and scope, adopted from the event before the
/// staff decide (<see cref="IEventChild"/>). The member corrects it until it is decided (the core's rule of a row a member sent, T11).
/// <b>It carries a decision of the staff</b> — the state, who decided, their note —, so it is not <c>[WithdrawnByStakeholder]</c>: deleting
/// it would delete the decision too (reading 2; Carmine's rule on #232, point 6). <c>EventReports.Edit</c> is denied to its member, the
/// super administrator included (<c>DeniedToStakeholder</c>).</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.ReportsArea)]
public sealed class EventReport : IEventChild, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope
{
    /// <summary>The longest note of a decision: a sentence or two.</summary>
    public const int MaxNoteLength = 1000;

    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>The member: whoever the row is about.</summary>
    public int Vid { get; set; }

    public ReportKind Kind { get; set; }

    public ReportSource Source { get; set; } = ReportSource.Manual;

    public ReportState State { get; set; } = ReportState.Submitted;

    public ReportVerification Verification { get; set; } = ReportVerification.Unavailable;

    /// <summary>The VID of whoever of the staff decided it.</summary>
    public int? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    /// <summary>What the validator wrote about it, about the member: emptied with their data (E10n).</summary>
    [ErasedWith(nameof(Vid))]
    public string? DecisionNote { get; set; }

    /// <summary>When the member last sent it.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>When the check last looked at its items: at every send.</summary>
    public DateTime CheckedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>Any member, as the global filter reads it; which member is the endpoints' business, as for a registration.</summary>
    public Visibility Visibility
    {
        get => Visibility.Members;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public int? StakeholderVid => Vid;

    /// <summary>The event's: a permission granted on one event reaches its reports.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}

/// <summary>
/// One item of a report of support (design M4 §1.8, E14a; note 2026-10-17-il-pirep-di-supporto, readings 1, 4 and 5), <c>evt_report_items</c>:
/// a flight — callsign, departure, arrival, day — or a shift — the position's callsign, from, to — as its member declared it, and what the
/// check found: the session of the network, the minutes, the distance between the two airports, whether it is verified (none: not asked).
/// <para>A row of the member as its report is, <b>with the member's VID of its own</b>: the guard of the core reads whose a row is from the
/// row it writes (E10h), and <see cref="ErasedWithAttribute"/> covers only a column the same row names (E10n). It carries no decision of the
/// staff — the check is the system's —, so its member takes it back (<see cref="WithdrawnByStakeholderAttribute"/>): a correction replaces the
/// items of a report not decided yet, which the verb alone allows (reading 6).</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.ReportsArea)]
[WithdrawnByStakeholder]
public sealed class EventReportItem : IEventChild, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope
{
    /// <summary>The longest callsign of a flight or of a position, as a slot's and a position's.</summary>
    public const int MaxCallsignLength = 16;

    public long Id { get; set; }

    public long ReportId { get; set; }

    public long EventId { get; set; }

    /// <summary>The member, as on the report.</summary>
    public int Vid { get; set; }

    /// <summary>Where it stands among the report's items, from 0, as the member listed them.</summary>
    public int Ordinal { get; set; }

    /// <summary>The flight's callsign, or the position's: the member's data, emptied with it (E10n).</summary>
    [ErasedWith(nameof(Vid))]
    public string Callsign { get; set; } = string.Empty;

    /// <summary>A flight only: where it left from.</summary>
    public string? DepartureIcao { get; set; }

    /// <summary>A flight only: where it landed.</summary>
    public string? ArrivalIcao { get; set; }

    /// <summary>A flight only: the day it flew, in UTC.</summary>
    public DateOnly? FlightDay { get; set; }

    /// <summary>A shift only: from when.</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>A shift only: to when.</summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>A flight only: the session of the network the check found it in — it ties the row to the person on the tracker.</summary>
    [ErasedWith(nameof(Vid))]
    public long? SessionId { get; set; }

    /// <summary>The minutes the check found: the session's for a flight, those on the position within the hours for a shift.</summary>
    public int? Minutes { get; set; }

    /// <summary>A flight only: the distance between its two airports in nautical miles, to the tenth — a fact of the leg, found or not.</summary>
    public decimal? DistanceNm { get; set; }

    /// <summary>Whether the check found it: none when the source did not answer.</summary>
    public bool? Verified { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public Visibility Visibility
    {
        get => Visibility.Members;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public int? StakeholderVid => Vid;

    public string ResourceScope => Event.ScopeOf(EventId);
}
