namespace IvaoHub.Core.Division;

/// <summary>
/// Which rows may be embedded inside something of a given visibility.
/// <para>The global query filter answers "may <b>this reader</b> see this row", and it is the only
/// thing that ever answers it. This answers a different question, and one it cannot: "may this row
/// be <b>copied into</b> a page that is shown to somebody else". Publication is where the two part
/// company — a data block marked <c>frozen</c> is resolved once, by the member publishing, and the
/// answer is then stored in the version that a visitor reads. Without a ceiling, a coordinator
/// publishing a public page would freeze into it the staff-only rows only they can see (design M0
/// section 5.5, note <c>2026-09-04-frozen-e-visibilita.md</c>).</para>
/// <para>Written as a table rather than as an ordering. The four visibilities are not really a
/// scale — <c>Department</c> is narrower than <c>Staff</c> but points at one department in
/// particular — and a table can be read and tested row by row, the way
/// <c>RolePermissionMatrix</c> is.</para>
/// </summary>
public static class VisibilityCeiling
{
    private static readonly Dictionary<Visibility, Visibility[]> Embeddable = new()
    {
        // A page anybody can read may only carry rows anybody can read.
        [Visibility.Public] = [Visibility.Public],

        // A page for members may carry what a member sees.
        [Visibility.Members] = [Visibility.Public, Visibility.Members],

        // A page for the staff may carry what the staff sees, of every department.
        [Visibility.Staff] = [Visibility.Public, Visibility.Members, Visibility.Staff],

        // A page for one department may carry all of that, plus rows of that same department —
        // which is what the caller checks with the owner, since this list cannot say "of which".
        [Visibility.Department] =
            [Visibility.Public, Visibility.Members, Visibility.Staff, Visibility.Department],
    };

    /// <summary>
    /// The visibilities a row may have to be embeddable in something of this visibility. A row
    /// marked <see cref="Visibility.Department"/> also has to belong to the same department as the
    /// page: see <see cref="Allows"/>, which is the whole rule and the one callers should use.
    /// </summary>
    public static IReadOnlyList<Visibility> For(Visibility visibility) =>
        Embeddable.TryGetValue(visibility, out var allowed) ? allowed : [Visibility.Public];

    /// <summary>
    /// Whether one row may be embedded in a page. The whole rule, for a caller that has the row in
    /// hand rather than a query to narrow.
    /// </summary>
    public static bool Allows(
        Visibility pageVisibility,
        Department pageDepartment,
        Visibility rowVisibility,
        Department rowDepartment)
    {
        if (!For(pageVisibility).Contains(rowVisibility))
        {
            return false;
        }

        // "Visible to a department" means a different set of people for each department, so a row
        // of one may not travel into a page of another even though the two share a visibility.
        return rowVisibility != Visibility.Department || rowDepartment == pageDepartment;
    }
}
