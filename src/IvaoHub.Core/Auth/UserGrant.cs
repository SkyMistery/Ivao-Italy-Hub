using System.Text.Json;
using System.Text.Json.Serialization;
using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth;

/// <summary>What a grant does to a permission.</summary>
public enum GrantEffect
{
    Grant,
    Deny,
}

/// <summary>What a grant talks about. Only permissions for now.</summary>
public enum GrantKind
{
    Permission,
}

/// <summary>
/// A permission given to (or taken from) a single VID — or, since M2, to a <b>position</b>: a
/// department and one or more levels (note 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.2) —
/// on top of what the staff positions derive. A grant has exactly one of the two subjects.
/// Effective permissions are derived, union grants, minus denies (design M0 section 3.7).
/// Who granted it and when are the audit columns: <c>created_by</c> and <c>created_at</c>, not a
/// second pair of columns saying the same thing.
/// </summary>
[Audited]
public sealed class UserGrant : IAuditable, IAffectsUserSession
{
    public long Id { get; set; }

    /// <summary>The member the grant is for, when its subject is a person.</summary>
    public int? Vid { get; set; }

    /// <summary>
    /// The department of the position the grant is for, when its subject is a position: whoever holds
    /// a position of this department at one of <see cref="PositionLevels"/> holds the grant, and
    /// stops holding it with the position (decided with Carmine, 13 September 2026).
    /// </summary>
    public Department? PositionDepartment { get; set; }

    /// <summary>The levels of that position, as a JSON array of names. Null for a grant to a person.</summary>
    public string? PositionLevelsJson { get; set; }

    /// <summary><see cref="PositionLevelsJson"/>, read and written as the list it is.</summary>
    public IReadOnlyList<StaffLevel> PositionLevels
    {
        get => PositionLevelsJson is null ? [] : JsonSerializer.Deserialize<StaffLevel[]>(PositionLevelsJson, LevelsJson) ?? [];
        set => PositionLevelsJson = value is null || value.Count == 0 ? null : JsonSerializer.Serialize(value.Distinct().Order().ToArray(), LevelsJson);
    }

    /// <summary>Whether a member holding these positions is who this grant is for, when it is for a position.</summary>
    public bool IsHeldThrough(IEnumerable<StaffPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        return PositionDepartment is { } department
            && positions.Any(position => position.Department == department && PositionLevels.Contains(position.Level));
    }

    public GrantKind Kind { get; set; }

    /// <summary>The permission name, for example <c>Links.Edit</c>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Null means every department.</summary>
    public Department? Department { get; set; }

    /// <summary>
    /// The single row this grant is about, when it is about one: <c>flightops:tour:42</c>. Null is
    /// the ordinary grant, which reaches every row of its department.
    /// <para>The core never interprets it — it compares it with what a row declares through
    /// <c>IHasResourceScope</c> — and the generic permissions screen never writes it: the module
    /// that owns the rows does, from a screen that knows which rows exist (decision note of
    /// 15 September 2026).</para>
    /// </summary>
    public string? ResourceScope { get; set; }

    public GrantEffect Effect { get; set; }

    public DateTime? ExpiresAt { get; set; }

    /// <summary>Set when the roster sync no longer sees the VID as staff; the grant is kept, not deleted.</summary>
    public DateTime? SuspendedAt { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    public HubUser? User { get; set; }

    /// <summary>
    /// Changing a grant changes what its holder may do, so their cookie has to stop being believed
    /// at once rather than at their next login. The interceptor does it, for whoever writes the row
    /// (design M0 section 3.3).
    /// </summary>
    int IAffectsUserSession.AffectedVid => Vid ?? 0;

    /// <summary>A grant to a position decides the session of everybody who holds it now.</summary>
    StaffPositionSubject? IAffectsUserSession.AffectedPosition =>
        PositionDepartment is { } department && PositionLevels.Count > 0
            ? new StaffPositionSubject(department, PositionLevels)
            : null;

    private static readonly JsonSerializerOptions LevelsJson = new() { Converters = { new JsonStringEnumConverter() } };
}
