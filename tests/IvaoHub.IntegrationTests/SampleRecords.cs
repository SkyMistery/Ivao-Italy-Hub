using IvaoHub.Core.Division;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A row of the test module that two permissions besides <c>Sample.Edit</c> write (M3, A3, note
/// 2026-09-25-i-permessi-alternativi-e-la-creazione): what a training and an exam will be, with nothing of either in it.
/// <c>Sample.Decide</c> changes one, the way a trainer conducts the training assigned to them; <c>Sample.Record</c> also
/// brings one into existence, the way whoever examines enters an exam in the calendar. It is in the care of several
/// departments, answers with a scope of its own and may be about a member, so that every condition the interceptor's guard
/// puts on an alternative is proved against a real module context and a real table.
/// </summary>
[PermissionArea(SampleModule.PermissionArea)]
[AlsoWrittenWith(SampleModule.DecidePermission)]
[AlsoWrittenWith(SampleModule.RecordPermission, AlsoOnCreation = true)]
public sealed class SampleRecord : IOwnedByDepartment, IAuditable, IHasResourceScope, IHasStakeholder
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>The member this row is about, when it is about one.</summary>
    public int? StakeholderVid { get; set; }

    /// <summary>What a grant has to name to reach this row alone.</summary>
    public string ResourceScope => $"{SampleModule.ModuleKey}:record:{Id}";

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    /// <summary>Every department the row is in the care of: its column.</summary>
    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }
}
