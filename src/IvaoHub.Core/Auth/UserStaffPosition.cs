using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth;

/// <summary>
/// Snapshot of the raw IVAO staff positions of a user, with the columns that
/// <c>StaffRoleMap</c> derives from them (F2). The raw value is kept even when it is not
/// recognised, so nothing is ever lost.
/// </summary>
public sealed class UserStaffPosition
{
    public int Vid { get; set; }

    /// <summary>The position exactly as IVAO writes it, for example <c>IT-EC</c> or <c>LIRR-CH</c>.</summary>
    public string Position { get; set; } = string.Empty;

    public Department? Department { get; set; }

    public StaffLevel? Level { get; set; }

    /// <summary>ICAO of the FIR for a FIR position, otherwise null.</summary>
    public string? Fir { get; set; }

    public DateTime SyncedAt { get; set; }

    public HubUser? User { get; set; }
}
