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
/// </summary>
public sealed record ContactSubmitDto(Department Department, string Subject, string Body);

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
