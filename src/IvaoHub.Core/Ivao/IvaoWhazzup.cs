using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// Reading the network's own picture of who is connected. It is one payload of several megabytes
/// and what a page needs out of it is five numbers and a short list, so it is read once, narrowed
/// here, and only the narrow reading is ever kept (design M1 section 6.2): every controller's
/// callsign and frequency and the two ends of every flight plan, with nobody's name or number in
/// it — what any airspace is counted from (<see cref="IvaoNetworkPicture"/>, E4b).
/// <para>Written apart from the client so that the client and the fixture reader share it: what
/// "in the area" means is a rule, and a rule the two halves could disagree about would show up as
/// a figure that is right in development and wrong in production.</para>
/// <para><b>What "in the area" means</b>, in two lines, because it is a decision and not an
/// implementation detail: a controller is in the area when the station of its callsign — what
/// comes before the first underscore — is one of the division's centres or airports; a pilot is
/// when the flight plan departs from or arrives at one of its airports. Both come from the
/// snapshot of the reference data, so a division that forks gets its own answer without touching
/// a line of this. The airports a screen asks about are another airspace with the same two rules
/// (<see cref="IvaoAirspace.OfAirports"/>).</para>
/// </summary>
public static class IvaoWhazzup
{
    /// <summary>The endpoint. Public: it is the same picture the network shows on its own site.</summary>
    public const string Path = "/v2/tracker/whazzup";

