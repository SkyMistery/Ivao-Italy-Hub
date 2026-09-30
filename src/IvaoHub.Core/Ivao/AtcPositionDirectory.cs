using System.Linq.Expressions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The ATC positions of the division, as questions a module asks instead of naming a kind of position: "on which positions
/// is this rating trained?" (M3, A2; design M3 §1.7), which the training's request offers, copying the airport and the FIR
/// of the one chosen (design M3 §2.2, §1.2); and "which positions are there, and which one is this callsign?" (M4, E10c;
/// design M4 §4.1), which the events choose theirs from and copy the FIR of.
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
}

/// <summary>
/// One position, as a module offers it and keeps it: the callsign a controller connects as; its kind as IVAO spells it
/// (<c>TWR</c>, <c>APP</c>, <c>CTR</c>…), which a module hands to <see cref="RatingVocabulary.PreferredFor"/> without naming
/// it; the name said on the frequency; the airport of an airport position; and the FIR — for an airport position, its
/// airport's.
/// </summary>
public sealed record AtcPositionDto(string Callsign, string Type, string Name, string? AirportIcao, string? Fir);

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

        // IVAO's callsigns are upper case (IvaoAtcPositionReader); what a module was sent need not be.
        var wanted = callsigns
            .Where(callsign => !string.IsNullOrWhiteSpace(callsign))
            .Select(callsign => callsign.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (wanted.Length == 0)
        {
            return new Dictionary<string, AtcPositionDto>(StringComparer.OrdinalIgnoreCase);
        }

        return await OfDivision(position => wanted.Contains(position.Callsign))
            .ToDictionaryAsync(position => position.Callsign, StringComparer.OrdinalIgnoreCase, cancellationToken);
    }

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
