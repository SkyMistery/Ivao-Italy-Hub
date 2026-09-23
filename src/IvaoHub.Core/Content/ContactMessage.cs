using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>Where a message has got to. The queue of a department is these four words.</summary>
public enum ContactStatus
{
    /// <summary>Just arrived, or the sender has written again; nobody has opened it since.</summary>
    New,

    /// <summary>Somebody of the department has read it.</summary>
    Read,

    /// <summary>The department, or somebody taking part, has answered in the thread (M2, T14).</summary>
    Answered,

    /// <summary>Done with, answer or no answer.</summary>
    Closed,
}

/// <summary>
/// A message a member sends to a department (design M1 section 5.1), and since M2 the thread of answers that follows it
/// (note 2026-09-15-contatti-con-risposte).
/// <para><see cref="IOwnedByDepartment.OwnerDepartment"/> <b>is</b> the department it was sent to,
/// which is what makes the queue of the back office, the department filter of the list and the row
/// level check of the single authorization handler come out of mechanisms that already exist.</para>
/// <para><see cref="ISubmittedByMembers"/> is the other half of that choice: the sender is by
/// definition somebody who is not part of the department, so creating one is open to any signed in
/// member. Everything afterwards is an ordinary write and asks for <c>Contacts.Edit</c> on the
/// department (decision note of 6 September 2026) — except answering, which is reading's permission
/// (<see cref="AlsoWrittenWithAttribute"/>) or taking part in the thread (<see cref="IHasParticipants"/>).</para>
/// <para>Two columns the design listed are deliberately absent. The sender is
/// <see cref="CreatedBy"/> and the person who last moved the message is <see cref="UpdatedBy"/>:
/// the interceptor writes both, and a <c>FromVid</c> or a <c>HandledBy</c> next to them would be
/// the audit trail written a second time by hand.</para>
/// </summary>
[Audited]
[PermissionArea(CorePermissions.ContactsArea)]
// Answering is reading's permission (note 2026-09-15-contatti-con-risposte §3.2), and an answer moves the status: an
// advisor who holds Contacts.View writes the row that far. The PUT of the status still asks for Contacts.Edit.
[AlsoWrittenWith(CorePermissions.ContactsView)]
public sealed class ContactMessage : IOwnedByDepartment, IAuditable, ISubmittedByMembers, IHasParticipants
{
    public long Id { get; set; }

    /// <summary>The department the message was sent to, and therefore whose queue it is in.</summary>
    public Department OwnerDepartment { get; set; }

    /// <summary>What it is about, as the sender wrote it. One language: theirs.</summary>
    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public ContactStatus Status { get; set; } = ContactStatus.New;

    /// <summary>
    /// What kind of conversation it is: <see cref="ContactKinds.General"/> for the form, or one a module opens (M2, T14).
    /// A key, never a sentence: the screen spells it from <c>contacts.kinds</c>.
    /// </summary>
    public string Kind { get; set; } = ContactKinds.General;

    /// <summary>
    /// The VIDs taking part besides the sender, as a JSON array: <c>[780001]</c>. Read in SQL with
    /// <see cref="JsonQuery.ContainsValue"/> and in memory through <see cref="ParticipantVids"/>: the two halves of one
    /// rule, which <see cref="TakesPart"/> and <see cref="ParticipantVids"/> say side by side.
    /// </summary>
    public string ParticipantsJson { get; set; } = "[]";

    /// <summary>
    /// The row that opened the thread, when a module did (<see cref="ThreadOpeningProjection"/>): with <see cref="Kind"/>
    /// it is the key of "once only", unique in the table. Null for a message of the form.
    /// </summary>
    public string? SourceModule { get; set; }

    public string? SourceId { get; set; }

    /// <summary>When the message arrived, and the VID of whoever sent it.</summary>
    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    /// <summary>When the status last moved, and by whom.</summary>
    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>The participants added to the thread, read from <see cref="ParticipantsJson"/>.</summary>
    public IReadOnlyList<int> AddedParticipants => ContactParticipants.Parse(ParticipantsJson);

