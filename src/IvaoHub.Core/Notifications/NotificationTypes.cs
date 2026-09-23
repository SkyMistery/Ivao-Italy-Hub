namespace IvaoHub.Core.Notifications;

/// <summary>
/// The kinds of notification of the core. A type is three things at once, and that is deliberate:
/// the root of the template keys in <c>mail.json</c>, the preference a member switches off, and the
/// label the profile screen shows — one value, so the three cannot drift.
/// <para>M1 has five. Another one of the core is a line here, a pair of keys in every language file
/// and nothing else: that is the whole reason the preferences are a table and not a column. A
/// module's types are the module's (<c>IModule.NotificationTypes</c>), and every type the
/// installation knows is <see cref="NotificationTypeCatalog"/>.</para>
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

    /// <summary>
    /// A page is waiting for approval (G19). Its audience is whoever may approve a page of that
    /// department: the director and the web team, and anybody granted <c>Content.Approve</c>.
    /// </summary>
    public const string ContentReadyForApproval = "content.readyForApproval";

    /// <summary>A page the reader marked ready has been published. Its audience is that person.</summary>
    public const string ContentApproved = "content.approved";

    /// <summary>A page the reader marked ready has been sent back, with a note. Its audience is that person.</summary>
    public const string ContentSentBack = "content.sentBack";

    /// <summary>The core's, in the order the profile screen lists them, before the modules'.</summary>
    public static readonly IReadOnlyList<string> All =
        [ContactReceived, DocumentReviewDue, ContentReadyForApproval, ContentApproved, ContentSentBack];

    /// <summary>The key of the subject line of a type, in <c>locales/{lang}/mail.json</c>.</summary>
    public static string SubjectKey(string type) => $"mail.{type}.subject";

    /// <summary>The key of the body of a type.</summary>
    public static string BodyKey(string type) => $"mail.{type}.body";

}

/// <summary>
/// Every kind of notification this installation knows: the core's and the ones the enabled modules
/// declare in <c>IModule.NotificationTypes</c> (M2, T13, note 2026-09-23-la-validazione §3.2), each
/// named after its module — <c>flightops.pirepAccepted</c> — so the core never names a module.
/// <para>Composed once, like <c>PreferenceCatalog</c>, and it refuses at start up a type declared
/// twice or outside its module's name. A module's words live in its own language file: the mail
/// under <c>mail.{type}</c>, the label of the profile under <c>notifications.{name}</c> of its
/// namespace.</para>
/// </summary>
public sealed class NotificationTypeCatalog
{
    /// <summary>The width of <c>hub_notifications.type</c> and <c>hub_notification_preferences.type</c>.</summary>
    public const int MaxTypeLength = 64;

    private readonly List<string> _all = [.. NotificationTypes.All];

    /// <param name="declared">Each module's key and the types it declares.</param>
    public NotificationTypeCatalog(IEnumerable<(string Module, IReadOnlyList<string> Types)> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        foreach (var (module, types) in declared)
        {
            foreach (var type in types)
            {
                if (!type.StartsWith(module + ".", StringComparison.Ordinal)
                    || type.Length == module.Length + 1
                    || type.Length > MaxTypeLength)
                {
                    throw new InvalidOperationException(
                        $"The notification type '{type}' of the module '{module}' has to be named "
                        + $"'{module}.<name>' and fit in {MaxTypeLength} characters.");
                }

                if (_all.Contains(type, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException($"The notification type '{type}' is declared twice.");
                }

                _all.Add(type);
            }
        }
    }

    /// <summary>The core's first, then each module's, in the order the profile screen lists them.</summary>
    public IReadOnlyList<string> All => _all;

    public bool IsKnown(string? type) => type is not null && _all.Contains(type, StringComparer.Ordinal);
}
