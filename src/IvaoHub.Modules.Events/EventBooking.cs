using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>
/// A pilot's booking of a slot (design M4 §1.6), <c>evt_bookings</c>: one slot, one booking — the unique index on the slot is the
/// database's, so two pilots booking the same slot in the same instant meet there and one of them wins (§10.1). Born whole in E6a,
/// with the columns the later phases use: the flight of a private slot (E7), the check after the event (E13a), the staff's excuse
/// of a booking not flown (E13b) and the reminder of the day before (E6b).
/// <para>A row of a member: sent by the pilot (<see cref="ISubmittedByMembers"/>) and about them (<see cref="IHasStakeholder"/>),
/// in the area of the bookings with the event's departments and scope, so that the staff of the bookings reach it and nobody else
/// does (§1.1) — the event's care as it is when the staff act, not as it was on the day of the booking: «take away» copies it
/// first, as every row of an event does (<see cref="IEventChild"/>). Any member reads it through the global filter
/// (<see cref="Visibility.Members"/>), never a visitor; which member is
/// the endpoints' business — a pilot reads their own, the staff the event's — and nothing gives a member the area's
/// <c>View</c> (no <c>IHasParticipants</c>). Withdrawing deletes it (§3.6) — the pilot takes back what they sent, which the write
/// guard of the core lets them do because the entity says so (<see cref="WithdrawnByStakeholderAttribute"/>, E10h) —, and the audit
/// of the core keeps who did what.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.BookingsArea)]
[WithdrawnByStakeholder]
public sealed class EventBooking : IEventChild, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope
{
    /// <summary>The longest note of the staff on a booking not flown (E13b), and the longest reason a booking is taken away with.</summary>
    public const int MaxNoteLength = 1000;

    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>The slot booked: one booking each, as the unique index says.</summary>
    public long SlotId { get; set; }

    /// <summary>The pilot: whoever the row is about.</summary>
    public int BookerVid { get; set; }

    /// <summary>
    /// The aircraft type the pilot flies: one of those the slot allows, for a public slot; the one they declare, for a private
    /// one (E7). An ICAO code the core knows, upper case.
    /// </summary>
    public string AircraftIcao { get; set; } = string.Empty;

    /// <summary>A private slot only (E7): the callsign the pilot flies with — a public slot has its own.</summary>
    public string? Callsign { get; set; }

    /// <summary>A private slot only (E7): the other airport of the flight, the event's being the slot's.</summary>
    public string? OtherIcao { get; set; }

    /// <summary>A private slot only (E7): the time at the other airport — the off block of an arrival, the on block of a departure.</summary>
    public DateTime? OtherTimeUtc { get; set; }

    /// <summary>A private arrival only (E7): the departure booked with it from the same airport, so that both get the same gate.</summary>
    public long? PairedBookingId { get; set; }

    /// <summary>When the flight was found after the event (E13a); empty while not found.</summary>
    public DateTime? FlownAt { get; set; }

    /// <summary>The session of the network the flight was found in (E13a).</summary>
    public long? FlownSessionId { get; set; }

    /// <summary>When the check after the event looked at this booking (E13a): the job decides from it, not from the hour.</summary>
    public DateTime? FlownCheckedAt { get; set; }

    /// <summary>The VID of the staff member who took a booking not flown out of the pilot's record (E13b).</summary>
    public int? UnflownExcusedBy { get; set; }

    /// <summary>Why (E13b): a network failure, a fault of the hub.</summary>
    public string? UnflownExcusedNote { get; set; }

    /// <summary>When the reminder of the day before left (E6b): once per booking.</summary>
    public DateTime? RemindedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>
    /// Any member, as the global filter reads it: a visitor never reads a booking — the page of an event tells them only whether a
    /// slot is taken (§7.1).
    /// </summary>
    public Visibility Visibility
    {
        get => Visibility.Members;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public int? StakeholderVid => BookerVid;

    /// <summary>The event's: a permission granted on one event reaches its bookings, as it reaches its slots.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
