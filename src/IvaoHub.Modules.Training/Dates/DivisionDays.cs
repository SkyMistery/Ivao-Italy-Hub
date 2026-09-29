namespace IvaoHub.Modules.Training.Dates;

/// <summary>
/// Days as the division counts them (design M3 §1.2, §2.5): in its own time zone, each from its first moment. A session shows as
/// held from the day after its own, and a date is checked against what happens on the days it touches — both counted here, so
/// that the list of the staff and the warnings of a date agree on when a day begins.
/// </summary>
public static class DivisionDays
{
    /// <summary>The day an instant falls on in the zone.</summary>
    public static DateOnly Of(DateTime utc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
    }

    /// <summary>
    /// The first moment of a day in the zone, in UTC. A day that begins in a gap of the clock begins at its first moment that
    /// exists.
    /// </summary>
    public static DateTime Begins(DateOnly day, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var midnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddMinutes(1);
        }

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(midnight, zone), DateTimeKind.Utc);
    }

    /// <summary>
    /// The whole days a moment touches in the zone, as the first moment of the first of them and the first moment of the day after
    /// the last, in UTC. A moment that ends at a midnight does not touch the day it ends on; one without an end touches its day.
    /// </summary>
    public static (DateTime From, DateTime To) Touched(DateTime startsAtUtc, DateTime? endsAtUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var last = endsAtUtc is { } end && end > startsAtUtc ? end.AddTicks(-1) : startsAtUtc;

        return (Begins(Of(startsAtUtc, zone), zone), Begins(Of(last, zone).AddDays(1), zone));
    }
}
