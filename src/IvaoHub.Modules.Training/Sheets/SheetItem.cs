using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Training.Sheets;

/// <summary>Where an item of the evaluation sheet sits, which says how the trainer marks it (design M3 §1.4).</summary>
public enum SheetSection
{
    /// <summary>What the trainee does on the position or in the aircraft, graded from one to five.</summary>
    Practice,

    /// <summary>What the trainee knows: done, not done, or to improve.</summary>
    Theory,
}

/// <summary>
/// An item of the evaluation sheet, <c>trn_sheet_items</c> (design M3 §1.4): what a trainer marks after a session of a training
/// on one ladder and rating, in the section that says how. The coordinator and the assistant of the training department write
/// them (<c>Training.ManageSheets</c>), in every language of the division (§12 n.11), and the trainee reads them in a report.
/// <para>A report keeps a copy of the items it marks (A9), so changing one never changes a report already written; and an item a
/// report marks is switched off, never deleted (<see cref="ISheetItemReports"/>).</para>
/// </summary>
[Audited]
[PermissionArea(TrainingPermissions.Area)]
public sealed class SheetItem : IOwnedByDepartment, IAuditable
{
    public long Id { get; set; }

    /// <summary>The ladder of the rating.</summary>
    public RatingKind Kind { get; set; }

    /// <summary>The rating, by the number the hub keeps: one the core's vocabulary gives a practical training.</summary>
    public int Rating { get; set; }

    public SheetSection Section { get; set; }

    public Localized<string> Title { get; set; } = Localized<string>.Empty;

    /// <summary>Where the item comes in the sheet of its rating, lowest first.</summary>
    public int Sort { get; set; }

    /// <summary>Whether a new report marks it. An item switched off stays in the reports that marked it.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}

/// <summary>
/// Whether a report marks an item of the sheet, which decides whether the item may be deleted or only switched off (design M3
/// §1.4). The reports arrive with A9, which answers from the sheets they fill; until then no item has any. A test may register
/// its own answer, as the tours did with theirs before their reports existed (M2, T6a).
/// </summary>
public interface ISheetItemReports
{
    Task<bool> AnyAsync(long itemId, CancellationToken cancellationToken = default);
}

/// <summary>The answer until the reports exist: no item has any.</summary>
internal sealed class NoSheetItemReports : ISheetItemReports
{
    public Task<bool> AnyAsync(long itemId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
