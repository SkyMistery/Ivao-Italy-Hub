using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth;

/// <summary>
/// A person who has logged in at least once: there is no IVAO endpoint that lists the staff of a
/// division, so the roster of the hub is exactly this table (plan section 16.13).
/// </summary>
[Audited]
public sealed class HubUser
{
    /// <summary>IVAO VID. Natural key, never a surrogate one.</summary>
    public int Vid { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PublicNickname { get; set; }

    public string? DivisionCode { get; set; }

    public string? Country { get; set; }

    public int? RatingAtc { get; set; }

    public int? RatingPilot { get; set; }

    /// <summary>
    /// Hours connected to the network as a controller, from the IVAO profile at every sign in, like the ratings (M3, A1):
    /// what the training compares with its thresholds. IVAO counts seconds; the reader turns them into hours and nothing
    /// else has to know. Null until the member signs in after it was added.
    /// </summary>
    public decimal? HoursAtc { get; set; }

    /// <summary>Hours connected as a pilot, the same way as <see cref="HoursAtc"/>.</summary>
    public decimal? HoursPilot { get; set; }

    /// <summary>
    /// The Discord account the member proved is theirs (<see cref="DiscordLinks"/>): fixed, unique, null when not linked. IVAO
    /// does not send it. Kept out of the audit (issue #326, item 2), as the address below: a person's datum, not a decision.
    /// </summary>
    [NotAudited]
    public string? DiscordId { get; set; }

    /// <summary>
    /// The account's username when the member linked it, shown to them on their profile and to nobody else. It changes on
    /// Discord without the hub knowing, until they link again.
    /// </summary>
    [NotAudited]
    public string? DiscordUsername { get; set; }

    /// <summary>When the member said yes, last.</summary>
    [NotAudited]
    public DateTime? DiscordLinkedAt { get; set; }

    /// <summary>
    /// Where the hub writes to this member, taken from their IVAO profile at every sign in.
    /// <para>It exists for the notification service and for nothing else (decision note of
    /// 6 September 2026): no DTO carries it, no screen shows it, and the staff directory of G9 is
    /// explicitly not allowed to. Null for anybody who has not signed in since it was added, which
    /// the queue treats as "cannot be reached" rather than as an error.</para>
    /// <para>Nor the audit (issue #326, item 2): every holder of <c>Audit.View</c> read it there.</para>
    /// </summary>
    [NotAudited]
    public string? Email { get; set; }

    /// <summary>Preferred language; the SPA falls back to the cookie and then to the browser.</summary>
    public string? Locale { get; set; }

    /// <summary>True as soon as at least one staff position of the division was recognised.</summary>
    public bool IsStaff { get; set; }

    /// <summary>
    /// What IVAO itself calls staff, kept as it comes. It is wider than <see cref="IsStaff"/>,
    /// because it counts headquarters and other divisions, so it never feeds a permission: it is
    /// there for the staff directory of M1. Null until the member has logged in.
    /// </summary>
    public bool? IvaoIsStaff { get; set; }

    /// <summary>Whether IVAO marks the member as a supervisor. Same rule: recorded, never used to decide.</summary>
    public bool? IvaoIsSupervisor { get; set; }

    /// <summary>The truth about super administrators; <c>division.json</c> only bootstraps it.</summary>
    public bool IsSuperadmin { get; set; }

    /// <summary>
    /// Changed whenever grants or the super administrator flag change, so the cookie is rejected. Not in the audit: the
    /// change it follows is, and the stamp itself is a secret of the session (issue #326, item 2).
    /// </summary>
    [NotAudited]
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>Bookkeeping, not a change: a sign in that moves only this, and what IVAO says again, leaves no audit row.</summary>
    [NotAudited]
    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime RowVersion { get; set; }

    public ICollection<UserStaffPosition> StaffPositions { get; } = [];
}
