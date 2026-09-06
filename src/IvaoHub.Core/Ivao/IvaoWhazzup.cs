using System.Globalization;
using System.Text.Json;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// Reading the network's own picture of who is connected. It is one payload of several megabytes
/// and what a page needs out of it is five numbers and a short list, so it is read once, narrowed
/// here, and only the narrow answer is ever kept (design M1 section 6.2).
/// <para>Written apart from the client so that the client and the fixture reader share it: what
/// "in the area" means is a rule, and a rule the two halves could disagree about would show up as
/// a figure that is right in development and wrong in production.</para>
/// <para><b>What "in the area" means</b>, in two lines, because it is a decision and not an
/// implementation detail: a controller is in the area when the station of its callsign — what
/// comes before the first underscore — is one of the division's centres or airports; a pilot is
/// when the flight plan departs from or arrives at one of its airports. Both come from the
/// snapshot of the reference data, so a division that forks gets its own answer without touching
/// a line of this.</para>
/// </summary>
public static class IvaoWhazzup
{
    /// <summary>The endpoint. Public: it is the same picture the network shows on its own site.</summary>
    public const string Path = "/v2/tracker/whazzup";

    /// <summary>How long an answer is kept. Long enough to be one call, short enough to be now.</summary>
    public static readonly TimeSpan Freshness = TimeSpan.FromSeconds(60);

    /// <summary>Narrows the payload to what a page can show. Never throws on a shape it does not know.</summary>
    public static IvaoNetworkStatus Read(JsonElement root, IvaoAirspace airspace)
    {
        ArgumentNullException.ThrowIfNull(airspace);

        if (root.ValueKind != JsonValueKind.Object)
        {
            return IvaoNetworkStatus.Unknown;
        }

        var clients = Property(root, "clients");
        var controllers = Array(clients, "atcs");
        var pilots = Array(clients, "pilots");

        var positions = new List<IvaoNetworkPosition>();
        foreach (var controller in controllers)
        {
            if (Text(controller, "callsign") is not { Length: > 0 } callsign)
            {
                continue;
            }

            var station = IvaoAirspace.StationOf(callsign);
            if (!airspace.Covers(station))
            {
                continue;
            }

            positions.Add(new IvaoNetworkPosition(
                callsign.ToUpperInvariant(),
                station,
                Frequency(Property(controller, "atcSession"))));
        }

        // Ordered by callsign so that two readings a minute apart do not reshuffle the list under
        // somebody who is reading it.
        positions.Sort((left, right) => string.CompareOrdinal(left.Callsign, right.Callsign));

        var areaPilots = 0;
        foreach (var pilot in pilots)
        {
            var plan = Property(pilot, "flightPlan");
            if (airspace.Serves(Icao(plan, "departureId"), Icao(plan, "arrivalId")))
            {
                areaPilots++;
            }
        }

        var connections = Property(root, "connections");

        return new IvaoNetworkStatus(
            UpdatedAt: Instant(root, "updatedAt"),
            NetworkAtc: Number(connections, "atc") ?? controllers.Count,
            NetworkPilots: Number(connections, "pilot") ?? pilots.Count,
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
