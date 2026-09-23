namespace IvaoHub.Core.Division;

/// <summary>
/// A row that belongs to a department. It is the hinge of the whole authorisation model: the
/// single authorization handler compares this with the departments of the current user, and the
/// save changes interceptor refuses a write that crosses it even when an endpoint forgot the
/// policy (design M0 sections 3.2 and 3.4).
/// </summary>
public interface IOwnedByDepartment
{
    /// <summary>
    /// The department the row belongs to. For a row in the care of several departments (a row of a
    /// module), the one it always has: the base department of the module when the division names one.
    /// </summary>
    Department OwnerDepartment { get; }

    /// <summary>
    /// Every department the row is in the care of, as a <see cref="DepartmentMask"/>. An editorial row
    /// has one and inherits this; a row of a module that may be organised together with other
    /// departments declares a settable property of this name, which is its column (M2, note
    /// 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.3). Held on one of them is held on the row.
    /// </summary>
    int OwnerDepartmentMask => DepartmentMask.Of(OwnerDepartment);

    /// <summary>The same set, as departments.</summary>
    IReadOnlyList<Department> OwnerDepartments => DepartmentMask.Departments(OwnerDepartmentMask | DepartmentMask.Of(OwnerDepartment));
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

    /// <summary>
    /// The position whose holders' sessions this row decides, when it decides a position rather than a
    /// person: a grant to a department and its levels (M2). The interceptor looks up who holds it at
    /// the moment of the write.
    /// </summary>
    StaffPositionSubject? AffectedPosition => null;
}

/// <summary>A position as a subject: a department and the levels of it that count.</summary>
public sealed record StaffPositionSubject(Department Department, IReadOnlyList<StaffLevel> Levels);

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
/// A second permission a write of this entity may pass the interceptor's guard with, besides <c>{Area}.Edit</c>. A
/// validator enabled on one tour holds <c>Tours.Validate</c> with that tour's scope and nothing else, and taking or
/// deciding a report is a write of the report (M2, T13, note 2026-09-23-la-validazione §3.1).
/// <para>The guard asks it the way the handler does: held on one of the row's departments <b>with the row's scope</b>
/// (<see cref="IHasResourceScope"/>), and never by the member the row is about (<see cref="IHasStakeholder"/>), who has
/// their own narrower way in (<see cref="ISubmittedByMembers"/>). Moving the row between departments still asks for
/// <c>Edit</c> on both sides.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AlsoWrittenWithAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
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
/// A row a permission can be granted on <b>by itself</b>. The scope it declares is compared, string
/// against string, with the scope of a grant: the core never parses it, and a module chooses its
/// shape (<c>flightops:tour:42</c>).
/// <para>A row of a module can answer with the scope of something above it — a report answers with
/// the scope of its tour — so that enabling somebody on a tour enables them on its reports, without
/// a grant per report (decision note of 15 September 2026).</para>
/// </summary>
public interface IHasResourceScope
{
    string ResourceScope { get; }
}

/// <summary>
/// A row that is <b>about</b> a member: the pilot of a report, and later whoever a training session
/// belongs to. The catalogue says which permissions such a person may not use on that row
/// (<c>PermissionDescriptor.DeniedToStakeholder</c>), and the single handler refuses them there —
/// to everybody, super administrator included.
/// <para>Reading is never refused this way: a validator sees their own reports, they just cannot
/// decide them (design M2 sections 4.1 and 7.3).</para>
/// </summary>
public interface IHasStakeholder
{
    int? StakeholderVid { get; }
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
/// <para>One exception after creation, for a row that is also <see cref="IHasStakeholder"/>: the member it is
/// about, who sent it, may keep changing it — a pilot withdraws or corrects their own report (M2, T11) —
/// provided it stays theirs and in the same departments. Deleting it is still the department's.</para>
/// </summary>
public interface ISubmittedByMembers;