    /// <summary>How long a reading is kept. Long enough to be one call, short enough to be now.</summary>
    public static readonly TimeSpan Freshness = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Narrows the payload to what any airspace is counted from. Never throws on a shape it does not
    /// know.
    /// </summary>
    public static IvaoNetworkPicture Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return IvaoNetworkPicture.Unknown;
        }

        var clients = Property(root, "clients");
        var controllers = Array(clients, "atcs");
        var pilots = Array(clients, "pilots");

        var stations = new List<IvaoNetworkPosition>(controllers.Count);
        foreach (var controller in controllers)
        {
            if (Text(controller, "callsign") is not { Length: > 0 } callsign)
            {
                continue;
            }

            stations.Add(new IvaoNetworkPosition(
                callsign.ToUpperInvariant(),
                IvaoAirspace.StationOf(callsign),
                Frequency(Property(controller, "atcSession"))));
        }

        // Ordered by callsign so that two readings a minute apart do not reshuffle the list under
        // somebody who is reading it.
        stations.Sort((left, right) => string.CompareOrdinal(left.Callsign, right.Callsign));

        // The two ends and nothing else: a flight with neither is in no airspace, and is not kept.
        var flights = new List<IvaoFlightEnds>(pilots.Count);
        foreach (var pilot in pilots)
        {
            var plan = Property(pilot, "flightPlan");
            var ends = new IvaoFlightEnds(Icao(plan, "departureId"), Icao(plan, "arrivalId"));
            if (ends.Departure is not null || ends.Arrival is not null)
            {
                flights.Add(ends);
            }
        }

        var connections = Property(root, "connections");

        return new IvaoNetworkPicture(
            updatedAt: Instant(root, "updatedAt"),
            networkAtc: Number(connections, "atc") ?? controllers.Count,
            networkPilots: Number(connections, "pilot") ?? pilots.Count,
            controllers: stations,
            flights: flights);
    }

    /// <summary>
    /// What "in the area" means, counted on a reading: the controllers whose station the airspace
    /// covers, in the reading's order, and the flights it serves.
    /// </summary>
    internal static IvaoNetworkStatus Count(IvaoNetworkPicture picture, IvaoAirspace airspace)
    {
        var positions = picture.Controllers.Where(controller => airspace.Covers(controller.Station)).ToList();
        var areaPilots = picture.Flights.Count(flight => airspace.Serves(flight.Departure, flight.Arrival));

        return new IvaoNetworkStatus(
            UpdatedAt: picture.UpdatedAt,
            NetworkAtc: picture.NetworkAtc,
            NetworkPilots: picture.NetworkPilots,
            AreaAtc: positions.Count,
            AreaPilots: areaPilots,
            Positions: positions);
    }

    private static JsonElement? Property(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } owner && owner.TryGetProperty(name, out var value)
            ? value
            : null;

    private static List<JsonElement> Array(JsonElement? element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.Array } array
            ? [.. array.EnumerateArray()]
            : [];

    private static string? Text(JsonElement? element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.String } value
            ? value.GetString()?.Trim()
            : null;

    private static string? Icao(JsonElement? element, string name) =>
        Text(element, name) is { Length: > 0 } icao ? icao.ToUpperInvariant() : null;

    private static int? Number(JsonElement? element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number)
            ? number
            : null;

    private static DateTime? Instant(JsonElement? element, string name) =>
        Text(element, name) is { Length: > 0 } raw
            && DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var instant)
            ? instant
            : null;

    /// <summary>
    /// The frequency as a page shows it. The network sends megahertz as a number, and three
    /// decimals is how a controller reads one out loud.
    /// </summary>
    private static string? Frequency(JsonElement? session)
    {
        if (Property(session, "frequency") is not { ValueKind: JsonValueKind.Number } value
            || !value.TryGetDouble(out var megahertz))
        {
            return null;
        }

        return megahertz.ToString("0.000", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// One reading of who is connected, narrowed to what any airspace is counted from: the network's
/// two totals, every controller online and the two ends of every flight plan. It is what the client
/// keeps for a minute, <b>one for every airspace that asks</b> (E4b): since a screen names the
/// airports it wants counted, the airspaces are as many as anybody cares to invent, and a reading
/// for each would turn every one of them into a download of the whole payload.
/// <para>Each airspace is counted from it once (<see cref="For"/>), by its
/// <see cref="IvaoAirspace.CacheKey"/>, and its answer goes when the reading goes: no answer is
/// ever older than the reading it came from.</para>
/// </summary>
public sealed class IvaoNetworkPicture
{
    private readonly ConcurrentDictionary<string, IvaoNetworkStatus> _answers = new(StringComparer.Ordinal);

    internal IvaoNetworkPicture(
        DateTime? updatedAt,
        int networkAtc,
        int networkPilots,
        IReadOnlyList<IvaoNetworkPosition> controllers,
        IReadOnlyList<IvaoFlightEnds> flights)
    {
        UpdatedAt = updatedAt;
        NetworkAtc = networkAtc;
        NetworkPilots = networkPilots;
        Controllers = controllers;
        Flights = flights;
    }

    /// <summary>What a caller gets when the network cannot be reached. Never an exception.</summary>
    public static IvaoNetworkPicture Unknown { get; } = new(null, 0, 0, [], []);

    /// <summary>When the network says it counted. Null when the answer could not be had.</summary>
    internal DateTime? UpdatedAt { get; }

    /// <summary>Controllers connected anywhere.</summary>
    internal int NetworkAtc { get; }

    /// <summary>Pilots connected anywhere.</summary>
    internal int NetworkPilots { get; }

    /// <summary>Every controller online, by callsign.</summary>
    internal IReadOnlyList<IvaoNetworkPosition> Controllers { get; }

    /// <summary>Where every flight plan starts and ends, and nothing else of the pilot.</summary>
    internal IReadOnlyList<IvaoFlightEnds> Flights { get; }

    /// <summary>Who of this reading is in the airspace, counted the first time an airspace asks.</summary>
    public IvaoNetworkStatus For(IvaoAirspace airspace)
    {
        ArgumentNullException.ThrowIfNull(airspace);

        // Nothing was read. Every failure shares this one reading, so it keeps no answer: it would
        // keep them for as long as the process lives.
        if (ReferenceEquals(this, Unknown))
        {
            return IvaoNetworkStatus.Unknown;
        }

        return _answers.GetOrAdd(airspace.CacheKey, _ => IvaoWhazzup.Count(this, airspace));
    }
}

/// <summary>The two ends of a flight plan, upper case; either may be missing.</summary>
internal readonly record struct IvaoFlightEnds(string? Departure, string? Arrival);
