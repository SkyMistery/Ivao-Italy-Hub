using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;

namespace IvaoHub.Modules.Events.Bookings;

/// <summary>
/// The block <c>events.myEvents</c> (design M4 §7.3, E6b): the reader's bookings still to fly, on <c>/me</c> — where a dashboard
/// puts it from the back office, as it puts <c>flightops.myTours</c> —, by the time of their flights, and the way to all of them on
/// <c>/events/mine</c>. «Still to fly» is a flight whose on block is to come: one in the air is still there, one landed is gone. The
/// bookings are <see cref="PilotBookings.MineAsync"/>'s, the same answer <c>/events/mine</c> reads, in the same shape. The shifts of the
/// ATC join them with their phase (E12).
/// <para>Always live and without properties: it is the reader's. A visitor gets <c>signedIn: false</c> and nothing else — the block
/// belongs on <c>/me</c>, and a page that shows it to the public shows a line that asks to sign in.</para>
/// </summary>
public sealed class MyEventsProvider(PilotBookings bookings, ICurrentUser currentUser, IClock clock) : IDataBlockProvider
{
    public const string BlockType = "events.myEvents";

    /// <summary>The shape of <c>GET /api/events/mine/bookings</c>: the browser reads the block and the page with one type.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LocalizedJsonConverterFactory(), new JsonStringEnumConverter() },
    };

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return new JsonObject { ["signedIn"] = false };
        }

        var now = clock.UtcNow;
        var upcoming = (await bookings.MineAsync(cancellationToken)).Where(booking => booking.OnBlockUtc > now).ToList();

        return new JsonObject
        {
            ["signedIn"] = true,
            ["bookings"] = JsonSerializer.SerializeToNode(upcoming, Json),
        };
    }
}
