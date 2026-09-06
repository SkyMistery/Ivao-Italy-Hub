namespace IvaoHub.Core.Notifications;

/// <summary>
/// Whether one member wants one kind of notification. A table and not a column on
/// <c>hub_users</c>: at the second kind a column would cost a migration and a row costs a row
/// (decided 5 September 2026; design M1 section 5.2).
/// <para>The absence of a row means <b>yes</b>. Somebody who has never opened their profile is
/// somebody who has not said no, and a queue nobody hears about is worse than one mail too many.
/// So a row only ever exists because a member switched something off and then perhaps on again.</para>
/// </summary>
public sealed class NotificationPreference
{
    /// <summary>The member. Unconstrained on purpose is not needed here: it is the same context.</summary>
    public int Vid { get; set; }

    /// <summary>One of <see cref="NotificationTypes"/>.</summary>
    public string Type { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
