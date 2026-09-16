namespace IvaoHub.Core.Content;

/// <summary>
/// "This row of a module shows this file, until then" (M2, T4, note 2026-09-15-file-con-scadenza):
/// a projection, written by the interceptor from <see cref="MediaUseProjection"/> in the transaction
/// of the row, rewritten in full for its source every time the row is saved. A tour extended moves
/// its date by itself; a banner changed takes the old use away.
/// <para>A table of its own and not a line of <see cref="ContentReference"/>: that index is per
/// published version of a content, and a row of a module has neither. The question "who uses this
/// file?" is still asked in one place, <see cref="ContentReferenceIndex"/>, which reads both.</para>
/// <para>Not owned, not visible, not audited, like the index of references: every question asked of
/// it goes back to the file and to the row for what the reader may see.</para>
/// </summary>
public sealed class MediaUse
{
    public long Id { get; set; }

    public string SourceModule { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public long MediaId { get; set; }

    /// <summary><c>null</c>: without an end, which keeps the file for as long as the row declares it.</summary>
    public DateTime? UsedUntil { get; set; }
}
