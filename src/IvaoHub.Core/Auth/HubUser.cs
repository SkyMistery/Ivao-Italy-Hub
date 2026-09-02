namespace IvaoHub.Core.Auth;

/// <summary>
/// A person who has logged in at least once: there is no IVAO endpoint that lists the staff of a
/// division, so the roster of the hub is exactly this table (plan section 16.13).
/// </summary>
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

    public string? DiscordId { get; set; }

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

    /// <summary>Changed whenever grants or the super administrator flag change, so the cookie is rejected.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime RowVersion { get; set; }

    public ICollection<UserStaffPosition> StaffPositions { get; } = [];
}
