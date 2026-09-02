using System.Globalization;
using System.Text.Json;

namespace IvaoHub.Core.Auth;

/// <summary>
/// Reads the IVAO user info payload (<c>/v2/users/me</c>) into the handful of fields the hub keeps.
/// <para>Everything is read defensively: a field that changes shape or disappears must cost a null,
/// never an exception during a login. Getting a field name wrong does not throw, it silently
/// removes an identity or a role, which is why nothing here is inferred from the profile as a whole:
/// only the named fields are taken, and the rest of the payload is dropped.</para>
/// <para>The field names were measured against the real payload on 3 September 2026, not guessed.
/// The payload also carries an email address, which is deliberately not read: the hub keeps the
/// minimum IVAO data it needs (plan section 6.4).</para>
/// </summary>
public static class IvaoUserProfileReader
{
    /// <summary>Placeholder IVAO gives to members who never chose a nickname; it is not a name.</summary>
    private static string Placeholder(int vid) => $"User {vid}";

    public static IvaoUserProfile? Read(JsonElement userInfo)
    {
        var vid = Number(userInfo, "id") ?? Number(userInfo, "sub");
        if (vid is null)
        {
            return null;
        }

        var nickname = Text(userInfo, "publicNickname") ?? Text(userInfo, "nickname");
        if (string.Equals(nickname, Placeholder(vid.Value), StringComparison.OrdinalIgnoreCase))
        {
            nickname = null;
        }

        return new IvaoUserProfile(
            Vid: vid.Value,
            // firstName and lastName only arrive with the "profile" scope. Without it IVAO answers
            // all the same, minus those two fields, and the site ends up showing a bare VID.
            FirstName: Text(userInfo, "firstName") ?? Text(userInfo, "given_name") ?? string.Empty,
            LastName: Text(userInfo, "lastName") ?? Text(userInfo, "family_name") ?? string.Empty,
            PublicNickname: nickname,
            DivisionCode: Text(userInfo, "divisionId") ?? Text(userInfo, "division"),
            CountryId: Text(userInfo, "countryId") ?? Text(userInfo, "country"),
            RatingAtc: Rating(userInfo, "atcRating"),
            RatingPilot: Rating(userInfo, "pilotRating"),
            // IVAO does not expose a Discord id here. Measured against the real payload on
            // 3 September 2026, with the "discord" scope granted: the fields are id, firstName,
            // lastName, centerId, countryId, createdAt, divisionId, isStaff, isSupervisor,
            // languageId, email, familyProfile, rating, gcas, hours, userStaffPositions,
            // userStaffDetails, prCreator, ownedVirtualAirlines, groups, sub, given_name,
            // family_name, nickname, publicNickname and profile. The column stays, empty, for
            // whenever the hub links Discord itself the way the division's own bot does.
            DiscordId: null,
            StaffPositions: StaffPositions(userInfo));
    }

    /// <summary>
    /// Only the position codes. IVAO sends an array of objects carrying the whole org chart, about
    /// 1.5 kB for two assignments, of which two strings are useful. <c>id</c> is read, with
    /// <c>connectAs</c> as a fallback.
    /// </summary>
    public static IReadOnlyList<string> StaffPositions(JsonElement userInfo)
    {
        if (!userInfo.TryGetProperty("userStaffPositions", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var codes = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            var code = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString()?.Trim(),
                JsonValueKind.Object => Text(item, "id") ?? Text(item, "connectAs"),
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(code))
            {
                codes.Add(code);
            }
        }

        return codes;
    }

    /// <summary>
    /// A rating out of the <c>rating</c> object, read either as a number or as an object with an
    /// <c>id</c>. An unexpected shape is worth null rather than a failed login.
    /// </summary>
    private static int? Rating(JsonElement root, string property)
    {
        if (root.TryGetProperty("rating", out var rating) && rating.ValueKind == JsonValueKind.Object)
        {
            root = rating;
        }

        if (!root.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var number) ? number : null,
            JsonValueKind.Object => Number(value, "id"),
            JsonValueKind.String => int.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            _ => null,
        };
    }

    private static string? Text(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static int? Number(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt32(out var number) ? number : null,
            JsonValueKind.String => int.TryParse(element.GetString(), CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            _ => null,
        };
    }
}
