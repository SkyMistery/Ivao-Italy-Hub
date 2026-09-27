using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Training.Bans;

/// <summary>
/// A ban of a trainee, <c>trn_bans</c> (design M3 §1.5-bis, §2.9): while it holds the member asks for no training, on either
/// ladder, and the trainings already open go on. The shape of the training system of today: who, why, until when — or until
/// somebody lifts it —, who gave it (who wrote the row) and who lifted it, and when. A ban is never deleted: it stays in the
/// trainee's history, and lifting it is writing who and when.
/// <para>A6 makes the table and reads it, for the request; the list, the form, «Ban» and «Lift the ban» are A10's, with
/// <c>Training.Ban</c>, which nobody uses on a ban of their own (<see cref="IHasStakeholder"/>).</para>
/// </summary>
[Audited]
[PermissionArea(TrainingPermissions.Area)]
public sealed class TraineeBan : IOwnedByDepartment, IAuditable, IHasStakeholder
{
    public long Id { get; set; }

    /// <summary>The member banned.</summary>
    public int Vid { get; set; }

    /// <summary>Why, in the mail to the member (A10) and on their page.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Until when; none, until somebody lifts it.</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>Who lifted it before its end; none while nobody did.</summary>
    public int? LiftedBy { get; set; }

    public DateTime? LiftedAt { get; set; }

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>When it was given: it holds from then.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Who gave it.</summary>
    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    public int? StakeholderVid => Vid;

    /// <summary>Whether it holds at that moment: given by then, not over, not lifted.</summary>
    public bool Holds(DateTime at) =>
        CreatedAt <= at
        && (EndsAt is null || EndsAt > at)
        && (LiftedAt is null || LiftedAt > at);
}