    /// <inheritdoc />
    /// <remarks>The sender takes part in their own thread, so they are here too (note §3.2).</remarks>
    public IReadOnlyCollection<int> ParticipantVids => [CreatedBy, .. AddedParticipants];

    /// <summary>
    /// The rule of <see cref="ParticipantVids"/> in SQL: the list of <c>/me/contacts</c> narrows with it
    /// (<c>CrudOptions.Participating</c>).
    /// </summary>
    public static readonly Expression<Func<ContactMessage, int, bool>> TakesPart =
        (message, vid) => message.CreatedBy == vid
            || JsonQuery.ContainsValue(message.ParticipantsJson, vid.ToString(CultureInfo.InvariantCulture));
}

/// <summary>The kinds of thread the core knows. They are keys: the screen spells them from <c>contacts.kinds</c>.</summary>
public static class ContactKinds
{
    /// <summary>A message of the form: somebody writes to a department.</summary>
    public const string General = "general";

    /// <summary>A decision contested within its window (M2, design section 3.8). Opened by a module, never by the form.</summary>
    public const string Dispute = "dispute";

    /// <summary>"Explain this to me", about one or more objects (M2, design section 3.10). It changes nothing of them.</summary>
    public const string Clarification = "clarification";

    /// <summary>The kinds a member may open from <c>POST /api/contacts</c>; the others are opened by a module.</summary>
    public static readonly IReadOnlyList<string> Submittable = [General, Clarification];

    /// <summary>The width of <c>cms_contact_messages.kind</c>.</summary>
    public const int MaxLength = 32;
}

/// <summary>Reading and writing the participants column: one parser, for the entity and for whoever opens a thread.</summary>
public static class ContactParticipants
{
    public static IReadOnlyList<int> Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<int[]>(json) ?? [];

    /// <summary>Distinct, positive and in order, without <paramref name="except"/>: the sender is never listed twice.</summary>
    public static string Write(IEnumerable<int> vids, int? except = null)
    {
        ArgumentNullException.ThrowIfNull(vids);
        return JsonSerializer.Serialize(vids.Where(vid => vid > 0 && vid != except).Distinct().Order().ToArray());
    }
}

/// <summary>Who wrote an answer, as the thread draws it. The member reads the department for both of the last two.</summary>
public enum ContactReplySide
{
    /// <summary>The member who opened the thread.</summary>
    Sender,

    /// <summary>Somebody of the department, with <c>Contacts.View</c> on it.</summary>
    Department,

    /// <summary>Somebody added to the thread: the validator of a disputed report.</summary>
    Participant,
}

/// <summary>
/// One answer in a thread (<c>cms_contact_replies</c>). Only ever added: an answer is part of a record a dispute or a ban
/// may cite, so it is neither changed nor deleted (note 2026-09-15-contatti-con-risposte §3.1).
/// <para>Not <see cref="IOwnedByDepartment"/>: it is a child of its message, written only by <c>ContactThreads</c> after
/// the handler has said yes on the message itself.</para>
/// </summary>
public sealed class ContactReply
{
    public long Id { get; set; }

    public long MessageId { get; set; }

    public int AuthorVid { get; set; }

    public ContactReplySide Side { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// An object a thread is about (<c>cms_contact_references</c>): a report, a leg, a rule. The core does not know what a
/// <c>pirep:123</c> is; the module's <see cref="IContactReferenceResolver"/> does. The label is taken when the thread
/// opens, so the reference still reads when the object behind it is gone.
/// </summary>
public sealed class ContactReference
{
    public long Id { get; set; }

    public long MessageId { get; set; }

    /// <summary>The thread, for the one writer that adds both at once (a projection) and needs the key filled in.</summary>
    public ContactMessage? Message { get; set; }

    public string SourceModule { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public Localized<string> Label { get; set; } = new(new Dictionary<string, string>());
}
