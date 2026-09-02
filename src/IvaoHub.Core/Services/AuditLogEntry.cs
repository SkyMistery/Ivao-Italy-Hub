namespace IvaoHub.Core.Services;

/// <summary>
/// One row per write on an entity marked as audited. Written by the single save changes
/// interceptor (F4), never by an endpoint.
/// </summary>
public sealed class AuditLogEntry
{
    public long Id { get; set; }

    /// <summary>VID of the author, 0 for a background job.</summary>
    public int Vid { get; set; }

    /// <summary>What happened, for example <c>created</c>, <c>updated</c>, <c>deleted</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public string? Ip { get; set; }

    /// <summary>Whether the author acted as a super administrator; a bypass is always visible.</summary>
    public bool IsSuperadmin { get; set; }

    public DateTime At { get; set; }
}
