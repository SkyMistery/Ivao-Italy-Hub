using IvaoHub.Core.Division;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A part of an item of the test module (M4, E10i, note 2026-10-09-il-grant-su-una-riga-scrive-la-sua-riga): what a slot or a route
/// of an event is, with nothing of either in it. It answers with the scope of its item (<see cref="SampleItem.ScopeOf"/>), as a
/// slot answers with its event's, so that a permission granted on the item reaches its parts — and brings new ones into existence —,
/// while a new item answers with its own scope, which names a key the database has not given yet. In the care of the item's
/// departments, written with <c>Sample.Edit</c> and audited, as the rows of the staff of an event are.
/// <para>Two alternatives as well, as on <see cref="SampleRecord"/>: <c>Sample.Decide</c> changes a part, and <c>Sample.Record</c>
/// also creates one, so that the guard is seen to ask an alternative where <c>Edit</c> is asked — with the scope the part answers
/// with, a new one included — and to let none move a part to another item.</para>
/// </summary>
[PermissionArea(SampleModule.PermissionArea)]
[Audited]
[AlsoWrittenWith(SampleModule.DecidePermission)]
[AlsoWrittenWith(SampleModule.RecordPermission, AlsoOnCreation = true)]
public sealed class SamplePart : IOwnedByDepartment, IHasResourceScope
{
    public long Id { get; set; }

    /// <summary>The item the part belongs to: a plain column, as a slot names its event.</summary>
    public long ItemId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>The item's: a permission granted on the item reaches its parts.</summary>
    public string ResourceScope => SampleItem.ScopeOf(ItemId);

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    /// <summary>Every department the row is in the care of: its column.</summary>
    public int OwnerDepartmentMask { get; set; }
}
