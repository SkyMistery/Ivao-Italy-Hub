using System.Globalization;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// Where the network shows a member's public profile, by VID: the one place the hub writes that address (note
/// 2026-10-17-il-motto-e-chi-ha-costruito-il-sito). It names IVAO, so it lives here and not in a screen or in
/// <c>division.json</c>; whoever needs the link — today the footer's "Built by", through the bootstrap — asks for it here.
/// </summary>
public static class IvaoMemberProfile
{
    /// <summary>The public profile of the member with this VID.</summary>
    public static string Url(int vid) =>
        string.Create(CultureInfo.InvariantCulture, $"https://www.ivao.aero/Member.aspx?Id={vid}");
}
