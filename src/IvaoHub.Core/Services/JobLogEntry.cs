namespace IvaoHub.Core.Services;

/// <summary>Outcome of a scheduled job. A failed synchronisation is a row here, never a crash.</summary>
public sealed class JobLogEntry
{
    public long Id { get; set; }

    public string Job { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    /// <summary>For example <c>succeeded</c>, <c>failed</c>, <c>skipped</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Message { get; set; }
}
