using System.Globalization;
using System.Text.RegularExpressions;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// What a public slot says, read the same way from a row of the sheet and from the form of one slot (design M4 §1.5, §3.1, E5):
/// the aircraft types written <c>A320/A20N</c>, an instant written in UTC, a code of the network. Pure, and tested as such.
/// </summary>
public static partial class SlotValues
{
    /// <summary>
    /// The aircraft types of a cell, separated by <c>/</c> as the sheet writes them: trimmed, upper case, each once, in their
    /// order. Whether each is a type the core knows is the load's to ask.
    /// </summary>
    public static IReadOnlyList<string> AircraftTypes(string? cell) =>
        [
            .. (cell ?? string.Empty)
                .Split('/')
                .Select(type => type.Trim().ToUpperInvariant())
                .Where(type => type.Length > 0)
                .Distinct(StringComparer.Ordinal),
        ];

    /// <summary>
    /// An instant as the sheet writes it: <c>2026-10-17 14:30</c>, with a <c>T</c> or a space, seconds if written, and a <c>Z</c>
    /// if written — always in UTC, as every moment of the hub. Null for anything else: a spreadsheet's day first or month first
    /// is not guessed (Carmine for the legs of the tours, M2: the file is cleaned, not guessed).
    /// </summary>
    public static DateTime? Instant(string? cell)
    {
        var match = InstantPattern().Match(cell?.Trim() ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        var seconds = match.Groups["seconds"].Success ? match.Groups["seconds"].Value : "00";
        var text = $"{match.Groups["day"].Value} {match.Groups["hours"].Value}:{match.Groups["minutes"].Value}:{seconds}";

        return DateTime.TryParseExact(
            text,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var instant)
            ? DateTime.SpecifyKind(instant, DateTimeKind.Utc)
            : null;
    }

    /// <summary>An airport as the network writes it: four letters or digits, upper case.</summary>
    public static bool IsAirport(string code) => AirportPattern().IsMatch(code);

    /// <summary>An aircraft type as the network writes it: two to four letters or digits, upper case.</summary>
    public static bool IsAircraftType(string code) => AircraftTypePattern().IsMatch(code);

    /// <summary>A callsign: letters and digits, upper case — a space or a dash is no callsign a pilot can connect with.</summary>
    public static bool IsCallsign(string code) => CallsignPattern().IsMatch(code);

    [GeneratedRegex(@"^(?<day>\d{4}-\d{2}-\d{2})[T ](?<hours>\d{2}):(?<minutes>\d{2})(?::(?<seconds>\d{2}))?Z?$")]
    private static partial Regex InstantPattern();

    [GeneratedRegex("^[A-Z0-9]{4}$")]
    private static partial Regex AirportPattern();

    [GeneratedRegex("^[A-Z0-9]{2,4}$")]
    private static partial Regex AircraftTypePattern();

    [GeneratedRegex("^[A-Z0-9]+$")]
    private static partial Regex CallsignPattern();
}

/// <summary>Where a public slot is, read off its two airports (design M4 §1.5): an airport of the event, and whether it arrives there.</summary>
public sealed record SlotDirection(string EventAirportIcao, bool IsArrival)
{
    /// <summary>
    /// A departure from an airport of the event; else an arrival at one; else nowhere — a slot is always at an airport of its
    /// event (§1.5), so a flight between two other airports is no slot of it. A flight between two airports of the event is the
    /// departure from the first: its time there is the off block the pilot books by.
    /// </summary>
    public static SlotDirection? Of(string departureIcao, string arrivalIcao, IReadOnlyCollection<string> eventAirports)
    {
        ArgumentNullException.ThrowIfNull(eventAirports);

        return eventAirports.Contains(departureIcao, StringComparer.Ordinal) ? new SlotDirection(departureIcao, IsArrival: false)
            : eventAirports.Contains(arrivalIcao, StringComparer.Ordinal) ? new SlotDirection(arrivalIcao, IsArrival: true)
            : null;
    }
}

/// <summary>
/// One leg of a rotation as the chains read it: <paramref name="Key"/> is the caller's — the row of the sheet, the slot of the
/// form, a slot already stored —, <paramref name="IsNew"/> whether it is being written now, and so whether a refusal can land on it.
/// </summary>
public sealed record ChainLeg(
    object Key,
    bool IsNew,
    string Rotation,
    int? Leg,
    string DepartureIcao,
    string ArrivalIcao,
    DateTime OffBlockUtc,
    DateTime OnBlockUtc);

/// <summary>A refusal of a leg: the column of the sheet it is about, and its i18n key.</summary>
public sealed record ChainProblem(ChainLeg Leg, string Column, string Key);

/// <summary>What the chains found: the refusals, and the place each leg written without one takes.</summary>
public sealed record ChainCheck(IReadOnlyList<ChainProblem> Problems, IReadOnlyDictionary<object, int> Assigned);

/// <summary>
/// The rotations (design M4 §3.1, note 2026-09-29-gli-slot-e-le-prenotazioni §2.2–§2.3): a chain of legs, each leaving from where
/// the one before it arrived, in the order of their places, each one leaving at least <c>bookingGapMinutes</c> after the one
/// before it arrived — so that a whole rotation can be booked by one pilot (§3.5). «Every two legs an airport of the event» needs
/// no rule of its own: every slot is at an airport of its event (<see cref="SlotDirection"/>), so a chain touches one at every leg.
/// <para>A rotation is every slot with its code: those stored and staying, and those being written. Its legs are in the order of
/// their places; written without places (all of them, and none stored yet), they take them from their times. A refusal lands on a
/// leg being written, never on one already stored.</para>
/// </summary>
public static class SlotChains
{
    public static ChainCheck Check(IReadOnlyList<ChainLeg> legs, int gapMinutes)
    {
        ArgumentNullException.ThrowIfNull(legs);

        var problems = new List<ChainProblem>();
        var assigned = new Dictionary<object, int>();
        var gap = TimeSpan.FromMinutes(gapMinutes);

        foreach (var rotation in legs.GroupBy(leg => leg.Rotation, StringComparer.Ordinal))
        {
            var chain = rotation.ToList();
            var unplaced = chain.Where(leg => leg.Leg is null).ToList();

            if (unplaced.Count == chain.Count)
            {
                // No place written anywhere: the order of the times is the order of the chain.
                var place = 1;
                foreach (var leg in chain.OrderBy(leg => leg.OffBlockUtc).ThenBy(leg => leg.OnBlockUtc))
                {
                    assigned[leg.Key] = place++;
                }
            }
            else if (unplaced.Count > 0)
            {
                // Some written, some not: which place a leg without one takes would be a guess.
                problems.AddRange(unplaced.Where(leg => leg.IsNew).Select(leg => new ChainProblem(leg, SlotColumns.Leg, "events:errors.legMissing")));
                continue;
            }

            var placed = chain.Select(leg => (Leg: leg, Place: leg.Leg ?? assigned[leg.Key])).ToList();
            var twice = placed
                .GroupBy(entry => entry.Place)
                .Where(group => group.Count() > 1)
                .SelectMany(group => group.Skip(group.Any(entry => !entry.Leg.IsNew) ? 0 : 1))
                .Where(entry => entry.Leg.IsNew)
                .ToList();

            if (twice.Count > 0)
            {
                problems.AddRange(twice.Select(entry => new ChainProblem(entry.Leg, SlotColumns.Leg, "events:errors.legTwice")));
                continue;
            }

            var ordered = placed.OrderBy(entry => entry.Place).Select(entry => entry.Leg).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                if (Between(ordered[index - 1], ordered[index], gap) is { } problem)
                {
                    problems.Add(problem);
                }
            }
        }

        return new ChainCheck(problems, assigned);
    }

    /// <summary>
    /// What is wrong between a leg and the next: the next does not leave from where the first arrived, leaves before it, or leaves
    /// too soon after it arrived. On the next leg when it is being written, else on the first; between two stored legs, nothing.
    /// </summary>
    private static ChainProblem? Between(ChainLeg before, ChainLeg after, TimeSpan gap)
    {
        if (!after.IsNew && !before.IsNew)
        {
            return null;
        }

        if (!string.Equals(before.ArrivalIcao, after.DepartureIcao, StringComparison.Ordinal))
        {
            return after.IsNew
                ? new ChainProblem(after, SlotColumns.DepartureIcao, "events:errors.chainBroken")
                : new ChainProblem(before, SlotColumns.ArrivalIcao, "events:errors.chainBroken");
        }

        var key = after.OffBlockUtc < before.OffBlockUtc ? "events:errors.chainOrder"
            : after.OffBlockUtc - before.OnBlockUtc < gap ? "events:errors.chainTooClose"
            : null;

        return key is null ? null
            : after.IsNew ? new ChainProblem(after, SlotColumns.OffBlockUtc, key)
            : new ChainProblem(before, SlotColumns.OnBlockUtc, key);
    }
}
