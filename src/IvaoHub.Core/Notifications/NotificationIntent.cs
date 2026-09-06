namespace IvaoHub.Core.Notifications;

/// <summary>
/// Who a notification is for. Two kinds, and the difference matters all the way down: a
/// <see cref="Member"/> is a person, whose address and language the hub looks up when the intent is
/// queued; a <see cref="Mailbox"/> is an address that belongs to nobody — the shared inbox of a
/// department — and has no language of its own, so it is written in the language of the division
/// (decision note of 6 September 2026).
/// </summary>
public sealed record NotificationRecipient
{
    private NotificationRecipient(int vid, string? address)
    {
        Vid = vid;
        Address = address;
    }

    /// <summary>The VID of the person, or zero for a mailbox.</summary>
    public int Vid { get; }

    /// <summary>The address of a mailbox, or null for a person.</summary>
    public string? Address { get; }

    /// <summary>A person. Their address, their language and their preference are looked up.</summary>
    public static NotificationRecipient Member(int vid) => new(vid, null);

    /// <summary>
    /// A fixed address, taken as it is. No preference applies: nobody owns a shared inbox, so
    /// there is nobody to have switched it off.
    /// </summary>
    public static NotificationRecipient Mailbox(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return new(0, address.Trim());
    }
}

/// <summary>
/// What a part of the hub wants somebody told. It carries who, which template and the values that
/// go in it — never a sentence: the words live in <c>locales/{lang}/mail.json</c> and are resolved
/// in the language of each recipient, one at a time (design M1 section 5.2).
/// <para>This is the shape M2 and M3 are meant to use without changing it: a module publishes an
/// intent and knows nothing about SMTP, queues or retries.</para>
/// </summary>
/// <param name="Type">
/// One of <see cref="NotificationTypes"/>. It is the root of the template keys —
/// <c>mail.{type}.subject</c> and <c>mail.{type}.body</c> — and the preference a member switches
/// off, which is why it is one value and not two.
/// </param>
/// <param name="Recipients">Who should hear about it, before preferences are applied.</param>
/// <param name="Data">
/// The placeholders of the template, by name: <c>{{subject}}</c> in the text takes
/// <c>Data["subject"]</c>. Values are already text; formatting a date or a number is the caller's
/// job, because only the caller knows what it means.
/// </param>
public sealed record NotificationIntent(
    string Type,
    IReadOnlyList<NotificationRecipient> Recipients,
    IReadOnlyDictionary<string, string> Data);
