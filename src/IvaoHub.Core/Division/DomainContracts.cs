namespace IvaoHub.Core.Division;

/// <summary>
/// A row that belongs to a department. It is the hinge of the whole authorisation model: the
/// single authorization handler compares this with the departments of the current user, and the
/// save changes interceptor refuses a write that crosses it even when an endpoint forgot the
/// policy (design M0 sections 3.2 and 3.4).
/// </summary>
public interface IOwnedByDepartment
{
    Department OwnerDepartment { get; }
}

/// <summary>A row that is not necessarily readable by everybody. Enforced by the global query filter.</summary>
public interface IVisible
{
    Visibility Visibility { get; }
}

/// <summary>
/// A row the public only ever sees once somebody published it. A draft is also never projected:
/// the convention is applied by the interceptor, not repeated by every entity.
/// </summary>
public interface IPublishable
{
    PublishStatus Status { get; }

    DateTime? PublishedAt { get; }
}

/// <summary>
/// Who wrote the row and when. The values are filled by the interceptor and never by an endpoint,
/// so "created_by" always means the same thing.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }

    /// <summary>VID of the author, 0 for a background job.</summary>
    int CreatedBy { get; set; }

    DateTime UpdatedAt { get; set; }

    int UpdatedBy { get; set; }
}

/// <summary>
/// A row whose change makes somebody's session out of date: writing it regenerates that member's
/// <c>security_stamp</c>, so the cookie they are carrying right now is refused on their very next
/// request and rebuilt with the permissions they hold now.
/// <para>Declared by the entity and applied by the save changes interceptor, in the same shape as
/// <see cref="IAuditable"/> and <c>IProjectable</c>: it therefore holds for whoever writes the row —
/// the administration screen, a seed, a service that does not exist yet — rather than only for the
/// one path that remembered to ask. A grant is the first of them (design M0 section 3.3).</para>
/// </summary>
public interface IAffectsUserSession
{
    /// <summary>The VID whose session this row decides. Zero means nobody, and nothing happens.</summary>
    int AffectedVid { get; }
}

/// <summary>
/// A row that belongs to a FIR. Used when the division sets <c>firStaffScope = own</c>; no entity
/// of M0 implements it, but the handler already knows what to do with it.
/// </summary>
public interface IHasFir
{
    string? Fir { get; }
}

/// <summary>
/// The permission area of an entity, when it is not the name of its <c>DbSet</c>. The interceptor
/// asks for <c>{Area}.Edit</c> before letting a write through, so <c>ContentEntry</c> declares
/// <c>Content</c> rather than inheriting <c>Contents</c> from its set.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PermissionAreaAttribute(string area) : Attribute
{
    public string Area { get; } = area;
}

/// <summary>
/// Every write on this entity leaves a row in <c>hub_audit_log</c>, with the scalar properties
/// before and after. Written by the interceptor: a service never writes an audit row by hand.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditedAttribute : Attribute;
