using IvaoHub.Core.Localization;
using IvaoHub.Modules.Training.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training.Sheets;

/// <summary>How an item of theory is marked (design M3 §1.4, R.5). Stored by name.</summary>
public enum TheoryMark
{
    Done,

    NotDone,

    ToImprove,
}

/// <summary>
/// One item of the sheet a report filled, <c>trn_evaluations</c> (design M3 §1.4, §2.7). It keeps a copy of the item as it was — its
/// title in every language, its section, its place in the sheet —, so that a report never changes when the item does (note
/// <c>il-tempo-per-la-data-e-le-voci-della-scheda</c>), and how the session went on it: a grade from one to five for practice, done,
/// not done or to improve for theory, or nothing at all — not applicable, an item the session did not touch (d4) —; the comment the
/// trainee reads, and the note of the staff, which the trainee never does (note <c>le-note-riservate-e-il-trainee</c>).
/// <para>A child row of the training, written with its report by whoever conducts it (§1.1): its authorization is the training's. It
/// keeps which item it copies as well — with no key towards the sheet, like the errors a report of the tours marks —, so that an item a
/// report marked is switched off and never deleted (<see cref="ISheetItemReports"/>). Not audited: the notes of the staff stay out of
/// the log, which the note cannot reach.</para>
/// </summary>
public sealed class TrainingEvaluation
{
    public long Id { get; set; }

    public long TrainingId { get; set; }

    /// <summary>The item this is a copy of.</summary>
    public long SheetItemId { get; set; }

    /// <summary>The item's section when the report was written: how it was marked.</summary>
    public SheetSection Section { get; set; }

    /// <summary>The item's title when the report was written, in every language of the division.</summary>
    public Localized<string> Title { get; set; } = Localized<string>.Empty;

    /// <summary>The item's place in the sheet when the report was written, lowest first.</summary>
    public int Sort { get; set; }

    /// <summary>A practice item's grade, from one to five; none when the session did not touch it, and never on an item of theory.</summary>
    public int? Grade { get; set; }

    /// <summary>A theory item's mark; none when the session did not touch it, and never on an item of practice.</summary>
    public TheoryMark? Mark { get; set; }

    /// <summary>What the trainee reads of the item.</summary>
    public string? TraineeComment { get; set; }

    /// <summary>What the staff and the trainers read of the item, and the trainee never does.</summary>
    public string? StaffNote { get; set; }
}

/// <summary>
/// Whether a report marks an item of the sheet (design M3 §1.4): the sheets the reports filled answer (A9), in the registration that
/// answered «none» before them. Any report, whatever its training came to since, is one.
/// </summary>
internal sealed class EvaluationSheetItemReports(TrainingDbContext database) : ISheetItemReports
{
    public Task<bool> AnyAsync(long itemId, CancellationToken cancellationToken = default) =>
        database.Evaluations.AnyAsync(evaluation => evaluation.SheetItemId == itemId, cancellationToken);
}
