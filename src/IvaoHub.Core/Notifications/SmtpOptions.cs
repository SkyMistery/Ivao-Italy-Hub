namespace IvaoHub.Core.Notifications;

/// <summary>
/// The mail server, bound from <c>Smtp:</c> — <c>appsettings</c> in development, a file of
/// <c>secrets/</c> or an environment variable in production, never the repository (design M0
/// section 1.4).
/// <para>In development it is Mailpit, which is in <c>docker-compose.yml</c> from the first day:
/// host <c>localhost</c>, port 1025, no user, no TLS.</para>
/// </summary>
public sealed record SmtpOptions
{
    /// <summary>Configuration section.</summary>
    public const string Section = "Smtp";

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 1025;

    public string? User { get; init; }

    public string? Password { get; init; }

    /// <summary>The address the hub writes from.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>The name beside it. Empty is fine: then the address stands on its own.</summary>
    public string? FromName { get; init; }

    /// <summary>
    /// Whether to ask for STARTTLS. False for Mailpit, true for anything on the open internet.
    /// </summary>
    public bool UseStartTls { get; init; }

    /// <summary>
    /// Whether there is a server to talk to at all. A hub with no mail configured is a working
    /// hub: the queue fills and the job says it is not sending, which is a line in
    /// <c>hub_jobs_log</c> rather than a stack trace every minute.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}
