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

/// <summary>
/// A row some of whose instances every department may <b>read</b>, whoever owns them. A template
/// is the first and so far the only one: it belongs to the department that made it and is edited
/// by that department alone, but a coordinator of any other has to be able to see it, or "new from
/// a template" does not exist for eight departments out of nine (design M1 section 9.4).
/// <para>Writing is untouched. The rule below only ever applies to a permission that reads, so a
/// row being shared never widens who may change it.</para>
/// <para>⚠️ There are two sides to this and they have to say the same thing: the list narrows in
/// SQL, through <c>CrudOptions.SharedForReading</c>, and the single authorization handler decides
/// in memory, through this property. So the entity declares the rule <b>once</b>, as an expression,
/// and compiles it for the second side rather than writing it twice — the same discipline the
/// envelope and the walker are held to.</para>
/// </summary>
public interface ISharedForReading
{
    /// <summary>Whether this particular row is one any department may read.</summary>
    bool IsSharedForReading { get; }
}

/// <summary>
/// A row any signed in member may <b>bring into existence</b> inside the space of a department
/// they have nothing to do with. A contact message is the first: somebody writes to a department
/// precisely because they are not part of it (design M1 section 5.1, decision note of 6 September
/// 2026).
/// <para>The write guard of the interceptor asks for <c>{Area}.Edit</c> on the owning department
/// before letting any write of an <see cref="IOwnedByDepartment"/> row through, which is exactly
/// right for every row somebody edits and exactly wrong for a row somebody sends. This is the
/// entity's way of saying so, and it is deliberately the narrowest thing that works: it applies to
/// <b>creation only</b>. Changing such a row afterwards — moving a message from new to answered —
/// is an ordinary write and asks for the permission like everything else.</para>
/// <para>It is the third of the same family and it is written in the same style: <see
/// cref="ISharedForReading"/> widens reading, <c>CrudOptions.ReadOnlyRows</c> narrows writing, this
/// widens creating. The engine is never told what a contact message is; it is told that this
/// resource accepts submissions.</para>
/// </summary>
public interface ISubmittedByMembers;
