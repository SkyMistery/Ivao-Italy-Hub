using System.Linq.Expressions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The ATC positions of the division, as questions a module asks instead of naming a kind of position: "on which positions
/// is this rating trained?" (M3, A2; design M3 §1.7), which the training's request offers, copying the airport and the FIR
/// of the one chosen (design M3 §2.2, §1.2); "which positions are there, and which one is this callsign?" (M4, E10c;
/// design M4 §4.1), which the events choose theirs from and copy the FIR of; and "who may connect there, in these hours?",
/// which is IVAO's FRAs of the position (M4, E10c; design M4 §4.3).
/// </summary>
public interface IAtcPositionDirectory
{
    /// <summary>
    /// The positions of the division of the kind the vocabulary trains <paramref name="rating"/> on
    /// (<see cref="Rating.PositionType"/>: an ADC on the towers, an APC on the approaches, an ACC on the sectors), in the
    /// order of their callsigns; none for a rating trained on no position. The military ones are among them: the training
    /// department uses some, and the positions it does not use are the module's own setting to leave out.
    /// </summary>
    Task<IReadOnlyList<AtcPositionDto>> ForRatingAsync(Rating rating, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every position of the division, of every kind IVAO lists, in the order of their callsigns: what an event's positions
    /// are chosen from (design M4 §4.1). Which ones an event opens is its staff's to say, so the military ones and the ATIS
    /// are among them too.
    /// </summary>
    Task<IReadOnlyList<AtcPositionDto>> OfDivisionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The positions of the division among <paramref name="callsigns"/>, by callsign in any case: what a module checks a
    /// callsign against, and the kind and the FIR it copies (M4, E10c). A callsign IVAO does not list, or one of another
    /// division, is simply absent.
    /// </summary>
    Task<IReadOnlyDictionary<string, AtcPositionDto>> FindAsync(
        IReadOnlyCollection<string> callsigns,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The minimum of each position among <paramref name="callsigns"/>, by callsign in any case: its FRAs on IVAO, which a
    /// module asks for the window of a shift (<see cref="AtcPositionMinimum.Over"/>) — who may be proposed there, and who an
    /// assignment by hand should be warned about (M4, E10c; design M4 §4.3, §4.4). Every callsign asked has one: a position
    /// with no FRA, or not of the division, has none that holds.
    /// </summary>
    Task<IReadOnlyDictionary<string, AtcPositionMinimum>> MinimaAsync(
        IReadOnlyCollection<string> callsigns,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One position, as a module offers it and keeps it: the callsign a controller connects as; its kind as IVAO spells it
/// (<c>TWR</c>, <c>APP</c>, <c>CTR</c>…), which a module hands to <see cref="RatingVocabulary.PreferredFor"/> without naming
/// it; the name said on the frequency; the airport of an airport position; and the FIR — for an airport position, its
/// airport's.
/// </summary>
public sealed record AtcPositionDto(string Callsign, string Type, string Name, string? AirportIcao, string? Fir);

/// <summary>
/// One FRA of a position as its minimum reads it: IVAO's number of the lowest rating that may connect, from a time to a time
/// — past midnight when the end is not after the start — on the days of the week it names (<see cref="IvaoFraReader.DayBit"/>)
/// or on one date. Only the ones the division has switched on become one.
/// </summary>
public sealed record AtcPositionRule(int MinimumRating, int Days, TimeOnly StartsAt, TimeOnly EndsAt, DateOnly? OnDate);

/// <summary>
/// The minimum of one position, as IVAO lets a controller connect to it: the FRAs of the division on that position
/// (M4, E10c; the maintainer's answer on #204). Neither a refusal nor a rule of the hub: who builds the shifts may put
/// somebody below it — the staff lift the FRA for that controller on IVAO — and the hub says so rather than stopping them.
/// </summary>
public sealed class AtcPositionMinimum(IReadOnlyList<AtcPositionRule> rules)
{
    /// <summary>
    /// The words of the warning a module shows when somebody is put below the minimum by hand — lift the FRA for that
    /// controller on IVAO —, a key of the core's language files: they name IVAO, so they live in its perimeter (plan §4.2).
    /// </summary>
    public const string BelowMinimumKey = "atcPositions.belowMinimum";

    /// <summary>A position with no FRA that holds: nobody is kept off it but by the rules of the hub.</summary>
    public static AtcPositionMinimum None { get; } = new([]);

    /// <summary>The FRAs of the position, switched on.</summary>
    public IReadOnlyList<AtcPositionRule> Rules { get; } = rules ?? throw new ArgumentNullException(nameof(rules));

    /// <summary>
    /// IVAO's number of the lowest ATC rating that may connect to the position for the whole of a window — a shift: the
    /// highest minimum among the FRAs that hold at some moment of it, since one who is below it would be refused for that
    /// part. Null when none holds. Compare it with <see cref="RatingVocabulary.IsAtLeast"/>, which says a rating it does not
    /// know is at least nothing. Times in UTC; a window that does not run forward is its first moment.
    /// </summary>
    public int? Over(DateTime fromUtc, DateTime toUtc)
    {
        var end = toUtc > fromUtc ? toUtc : fromUtc.AddTicks(1);
        int? minimum = null;

        foreach (var rule in Rules)
        {
            if (Holds(rule, fromUtc, end) && (minimum is null || rule.MinimumRating > minimum))
            {
                minimum = rule.MinimumRating;
            }
        }

        return minimum;
    }

    /// <summary>
    /// Whether an FRA holds at some moment of the window: on each day it names — its date, or its days of the week — from its
    /// start to its end, the end on the next day when it is not after the start. The day before the window counts too, for an
    /// FRA that started then and runs past midnight into it.
    /// </summary>
    private static bool Holds(AtcPositionRule rule, DateTime fromUtc, DateTime toUtc)
    {
        for (var day = DateOnly.FromDateTime(fromUtc).AddDays(-1); day <= DateOnly.FromDateTime(toUtc); day = day.AddDays(1))
        {
            var named = rule.OnDate is { } date
                ? day == date
                : (rule.Days & IvaoFraReader.DayBit(day.DayOfWeek)) != 0;
            if (!named)
            {
                continue;
            }

            var starts = day.ToDateTime(rule.StartsAt);
            var ends = rule.EndsAt > rule.StartsAt ? day.ToDateTime(rule.EndsAt) : day.AddDays(1).ToDateTime(rule.EndsAt);
            if (starts < toUtc && ends > fromUtc)
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class AtcPositionDirectory(HubDbContext database, IOptions<DivisionOptions> division) : IAtcPositionDirectory
{
    public async Task<IReadOnlyList<AtcPositionDto>> ForRatingAsync(Rating rating, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rating);

        if (rating.PositionType is not { } type)
        {
            return [];
        }

        return await OfDivision(position => position.PositionType == type).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AtcPositionDto>> OfDivisionAsync(CancellationToken cancellationToken = default) =>
        await OfDivision(position => true).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, AtcPositionDto>> FindAsync(
        IReadOnlyCollection<string> callsigns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callsigns);

        var wanted = Normalised(callsigns);
        if (wanted.Length == 0)
        {
            return new Dictionary<string, AtcPositionDto>(StringComparer.OrdinalIgnoreCase);
        }

        return await OfDivision(position => wanted.Contains(position.Callsign))
            .ToDictionaryAsync(position => position.Callsign, StringComparer.OrdinalIgnoreCase, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, AtcPositionMinimum>> MinimaAsync(
        IReadOnlyCollection<string> callsigns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callsigns);

        var wanted = Normalised(callsigns);
        var minima = new Dictionary<string, AtcPositionMinimum>(StringComparer.OrdinalIgnoreCase);
        if (wanted.Length == 0)
        {
            return minima;
        }

        // The snapshot holds the FRAs of the division's country and nobody else's (RefDataSyncJob), switched on or not.
        var rules = await database.IvaoFras.AsNoTracking()
            .Where(fra => fra.IsActive && wanted.Contains(fra.Callsign))
            .Select(fra => new { fra.Callsign, fra.MinimumRating, fra.Days, fra.StartsAt, fra.EndsAt, fra.OnDate })
            .ToListAsync(cancellationToken);

        foreach (var callsign in wanted)
        {
            var own = rules
                .Where(fra => string.Equals(fra.Callsign, callsign, StringComparison.OrdinalIgnoreCase))
                .Select(fra => new AtcPositionRule(fra.MinimumRating, fra.Days, fra.StartsAt, fra.EndsAt, fra.OnDate))
                .ToList();

            minima[callsign] = own.Count == 0 ? AtcPositionMinimum.None : new AtcPositionMinimum(own);
        }

        return minima;
    }

    /// <summary>
    /// The callsigns asked, as IVAO spells them — upper case (IvaoAtcPositionReader) —, each once: what a module was sent
    /// need not be.
    /// </summary>
    private static string[] Normalised(IReadOnlyCollection<string> callsigns) =>
        [.. callsigns
            .Where(callsign => !string.IsNullOrWhiteSpace(callsign))
            .Select(callsign => callsign.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The positions <paramref name="which"/> picks, of the division, the way the airspace is (FirDirectory): an airport
    /// position when its airport is of the country of the division, a sector when its FIR is one of the division's. The
    /// table holds the world, and without this every answer would be the network's.
    /// </summary>
    private IQueryable<AtcPositionDto> OfDivision(Expression<Func<IvaoAtcPosition, bool>> which)
    {
        var country = division.Value.CountryId;

        return
            from position in database.IvaoAtcPositions.AsNoTracking().Where(which)
            join airport in database.IvaoAirports.AsNoTracking() on position.AirportIcao equals airport.Icao into airports
            from airport in airports.DefaultIfEmpty()
            where (position.AirportIcao != null && airport!.CountryId == country)
                || (position.AirportIcao == null
                    && database.IvaoCenters.Any(center => center.Id == position.CenterId && center.CountryId == country))
            orderby position.Callsign
            select new AtcPositionDto(
                position.Callsign,
                position.PositionType,
                position.Name,
                position.AirportIcao,
                position.AirportIcao == null ? position.CenterId : airport!.CenterId);
    }
}
