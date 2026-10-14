using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The numbers of an event (design M4 §1.10), <c>evt_event_stats</c>: one row per event, numbers only, kept for good (c3) — the
/// slots, the bookings and what was flown, the ATC, the reports of support, the signals of award, and for an event in person the
/// registrations, the sums of their answers and the seats taken in the activities. Worked out after the event, and again whenever
/// the staff ask (§5.1).
/// <para>Born whole with E16 (plan «Parallelismo possibile»: M4c came before M4b), with every column of §1.10, so that no later
/// phase migrates it. Nothing writes it yet: the retention of E17 writes the sums of an event in person before its registrations
/// go, and E13a the rest after the event. The sums are the ones the staff read live (<c>Registrations.RegistrationSums</c>).</para>
/// <para>Numbers only: no column names a person. In the area of the event, with its care and scope, so that whoever reads the event
/// in the back office reads its statistics (§6.1, <c>Events.View</c>).</para>
/// </summary>
[PermissionArea(EventsPermissions.Area)]
public sealed class EventStats : IEventChild, IHasResourceScope
{
    public long Id { get; set; }

    /// <summary>The event; one row each.</summary>
    public long EventId { get; set; }

    public int PublicSlots { get; set; }

    public int PrivateSlots { get; set; }

    /// <summary>Slots booked.</summary>
    public int Booked { get; set; }

    /// <summary>Bookings flown (§5.1).</summary>
    public int BookedFlown { get; set; }

    /// <summary>Bookings not flown (§3.7).</summary>
    public int BookedUnflown { get; set; }

    /// <summary>Flights to or from the event's airports in its window without a booking: never stored by VID, only counted (§11).</summary>
    public int FlownUnbooked { get; set; }

    /// <summary>Availabilities the controllers gave (§4.2).</summary>
    public int AtcAvailabilities { get; set; }

    /// <summary>Shifts of the roster (§4.3).</summary>
    public int AtcShifts { get; set; }

    /// <summary>Shifts controlled (§4.5).</summary>
    public int AtcAttended { get; set; }

    public int AtcNoShows { get; set; }

    public int AtcExcused { get; set; }

    /// <summary>Reports of support sent (§5.2).</summary>
    public int ReportsSubmitted { get; set; }

    public int ReportsAccepted { get; set; }

    /// <summary>Signals of award raised (§5.4).</summary>
    public int AwardSignals { get; set; }

    /// <summary>An event in person: the members registered.</summary>
    public int Registrations { get; set; }

    /// <summary>An event in person: the sums of the answers, by question, as a JSON array (<c>Registrations.QuestionSum</c>).</summary>
    public string AnswerSumsJson { get; set; } = "[]";

    /// <summary>An event in person: the seats taken, by activity, as a JSON object of the activity's id and its seats (E17).</summary>
    public string ActivitySeatsJson { get; set; } = "{}";

    /// <summary>When the numbers were last worked out.</summary>
    public DateTime ComputedAt { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>The event's.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
