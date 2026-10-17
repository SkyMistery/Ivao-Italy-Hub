namespace IvaoHub.Modules.Events;

/// <summary>
/// When the rows of the pilots and the availabilities of the controllers leave an event (design M4 §11, E15b; notes
/// 2026-09-29-i-dati-dei-membri-negli-eventi §2.3 and 2026-10-17-la-conservazione-di-m4b): the one place that says it, so that
/// <c>events-retention</c>, the statistics and the register of the pilots read the same dates. The short retention of an event in person is
/// <see cref="Registrations.InPersonRetention"/>'s (E17).
/// </summary>
public static class EventRetention
{
    /// <summary>
    /// The earliest end of an event whose pilots' rows are still kept at <paramref name="now"/>: <paramref name="months"/> months before
    /// it. The register of the pilots counts the bookings of the events that ended since (E13b), and the retention deletes those of the
    /// events that ended before (<see cref="PilotsDue"/>): the one boundary, so that a booking leaves the register when it leaves the
    /// database, and never one before the other.
    /// </summary>
    public static DateTime PilotsKeptSince(DateTime now, int months) => now.AddMonths(-months);

    /// <summary>
    /// Whether the bookings and the pilots' reports of support of an event go (§11: <c>pilotRetentionMonths</c> after its end): it ended
    /// before <see cref="PilotsKeptSince"/>, so it is over too.
    /// </summary>
    public static bool PilotsDue(Event row, int months, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.EndsAtUtc < PilotsKeptSince(now, months);
    }

    /// <summary>
    /// Whether the availabilities of an event go (§11: «at the roster published and the event over»): <b>the event is over</b>
    /// (<see cref="EventState.IsOver"/>). An event over has had its roster published, or never will — <c>events-roster</c> publishes no event
    /// that ended (E12) —, and from then on nothing reads them: the shifts are the roster, and the statistics keep how many there were
    /// (reading 1 of the note).
    /// </summary>
    public static bool AvailabilitiesDue(Event row, DateTime now) => EventState.IsOver(row, now);
}
