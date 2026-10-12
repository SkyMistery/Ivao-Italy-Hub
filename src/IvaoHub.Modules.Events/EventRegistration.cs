using IvaoHub.Core.Division;
using IvaoHub.Core.Privacy;

namespace IvaoHub.Modules.Events;

/// <summary>
/// A member's registration to an event in person (design M4 §4-bis.1, E16), <c>evt_registrations</c>: one per member per event —
/// the unique index is the database's —, with their answers to the event's questions, checked by the server by their type.
/// <para>A row of a member, as a booking is: sent by them (<see cref="ISubmittedByMembers"/>) and about them
/// (<see cref="IHasStakeholder"/>), in the area of the bookings with the event's departments and scope, so that the staff who read
/// the registrations reach it and nobody else does (§1.1, §4-bis.2: «visible only to the staff with <c>EventBookings.View</c> and to
/// the member»). The member changes their answers (the core's rule of a row a member sent, T11) and withdraws until the event starts;
/// withdrawing deletes it (<see cref="WithdrawnByStakeholderAttribute"/>, E10h): <b>a registration carries no decision of the staff
/// about the member</b> — nothing on it is approved, refused or confirmed; the staff only read it, sum it and take it away —, so
/// deleting it deletes nothing but what the member sent. The audit of the core keeps who did what.</para>
/// <para>The answers say who comes in person and with what: they are kept for <c>inPersonRetentionMonths</c> after the event (E17),
/// and the sums stay in the statistics.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.BookingsArea)]
[WithdrawnByStakeholder]
public sealed class EventRegistration : IEventChild, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope
{
    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>The member: whoever the row is about.</summary>
    public int Vid { get; set; }

    /// <summary>
    /// The answers, a JSON object by question (<see cref="EventQuestion.AnswerKey"/>): <c>true</c> or <c>false</c>, the key of a
    /// choice, the keys of several, a number, a text. A question nobody answered is not in it. The member's own data: a registration
    /// withdrawn or taken away before their erasure leaves no answer in the audit either (<see cref="ErasedWithAttribute"/>, E10n).
    /// </summary>
    [ErasedWith(nameof(Vid))]
    public string AnswersJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }

    /// <summary>When the member last changed their answers.</summary>
    public DateTime UpdatedAt { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>Any member, as the global filter reads it; which member is the endpoints' business, as for a booking.</summary>
    public Visibility Visibility
    {
        get => Visibility.Members;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public int? StakeholderVid => Vid;

    /// <summary>The event's: a permission granted on one event reaches its registrations.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
