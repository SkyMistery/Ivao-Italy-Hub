using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Privacy;

namespace IvaoHub.Modules.Events;

/// <summary>
/// A parallel activity of an event in person (design M4 §4-bis.1, E17), <c>evt_activities</c>: a flight simulator, a tour of the tower —
/// something with few places, booked by turns. Its name and its place in every language of the division, its window, how long a turn
/// lasts and how many seats each turn has (two simulators are two seats every thirty minutes).
/// <para>A row of the staff that belongs to its event (<see cref="IEventChild"/>), in the area of the bookings (§4-bis.1, the catalogue
/// §6.1: <c>EventBookings.Edit</c> writes «the activities»; note 2026-10-15-le-attivita-e-la-conservazione-breve, question 1), with the
/// event's care and scope copied at every write, as an airport of the event takes them.</para>
/// <para><b>Its turns are a sum, not rows</b>: from <see cref="FromUtc"/>, one every <see cref="SlotMinutes"/>, while a turn ends within
/// <see cref="ToUtc"/> (<see cref="Turns()"/>). A member's seat names the start of its turn (<see cref="EventActivitySeat"/>).</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.BookingsArea)]
public sealed class EventActivity : IEventChild, IAuditable, IHasResourceScope
{
    /// <summary>The most activities of one event.</summary>
    public const int MaxPerEvent = 20;

    /// <summary>The longest name, and the longest place, in each language.</summary>
    public const int MaxTextLength = 200;

    /// <summary>The shortest and the longest turn, in minutes.</summary>
    public const int MinSlotMinutes = 5;

    public const int MaxSlotMinutes = 480;

    /// <summary>The most seats of one turn.</summary>
    public const int MaxSeatsPerSlot = 100;

    /// <summary>The most turns of one activity: a page of turns, not a timetable of the year.</summary>
    public const int MaxTurns = 200;

    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>What the activity is, in every language of the division: the members read it.</summary>
    public Localized<string> Name { get; set; } = Localized<string>.Empty;

    /// <summary>Where it is, in every language of the division when said: a room, a hangar. None when it is the event's venue.</summary>
    public Localized<string>? Place { get; set; }

    /// <summary>When its first turn starts: inside the event's window.</summary>
    public DateTime FromUtc { get; set; }

    /// <summary>When its last turn ends at the latest: inside the event's window, after it starts.</summary>
    public DateTime ToUtc { get; set; }

    /// <summary>How long a turn lasts.</summary>
    public int SlotMinutes { get; set; }

    /// <summary>How many members take a turn together.</summary>
    public int SeatsPerSlot { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>The event's: a permission granted on one event reaches its activities.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);

    /// <summary>
    /// The starts of the turns of a window: one every <paramref name="slotMinutes"/> from <paramref name="fromUtc"/>, while a turn ends
    /// within <paramref name="toUtc"/> — the turns that fit whole (reading 3). None for a window shorter than a turn, or a turn of no length.
    /// </summary>
    public static IReadOnlyList<DateTime> Turns(DateTime fromUtc, DateTime toUtc, int slotMinutes)
    {
        if (slotMinutes <= 0 || toUtc <= fromUtc)
        {
            return [];
        }

        var length = TimeSpan.FromMinutes(slotMinutes);
        var turns = new List<DateTime>();

        // One past the most, so that a window of too many turns is told from one of just enough, and never a loop of the year.
        for (var start = fromUtc; start + length <= toUtc && turns.Count <= MaxTurns; start += length)
        {
            turns.Add(start);
        }

        return turns;
    }

    /// <summary>The starts of this activity's turns.</summary>
    public IReadOnlyList<DateTime> Turns() => Turns(FromUtc, ToUtc, SlotMinutes);

    /// <summary>Whether <paramref name="startsAtUtc"/> is the start of one of this activity's turns.</summary>
    public bool IsTurn(DateTime startsAtUtc)
    {
        var length = TimeSpan.FromMinutes(SlotMinutes);
        return SlotMinutes > 0
            && startsAtUtc >= FromUtc
            && startsAtUtc + length <= ToUtc
            && (startsAtUtc - FromUtc).Ticks % length.Ticks == 0;
    }
}

/// <summary>
/// A seat a member took in a turn of a parallel activity (design M4 §4-bis.1, E17), <c>evt_activity_bookings</c>: the activity, the start of
/// its turn, the member. Only a member registered to the event takes one, a turn has <see cref="EventActivity.SeatsPerSlot"/> of them —
/// counted under a lock on the activity's row, since a unique index cannot count —, and a member's turns never overlap
/// (<c>Registrations.MemberSeats</c>).
/// <para>A row of a member, as a registration is: sent by them (<see cref="ISubmittedByMembers"/>) and about them
/// (<see cref="IHasStakeholder"/>), in the area of the bookings with the event's departments and scope, so that the staff who read the
/// registrations reach it and nobody else does. Giving it back deletes it (<see cref="WithdrawnByStakeholderAttribute"/>, E10h): <b>a seat
/// carries no decision of the staff about the member</b> — nothing on it is approved, refused or confirmed; the staff only read it and count
/// it —, so deleting it deletes nothing but what the member took (Carmine's rule on #232). It has no words of the member: nothing to mark
/// with <see cref="ErasedWithAttribute"/>.</para>
/// <para>Kept for <c>inPersonRetentionMonths</c> after the event, as the registrations; the seats taken, counted, stay in the statistics.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.BookingsArea)]
[WithdrawnByStakeholder]
public sealed class EventActivitySeat : IEventChild, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope
{
    public long Id { get; set; }

    public long ActivityId { get; set; }

    public long EventId { get; set; }

    /// <summary>The member: whoever the row is about.</summary>
    public int Vid { get; set; }

    /// <summary>The start of the turn: one of the activity's (<see cref="EventActivity.IsTurn"/>).</summary>
    public DateTime StartsAtUtc { get; set; }

    public DateTime CreatedAt { get; set; }

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

    /// <summary>The event's: a permission granted on one event reaches its seats.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
