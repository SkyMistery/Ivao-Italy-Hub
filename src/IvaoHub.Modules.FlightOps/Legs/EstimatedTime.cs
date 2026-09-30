namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>
/// The estimated time of a leg (design M2 §1.5): <c>minutes = 60 × GCD × (1 + k) / speed + c</c>, with <c>k</c> and
/// <c>c</c> from the settings and the speed of the tour's reference aircraft. Computed at every read and never
/// stored, so a new speed or a new pair of numbers changes every tour at once, published ones included.
/// Information for the pilot, never a constraint of a report.
/// </summary>
public static class EstimatedTime
{
    public static int Minutes(decimal distanceNm, int cruiseTasKt, decimal factor, int fixedMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cruiseTasKt);

        return (int)Math.Round((60m * distanceNm * (1 + factor) / cruiseTasKt) + fixedMinutes, MidpointRounding.AwayFromZero);
    }
}
