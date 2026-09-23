using IvaoHub.Core.Division;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Content;

/// <summary>
/// A message as the queue of a department shows it. The body is not here: a list is for choosing
/// which one to open.
/// </summary>
public sealed record ContactListDto(
    long Id,
    Department OwnerDepartment,
    string Subject,
    string Kind,
    ContactStatus Status,
    int CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// A message in full. Everything but the status is read only on the screen and read only on the
/// server: what the sender wrote is not the department's to edit, which is why the write payload
/// below carries the status and nothing else.
/// </summary>
public sealed record ContactDetailDto(
    long Id,
    Department OwnerDepartment,
    string Subject,
    string Body,
    string Kind,
    ContactStatus Status,
    int CreatedBy,
    DateTime CreatedAt,
    int UpdatedBy,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What the department may change: where the message has got to, and nothing else. A payload that
/// could carry the subject or the body would be a payload that could rewrite somebody's message,
/// and no permission is meant to allow that.
/// <para><see cref="RowVersion"/> is the version the screen was loaded with; a stale one is how the
/// server finds out that somebody else moved the message first, and answers 409.</para>
/// </summary>
public sealed record ContactStatusWriteDto(ContactStatus Status, DateTime RowVersion);

/// <summary>
/// What a member sends. There is no sender field: the VID is the one of the session, so there is
/// nothing to verify and nothing to forge (design M1 section 5.1).
/// <para>Since M2 a message may be a clarification about some objects of a module (design M2 section 3.10): its kind
/// and what it cites. A module's own kinds — a dispute — are never sent from here: the module opens them.</para>
/// </summary>
public sealed record ContactSubmitDto(
    Department Department,
    string Subject,
    string Body,
    string? Kind = null,
    IReadOnlyList<ContactReferenceInput>? References = null);

/// <summary>One object a message cites, as the sender names it: <c>flightops</c> and <c>pirep:123</c>.</summary>
public sealed record ContactReferenceInput(string SourceModule, string SourceId);

/// <summary>An answer, as whoever writes it sends it: the text and nothing else. Who and which side is the session.</summary>
public sealed record ContactReplyWriteDto(string Body);

/// <summary>
/// A thread as one reader reads it (M2, T14): the message, what it cites, and the answers in order. The same shape for
/// the back office and <c>/me/contacts</c>; what changes with the reader is what it hides.
/// <para><c>ReaderIsSender</c> says whether the reader wrote it, and the screen then draws the department as the other
/// side; <c>Participants</c>, who else takes part, is empty for the sender, who does not read who of the department
/// answers.</para>
/// </summary>
public sealed record ContactThreadDto(
    long Id,
    Department Department,
    string Kind,
    string Subject,
    string Body,
    ContactStatus Status,
    int SenderVid,
    DateTime CreatedAt,
    bool ReaderIsSender,
    IReadOnlyList<int> Participants,
    IReadOnlyList<ContactReferenceDto> References,
    IReadOnlyList<ContactReplyDto> Replies);

/// <summary>One object the thread cites: its label from when the thread opened, and a link when the reader may open it.</summary>
public sealed record ContactReferenceDto(string SourceModule, string SourceId, string Label, string? Url);

/// <summary>
/// One answer. <see cref="AuthorVid"/> and <see cref="AuthorName"/> are null when the reader is the sender and the
/// answer is from the department's side: the member reads the department, never the person (design M2 section 3.5).
/// </summary>
public sealed record ContactReplyDto(
    long Id,
    ContactReplySide Side,
    int? AuthorVid,
    string? AuthorName,
    string Body,
    DateTime CreatedAt);

/// <summary>
/// Entity to payload and back. Generated, like every other mapping of the hub.
/// <para>Only the status is applied: <see cref="ContactStatusWriteDto"/> has nothing else to
/// apply, which is the rule expressed as a type rather than as a check.</para>
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class ContactMapper
{
    public partial ContactListDto ToList(ContactMessage message);

    public partial ContactDetailDto ToDetail(ContactMessage message);

    public partial void Apply(ContactStatusWriteDto payload, ContactMessage message);
}
