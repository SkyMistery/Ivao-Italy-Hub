using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth;

/// <summary>
/// Snapshot of the raw IVAO staff positions of a user, with the columns that
/// <c>StaffRoleMap</c> derives from them (F2). The raw value is kept even when it is not
/// recognised, so nothing is ever lost.
/// <para>What a person may do rests on their positions, so a position that is added, taken away or read differently makes
/// the cookies of that person out of date, on every device: the interceptor gives them a new stamp, as for a grant. A sign-in
/// that finds the same positions only moves <see cref="SyncedAt"/>, which is bookkeeping, and signs nobody out.</para>
/// </summary>
public sealed class UserStaffPosition : IAffectsUserSession
{
    public int Vid { get; set; }

    /// <summary>The position exactly as IVAO writes it, for example <c>IT-EC</c> or <c>LIRR-CH</c>.</summary>
    public string Position { get; set; } = string.Empty;

    public Department? Department { get; set; }

    public StaffLevel? Level { get; set; }

    /// <summary>ICAO of the FIR for a FIR position, otherwise null.</summary>
    public string? Fir { get; set; }

    [NotAudited]
    public DateTime SyncedAt { get; set; }

    public HubUser? User { get; set; }

    int IAffectsUserSession.AffectedVid => Vid;
}
