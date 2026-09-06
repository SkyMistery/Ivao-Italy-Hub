namespace IvaoHub.Core.Notifications;

/// <summary>Where one queued notification has got to.</summary>
public enum NotificationStatus
{
    /// <summary>Waiting for the job, or waiting to be tried again.</summary>
    Pending,

    /// <summary>Handed to the mail server.</summary>
    Sent,

    /// <summary>Tried as many times as it is worth trying. Nobody will look at it again.</summary>
    Failed,
}

/// <summary>
/// One notification for one recipient, waiting to go out (design M1 section 5.2).
/// <para>One row per recipient rather than one per intent, because a retry is per address: an
/// intent for five people of whom one has a full mailbox must not resend to the other four.</para>
/// <para>The address is copied in when the intent is queued rather than looked up when it is sent.
/// It is what was true at the moment the thing happened, and it makes the job a sender and nothing
/// else — no preferences, no profiles, no reason to widen it later.</para>
/// <para>Not <c>IOwnedByDepartment</c>, not <c>IVisible</c>, not audited: a queue belongs to the
/// application rather than to a department, and it is never read by a screen.</para>
/// </summary>
public sealed class Notification
{
    public long Id { get; set; }

    /// <summary>One of <see cref="NotificationTypes"/>; it names the template.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The VID this was meant for, or zero when it is going to a shared mailbox.</summary>
    public int Vid { get; set; }

    public string Address { get; set; } = string.Empty;

    /// <summary>The language the template is rendered in: the recipient's, not the sender's.</summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>The placeholders of the template, as JSON.</summary>
    public string DataJson { get; set; } = "{}";

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    /// <summary>How many times it has been tried. It stops at the limit the job holds.</summary>
    public int Attempts { get; set; }

    /// <summary>Why the last attempt failed, truncated. The log file has the rest.</summary>
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }
}
