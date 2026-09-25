using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The ATC positions of the division, as a question a module asks about a rating instead of naming a kind of position
/// (M3, A2; design M3 §1.7): "on which positions is this rating trained?". The training's request offers them, and copies
/// the airport and the FIR of the one chosen (design M3 §2.2, §1.2).
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
}

/// <summary>
/// One position, as a module offers it and a training keeps it: the callsign a controller connects as, the name said on
/// the frequency, the airport of an airport position, and the FIR — for an airport position, its airport's.
/// </summary>
public sealed record AtcPositionDto(string Callsign, string Name, string? AirportIcao, string? Fir);

internal sealed class AtcPositionDirectory(HubDbContext database, IOptions<DivisionOptions> division) : IAtcPositionDirectory
{
    public async Task<IReadOnlyList<AtcPositionDto>> ForRatingAsync(Rating rating, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rating);

        if (rating.PositionType is not { } type)
        {
            return [];
        }

        // Of the division, the way the airspace is (FirDirectory): an airport position when its airport is of the
        // country of the division, a sector when its FIR is one of the division's. The table holds the world, and
        // without this the answer would be every tower on the network.
        var country = division.Value.CountryId;

        return await (
            from position in database.IvaoAtcPositions.AsNoTracking()
            join airport in database.IvaoAirports.AsNoTracking() on position.AirportIcao equals airport.Icao into airports
            from airport in airports.DefaultIfEmpty()
            where position.PositionType == type
                && ((position.AirportIcao != null && airport!.CountryId == country)
                    || (position.AirportIcao == null
                        && database.IvaoCenters.Any(center => center.Id == position.CenterId && center.CountryId == country)))
            orderby position.Callsign
            select new AtcPositionDto(
                position.Callsign,
                position.Name,
                position.AirportIcao,
                position.AirportIcao == null ? position.CenterId : airport!.CenterId))
            .ToListAsync(cancellationToken);
    }
}
