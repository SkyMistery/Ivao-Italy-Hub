namespace IvaoHub.Core.Preferences;

/// <summary>
/// One thing a member chose once and expects to find again on any computer: the order of a queue,
/// say (M2, T4b; design M2 section 4.1). A key and a JSON value, and nothing the core understands of
/// the value — the module that declared the key does.
/// <para>A table and not a column on <c>hub_users</c>, for the reason the notification preferences
/// gave: at the second preference a column costs a migration and a row costs a row. The absence of a
/// row means the member never chose, and the module reads that as its own default.</para>
/// </summary>
public sealed class UserPreference
{
    public int Vid { get; set; }

    /// <summary>One of the keys <see cref="PreferenceCatalog"/> knows, for example <c>flightops.reviewQueueOrder</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The value as the member's browser sent it, already accepted by the declaring module.</summary>
    public string ValueJson { get; set; } = "null";

    public DateTime UpdatedAt { get; set; }
}
