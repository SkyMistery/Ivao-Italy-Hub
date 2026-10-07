using IvaoHub.Core.Division;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A row of the test module that a member sends and takes back by deleting it (M4, E10h, note
/// 2026-10-07-il-ritiro-di-chi-ha-mandato-la-riga): what a booking of an event is, with nothing of a booking in it. Any member
/// sends one (<see cref="ISubmittedByMembers"/>), it is about them (<see cref="IHasStakeholder"/>, read from a column of its
/// own, as a booking reads its pilot), it is in the care of several departments and audited — and its entity says that its
/// member takes it back (<see cref="WithdrawnByStakeholderAttribute"/>), so that the interceptor's guard lets that member delete
/// it and nobody else who does not hold <c>Sample.Edit</c>.
/// </summary>
[PermissionArea(SampleModule.PermissionArea)]
[Audited]
[WithdrawnByStakeholder]
public sealed class SampleSubmission : IOwnedByDepartment, ISubmittedByMembers, IHasStakeholder
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>The member who sent it, and whom it is about.</summary>
    public int SenderVid { get; set; }

    public int? StakeholderVid => SenderVid;

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    /// <summary>Every department the row is in the care of: its column.</summary>
    public int OwnerDepartmentMask { get; set; }
}

/// <summary>
/// Its twin without the mark (M4, E10h): what a pilot's report is (M2, T11). Its member sends it and keeps changing it, and
/// deleting it is still the department's, as the guard has said of every row a member sends since M1.
/// </summary>
[PermissionArea(SampleModule.PermissionArea)]
[Audited]
public sealed class SampleReport : IOwnedByDepartment, ISubmittedByMembers, IHasStakeholder
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>The member who sent it, and whom it is about.</summary>
    public int SenderVid { get; set; }

    public int? StakeholderVid => SenderVid;

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    /// <summary>Every department the row is in the care of: its column.</summary>
    public int OwnerDepartmentMask { get; set; }
}
