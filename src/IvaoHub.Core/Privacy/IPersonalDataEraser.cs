namespace IvaoHub.Core.Privacy;

/// <summary>What becomes of some of a person's rows when their data is erased.</summary>
public enum ErasureOutcome
{
    /// <summary>The rows go.</summary>
    Deleted,

    /// <summary>The rows stay, without the person: their VID becomes the pseudonym and what they wrote goes.</summary>
    Anonymised,

    /// <summary>The rows stay as they are, for a reason the line says (a ban still in force).</summary>
    Kept,
}

/// <summary>
/// One line of what an erasure does, or did: a count of rows and what becomes of them.
/// </summary>
/// <param name="Key">Translation key of what the rows are. A module's is in its own namespace (<c>flightops:erasure.pireps</c>).</param>
/// <param name="Count">How many.</param>
/// <param name="Outcome">What becomes of them.</param>
public sealed record ErasureLine(string Key, int Count, ErasureOutcome Outcome);

/// <summary>The person being erased, and the number that takes their place in what stays.</summary>
/// <param name="Vid">Their VID.</param>
/// <param name="Pseudonym">A negative number, new for this erasure, that no table ties back to the VID.</param>
public sealed record ErasureRequest(int Vid, int Pseudonym)
{
    private readonly HashSet<object> _kept = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Keeps a row as it is, VID included: the core does not write the pseudonym into it. For a row the division keeps about the
    /// person on purpose — a ban still in force, which protects nobody without the VID (note
    /// <c>2026-09-25-le-righe-che-restano-con-il-vid</c>). The row is the instance the module's context tracks: the core reads
    /// through the same context, which hands back that very instance.
    /// </summary>
    public void Keep(object row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _kept.Add(row);
    }

    /// <summary>Whether a module asked to keep this row as it is.</summary>
    public bool IsKept(object row) => _kept.Contains(row);
}

/// <summary>
/// A module's half of erasing a person's data (note <c>2026-09-25-la-cancellazione-dei-dati-di-una-persona</c>). Registered in
/// the container by a module that keeps data <b>about</b> people, like <c>IContactReferenceResolver</c>.
/// <para>The module deletes or empties what it knows to be about the person — their reports, what they wrote — and nothing
/// else. The columns that name them are the core's job, for every module alike (<see cref="PersonColumns"/>): after
/// <see cref="EraseAsync"/> the core turns every one of them that still holds the VID into the pseudonym, in the same
/// transaction, and takes the copies of what the module deleted out of the audit log.</para>
/// <para>It writes through the module's own context, which the core has already put in a transaction and in erasure mode:
/// no stamps, and an audit row without the data for every audited row deleted or emptied. It must be safe to run twice: an
/// erasure that failed half way is run again.</para>
/// </summary>
public interface IPersonalDataEraser
{
    /// <summary>The key of the module, whose contexts the core runs this in.</summary>
    string ModuleKey { get; }

    /// <summary>What <see cref="EraseAsync"/> would do, for the superadmin to read before confirming. Reads only.</summary>
    Task<IReadOnlyList<ErasureLine>> PreviewAsync(int vid, CancellationToken cancellationToken = default);

    /// <summary>Deletes or empties the module's rows about the person, and saves. Returns what it did.</summary>
    Task<IReadOnlyList<ErasureLine>> EraseAsync(ErasureRequest request, CancellationToken cancellationToken = default);
}
