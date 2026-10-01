using System.Globalization;
using System.Text.Json;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// Turns IVAO's answers about the bookings of ATC positions into <see cref="AtcBookingDto"/>s (M4, E15a). It lives on its own,
/// like the reader of the tracker, so that the real client and the fixture client read the payload the same way.
/// <para>Measured on 30 September 2026 with the application's token (<c>client_credentials</c> and no scope; without a token,
/// 401): <c>/v2/atc/bookings/daily</c> answers a bare array, 20 to 70 bookings a day for the whole network. A booking is on an
/// airport's position (<c>atcPosition</c>, <c>atcPositionRef</c>) or on a sector (<c>subcenter</c>, <c>subcenterRef</c>) and
/// never both, the callsign the same as the reference's <c>composePosition</c>; <c>user</c> is the member — id, division,
/// names, rating —, of which the hub keeps the id; <c>training</c> is <c>training</c>, <c>exam</c> or null; the times are UTC.
/// </para>
/// </summary>
public static class IvaoAtcBookingReader
{
    /// <summary>
    /// The bookings of a day, in the order IVAO gave them; <see langword="null"/> when the answer is not the bare array the day
    /// is, which is «could not read», not «nobody booked». A row that is not a booking is left out, as one odd row of the
    /// tracker is.
    /// </summary>
    public static IReadOnlyList<AtcBookingDto>? ReadBookings(JsonElement root) =>
        root.ValueKind == JsonValueKind.Array ? [.. root.EnumerateArray().Select(ReadBooking).OfType<AtcBookingDto>()] : null;

    /// <summary>One booking, or <see langword="null"/> for a row without a position, its two times or the member.</summary>
    public static AtcBookingDto? ReadBooking(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || (Text(item, "atcPosition") ?? Text(item, "subcenter")) is not { } callsign
            || Moment(item, "startDate") is not { } startsAt
            || Moment(item, "endDate") is not { } endsAt
            || !item.TryGetProperty("user", out var user)
            || user.ValueKind != JsonValueKind.Object
            || !user.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.Number
            || !id.TryGetInt32(out var vid))
        {
            return null;
        }

        var kind = Text(item, "training")?.ToUpperInvariant() switch
        {
            "EXAM" => AtcBookingKind.Exam,
            "TRAINING" => AtcBookingKind.Training,
            _ => AtcBookingKind.Controlling,
        };

        return new AtcBookingDto(callsign.ToUpperInvariant(), startsAt, endsAt, vid, kind);
    }

    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static DateTime? Moment(JsonElement element, string property) =>
        Text(element, property) is { } text
        && DateTime.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var moment)
            ? moment
            : null;
}
