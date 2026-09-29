using IvaoHub.Core.Ivao;

namespace IvaoHub.Modules.Training.Sheets;

/// <summary>How a report marks one item of the sheet (design M3 §1.4, §2.7): the item, and how the session went on it.</summary>
/// <param name="ItemId">The item of the sheet, as the page read it.</param>
/// <param name="Grade">A practice item's grade, from one to five; none when the session did not touch it.</param>
/// <param name="Mark">A theory item's mark; none when the session did not touch it.</param>
/// <param name="TraineeComment">What the trainee reads of the item.</param>
/// <param name="StaffNote">What the staff and the trainers read of it, and the trainee never does.</param>
public sealed record TrainingEvaluationWriteDto(long ItemId, int? Grade, TheoryMark? Mark, string? TraineeComment, string? StaffNote);

/// <summary>
/// The evaluation sheet of a report (design M3 §1.4, §2.7), as a query and a pure function, so that the database and a test ask the
/// same thing: the items a report marks — the active ones of the training's ladder and rating, in the order of the sheet — and the
/// sheet filled from them, each evaluation a copy of its item with the entry that marks it, or not applicable (d4). Every refusal is an
/// i18n key under the field it is about, as the form names it: <c>sheet[2].grade</c>.
/// </summary>
public static class EvaluationSheet
{
    /// <summary>The lowest grade of an item of practice (R.5).</summary>
    public const int LowestGrade = 1;

    /// <summary>The highest grade of an item of practice (R.5).</summary>
    public const int HighestGrade = 5;

    /// <summary>The sheet sent is not the training's any more — an item switched off or changed meanwhile —: the page reads it again.</summary>
    public const string Changed = "training:errors.sheetChanged";

    /// <summary>The refusal of an item of the sheet written twice in one report.</summary>
    public const string ItemTwice = "training:errors.evaluationItemTwice";

    /// <summary>The refusal of a mark on an item of theory that is none of the marks the sheet has.</summary>
    public const string MarkUnknown = "training:errors.evaluationMarkUnknown";

    /// <summary>A grade on an item of theory, which is marked instead.</summary>
    public const string GradeOnTheory = "training:errors.evaluationGradeOnTheory";

    /// <summary>A mark on an item of practice, which is graded instead.</summary>
    public const string MarkOnPractice = "training:errors.evaluationMarkOnPractice";

    /// <summary>The name of the sheet in a report, and of its rows in a refusal.</summary>
    public const string Field = "sheet";

    /// <summary>The items a report on that ladder and rating marks now (§2.7): the active ones, in the order of the sheet.</summary>
    public static IQueryable<SheetItem> ItemsOf(IQueryable<SheetItem> items, RatingKind kind, int rating)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Where(item => item.Kind == kind && item.Rating == rating && item.IsActive)
            .OrderBy(item => item.Sort)
            .ThenBy(item => item.Id);
    }

    /// <summary>
    /// The sheet a report fills (§1.4, §2.7): one evaluation per item, in the order given, each a copy of its item — the title, the
    /// section, the place — with the entry that marks it. An item no entry names, or whose entry marks nothing, is not applicable (d4).
    /// Refused, field by field: an entry for an item that is not on the sheet (the sheet changed), or a second one for the same item; a
    /// grade out of its range, or on an item of theory; a mark on an item of practice, or one the sheet does not have; a comment or a
    /// note too long. Returns the sheet, or the refusals.
    /// </summary>
    public static (IReadOnlyList<TrainingEvaluation>? Sheet, IReadOnlyDictionary<string, string[]>? Problems) Fill(
        IReadOnlyList<SheetItem> items,
        IReadOnlyList<TrainingEvaluationWriteDto> entries)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(entries);

        var refusals = new Refusals();
        var onTheSheet = items.ToDictionary(item => item.Id);
        var marked = new Dictionary<long, (TrainingEvaluationWriteDto Entry, int Index)>();

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (!onTheSheet.TryGetValue(entry.ItemId, out var item))
            {
                refusals.Add(Field, Changed);
                continue;
            }

            // An item written twice is not a sheet that changed meanwhile: reloading the page would not help.
            if (!marked.TryAdd(entry.ItemId, (entry, index)))
            {
                refusals.Add(Field, ItemTwice);
                continue;
            }

            var row = $"{Field}[{index}]";
            if (item.Section == SheetSection.Practice)
            {
                if (entry.Grade is < LowestGrade or > HighestGrade)
                {
                    refusals.Add($"{row}.grade", "errors.number.range");
                }

                if (entry.Mark is not null)
                {
                    refusals.Add($"{row}.mark", MarkOnPractice);
                }
            }
            else
            {
                if (entry.Grade is not null)
                {
                    refusals.Add($"{row}.grade", GradeOnTheory);
                }

                if (entry.Mark is { } mark && !Enum.IsDefined(mark))
                {
                    refusals.Add($"{row}.mark", MarkUnknown);
                }
            }

            if (Trimmed(entry.TraineeComment)?.Length > Training.MaxTextLength)
            {
                refusals.Add($"{row}.traineeComment", "errors.text.tooLong");
            }

            if (Trimmed(entry.StaffNote)?.Length > Training.MaxTextLength)
            {
                refusals.Add($"{row}.staffNote", "errors.text.tooLong");
            }
        }

        if (!refusals.IsEmpty)
        {
            return (null, refusals.Errors);
        }

        return (
            [
                .. items.Select(item =>
                {
                    var entry = marked.TryGetValue(item.Id, out var found) ? found.Entry : null;
                    return new TrainingEvaluation
                    {
                        SheetItemId = item.Id,
                        Section = item.Section,
                        Title = item.Title,
                        Sort = item.Sort,
                        Grade = item.Section == SheetSection.Practice ? entry?.Grade : null,
                        Mark = item.Section == SheetSection.Theory ? entry?.Mark : null,
                        TraineeComment = Trimmed(entry?.TraineeComment),
                        StaffNote = Trimmed(entry?.StaffNote),
                    };
                }),
            ],
            null);
    }

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
