namespace IvaoHub.Modules.Training.Staff;

/// <summary>
/// What the trainee of a training never reads, even from the staff's side (note <c>le-note-riservate-e-il-trainee</c>; design M3
/// §12 n.13): a trainer is a member too, and asks for trainings of their own; their <c>Training.View</c> comes from their position,
/// and the core never denies reading, so from the staff's page they would read what the staff wrote about them. The staff's answer
/// on a training passes through here, once, as <see cref="StaffTrainings.PageAsync"/> builds it: when the reader is its trainee —
/// whoever they are, the coordinator, the direction and the super administrator included —, the reserved fields are left out and
/// the page says so. The row stays readable: this is the shape of the answer, not an authorization.
/// <para>Reserved are the report's comment for the staff, the note of the staff on every item of the sheet, and the internal notes of
/// every session. A phase that adds a reserved field adds it here, and the test of the note lists them.</para>
/// </summary>
public static class ReservedFields
{
    /// <summary>The page as <paramref name="readerVid"/> reads it: whole, or without what is reserved when they are its trainee.</summary>
    public static StaffTrainingDto For(StaffTrainingDto page, int readerVid)
    {
        ArgumentNullException.ThrowIfNull(page);

        return page.Trainee.Vid != readerVid
            ? page
            : page with
            {
                StaffComment = null,
                Sheet = [.. page.Sheet.Select(item => item with { StaffNote = null })],
                Sessions = [.. page.Sessions.Select(session => session with { InternalNotes = null })],
                ReservedLeftOut = true,
            };
    }
}
