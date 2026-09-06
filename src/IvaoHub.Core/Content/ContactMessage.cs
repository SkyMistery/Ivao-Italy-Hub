using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;

namespace IvaoHub.Core.Content;

/// <summary>Where a message has got to. The queue of a department is these four words.</summary>
public enum ContactStatus
{
    /// <summary>Just arrived; nobody has opened it.</summary>
    New,

    /// <summary>Somebody of the department has read it.</summary>
    Read,

    /// <summary>An answer went out, by whatever means the department answers with.</summary>
    Answered,

    /// <summary>Done with, answer or no answer.</summary>
    Closed,
}

/// <summary>
/// A message a member sends to a department (design M1 section 5.1).
/// <para><see cref="IOwnedByDepartment.OwnerDepartment"/> <b>is</b> the department it was sent to,
/// which is what makes the queue of the back office, the department filter of the list and the row
/// level check of the single authorization handler come out of mechanisms that already exist.</para>
/// <para><see cref="ISubmittedByMembers"/> is the other half of that choice: the sender is by
/// definition somebody who is not part of the department, so creating one is open to any signed in
/// member. Everything afterwards is an ordinary write and asks for <c>Contacts.Edit</c> on the
/// department (decision note of 6 September 2026).</para>
/// <para>Two columns the design listed are deliberately absent. The sender is
/// <see cref="CreatedBy"/> and the person who last moved the message is <see cref="UpdatedBy"/>:
/// the interceptor writes both, and a <c>FromVid</c> or a <c>HandledBy</c> next to them would be
/// the audit trail written a second time by hand.</para>
/// </summary>
[Audited]
[PermissionArea(CorePermissions.ContactsArea)]
public sealed class ContactMessage : IOwnedByDepartment, IAuditable, ISubmittedByMembers
{
    public long Id { get; set; }

    /// <summary>The department the message was sent to, and therefore whose queue it is in.</summary>
    public Department OwnerDepartment { get; set; }

    /// <summary>What it is about, as the sender wrote it. One language: theirs.</summary>
    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public ContactStatus Status { get; set; } = ContactStatus.New;

    /// <summary>When the message arrived, and the VID of whoever sent it.</summary>
    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    /// <summary>When the status last moved, and by whom.</summary>
    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}
