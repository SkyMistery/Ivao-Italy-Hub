using IvaoHub.Core.Auth.Permissions;

namespace IvaoHub.Modules.Training;

/// <summary>
/// The permissions of the training (design M3 §3.1). All of them are held on a department — the base department of the
/// module — and who holds them is <c>division.json → positionGrants</c> (§3.2), never this file.
/// <para>Five are denied to whoever a training is about: nobody approves, assigns, conducts, edits or bans on a training of
/// their own, the super administrator included (§3, §10).</para>
/// </summary>
public static class TrainingPermissions
{
    /// <summary>The area the CRUD engine derives <c>Training.View</c> and <c>Training.Edit</c> from.</summary>
    public const string Area = "Training";

    /// <summary>Every training in the back office, open and closed; never denied, because the core never denies reading (§12 n.13).</summary>
    public const string View = "Training.View";

    /// <summary>Accepting, refusing, closing a request nobody answered.</summary>
    public const string Approve = "Training.Approve";

    /// <summary>Assigning the trainer, and changing them.</summary>
    public const string Assign = "Training.Assign";

    /// <summary>Dates, rescheduling, no-show, sheet and report; a trainer holds it on the trainings assigned to them only (§3.3).</summary>
    public const string Conduct = "Training.Conduct";

    /// <summary>Everything on every training, and the permission the write guard asks of the staff's own rows.</summary>
    public const string Edit = "Training.Edit";

    public const string ManageSheets = "Training.ManageSheets";

    /// <summary>
    /// The exams in the calendar, put there by whoever holds the exam (§12 n.10). An exam is assigned only to an examiner — the
    /// direction, the coordinator and the assistant of the training department, or one of its advisors — and never to a
    /// trainer, so the trainers do not hold it.
    /// </summary>
    public const string ManageExams = "Training.ManageExams";

    public const string Ban = "Training.Ban";

    public const string ManageSettings = "Training.ManageSettings";

    public static readonly IReadOnlyList<PermissionDescriptor> All =
    [
        new(View, IsGlobal: false),
        new(Approve, IsGlobal: false, DeniedToStakeholder: true),
        new(Assign, IsGlobal: false, DeniedToStakeholder: true),
        new(Conduct, IsGlobal: false, DeniedToStakeholder: true),
        new(Edit, IsGlobal: false, DeniedToStakeholder: true),
        new(ManageSheets, IsGlobal: false),
        new(ManageExams, IsGlobal: false),
        new(Ban, IsGlobal: false, DeniedToStakeholder: true),
        new(ManageSettings, IsGlobal: false),
    ];
}
