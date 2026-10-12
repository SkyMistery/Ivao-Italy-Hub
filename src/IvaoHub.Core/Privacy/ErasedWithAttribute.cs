namespace IvaoHub.Core.Privacy;

/// <summary>
/// A column that is the data of the person another column of the same row names: the flight a pilot wrote on their booking,
/// <c>[ErasedWith(nameof(BookerVid))] public string? Callsign</c> (M4, E10n, note <c>2026-10-11-i-dati-di-una-persona-nell-audit</c>).
/// <para>What it is for: a row the person's data was on and that is no longer there when they are erased — a booking they
/// withdrew, or the staff took away, the week before — is a row no module's eraser finds, and its <c>created</c> and
/// <c>deleted</c> copies in the audit log still hold that data. The erasure empties these columns in every audit row of such a
/// row (<see cref="AuditRedaction.EmptyGoneAsync"/>), for every module alike, with nothing for the module to write but this
/// mark. A row that still exists stays its eraser's, as it was.</para>
/// <para>The person column is an integer of the same entity that names a person (<see cref="PersonColumns.IsVid"/>), and only an
/// audited entity has copies to empty. A column of the person's data named on another row — a session's notes, whose trainee is
/// named by the training above — is not covered: the mark names a column of the row itself.</para>
/// </summary>
/// <param name="person">The name of the property that names the person whose data this column is.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ErasedWithAttribute(string person) : Attribute
{
    /// <summary>The property that names the person whose data this column is.</summary>
    public string Person { get; } = person;
}
