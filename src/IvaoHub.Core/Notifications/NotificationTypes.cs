namespace IvaoHub.Core.Notifications;

/// <summary>
/// Every kind of notification this installation can send. A type is three things at once, and that
/// is deliberate: the root of the template keys in <c>mail.json</c>, the preference a member
/// switches off, and the label the profile screen shows — one value, so the three cannot drift.
/// <para>M1 has one. The second one is a line here, a pair of keys in every language file and
/// nothing else: that is the whole reason the preferences are a table and not a column.</para>
/// </summary>
public static class NotificationTypes
{
    /// <summary>A member has written to a department. Its audience is that department's staff.</summary>
    public const string ContactReceived = "contact.received";

    /// <summary>
    /// An operational document's review date has passed (G14). Its audience is the staff of the
    /// department that owns it, told once: <c>DocumentReviewJob</c> writes when it told them.
    /// </summary>
    public const string DocumentReviewDue = "document.reviewDue";

    /// <summary>In the order the profile screen lists them.</summary>
    public static readonly IReadOnlyList<string> All = [ContactReceived, DocumentReviewDue];

    /// <summary>The key of the subject line of a type, in <c>locales/{lang}/mail.json</c>.</summary>
    public static string SubjectKey(string type) => $"mail.{type}.subject";

    /// <summary>The key of the body of a type.</summary>
    public static string BodyKey(string type) => $"mail.{type}.body";

    public static bool IsKnown(string? type) => type is not null && All.Contains(type, StringComparer.Ordinal);
}
