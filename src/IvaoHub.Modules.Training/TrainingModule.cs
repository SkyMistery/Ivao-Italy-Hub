using FluentValidation;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Blocks;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Exams;
using IvaoHub.Modules.Training.Public;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Requests;
using IvaoHub.Modules.Training.Sessions;
using IvaoHub.Modules.Training.Settings;
using IvaoHub.Modules.Training.Sheets;
using IvaoHub.Modules.Training.Staff;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quartz;

namespace IvaoHub.Modules.Training;

/// <summary>
/// The training of the division (M3): the section "Training" of the site and of the back office, which takes over from the
/// training system of today and is designed in <c>docs/internal/07-design-m3.md</c>. A4 is its skeleton: the context, the
/// permissions, the settings, and what the settings are chosen from; A5 the items of the evaluation sheet; A6a the training
/// itself, from the trainee's side: the request, its checks and its cancellation, and the trainee's own trainings; A7 the
/// staff's side of it: the list, accepting and refusing a request, and assigning the trainer, who conducts the trainings assigned
/// to them and no other; A8 the date: the trainer's proposals with the warnings of the calendar, the trainee's choice, the date set
/// by hand, the session in the calendar, its reminder, and the closing of a training that found no date, by the staff or by the
/// night; A9 what the session came to: rescheduled with the notes of the staff, not attended, or reported with the evaluation
/// sheet, the comments and the boxes that make the next training a mock exam — and what the trainee of a training never reads of
/// it, even from the staff's side; A10a the trainee's path as the staff reads it, and the bans; A10b the sessions as the site
/// shows them, with the people only to a signed in reader, and the four blocks of the pages and the dashboards; A10c the exams in
/// the calendar, which whoever examines enters, changes and takes off it, on the rows assigned to them; A7b the trainer on the
/// same rule, with no grant of their own: the training says who its trainer is, and they conduct it with the permission of their
/// position.
/// <para>It does not belong to a department (note 2026-09-13-moduli-non-subordinati-ai-dipartimenti): its rows have a base
/// department, <c>division.json → modules.training.baseDepartment</c>, and who does what is the grants of
/// <c>positionGrants</c>, never a rule written here. Nor does it know the network's rules: the ratings, what comes after one,
/// and where each is trained are the core's to answer (design M3 §1.7), and a test of the module holds it to that.</para>
/// </summary>
public sealed class TrainingModule : ModuleBase
{
    public const string ModuleKey = "training";

    public override string Key => ModuleKey;

    public override IReadOnlyList<PermissionDescriptor> Permissions => TrainingPermissions.All;

    /// <summary>
    /// The staff's side: the trainings (A7), a trainee's path asked by VID, the exams (A10c) and the bans (A10a) — read by whoever does
    /// training —, the sheet (A5) and the settings (A4).
    /// </summary>
    public override IReadOnlyList<NavItemDescriptor> StaffNavigation =>
    [
        new NavItemDescriptor("training:nav.trainings", "/staff/training", TrainingPermissions.View),
        new NavItemDescriptor("training:nav.trainees", "/staff/training/trainees", TrainingPermissions.View),
        new NavItemDescriptor("training:nav.exams", "/staff/training/exams", TrainingPermissions.View),
        new NavItemDescriptor("training:nav.bans", "/staff/training/bans", TrainingPermissions.View),
        new NavItemDescriptor("training:nav.sheets", "/staff/training/sheets", TrainingPermissions.ManageSheets),
        new NavItemDescriptor("training:nav.settings", "/staff/training/settings", TrainingPermissions.ManageSettings),
    ];

    /// <summary>
    /// The blocks of the training (design M3 §4.3, A10b), all always live because each answers for whoever is looking: the sessions
    /// still to be held, with the people only to a signed in reader; the reader's own training; their trainings to move as a trainer;
    /// and the requests to accept and the trainings to assign. Each has its other half in <c>web/src/modules/training/blocks/</c>; the
    /// manifest test reads this literal.
    /// </summary>
    public override IReadOnlyList<BlockDescriptor> Blocks =>
    [
        new BlockDescriptor("training.upcomingSessions", Version: 1, BlockKind.Data, AlwaysLive: true),
        new BlockDescriptor("training.myTraining", Version: 1, BlockKind.Data, AlwaysLive: true),
        new BlockDescriptor("training.trainerQueue", Version: 1, BlockKind.Data, AlwaysLive: true),
        new BlockDescriptor("training.approvalQueue", Version: 1, BlockKind.Data, AlwaysLive: true),
    ];

    /// <summary>The pages of the training, the public ones and the member's, live under <c>/training</c>, so no page may be «training».</summary>
    public override IReadOnlyList<string> ReservedSegments => ["training"];

    /// <summary>The mails of the training (design M3 §5.2): the request received first (A6a), the others with their phases.</summary>
    public override IReadOnlyList<string> NotificationTypes => TrainingNotifications.All;

    public override ModuleSettingsDescriptor Settings { get; } =
        ModuleSettingsDescriptor.Create<TrainingSettings, TrainingSettingsSaveValidator>(
            TrainingPermissions.ManageSettings,
            new TrainingSettings());

    public override IEnumerable<Type> DbContextTypes => [typeof(TrainingDbContext)];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TrainingDbContext>(ModuleKey);

        // The rules of its payloads, found by the CRUD engine in the container like the core's (A5, the first resource).
        services.AddValidatorsFromAssemblyContaining<TrainingModule>(includeInternalTypes: true);

        services.AddScoped<TrainingReference>();

        // Whether a report marks an item of the sheet: the sheets the reports filled answer (A9); a test may still answer first.
        services.TryAddScoped<ISheetItemReports, EvaluationSheetItemReports>();

        // The trainee's side of a training (A6a), and whether they passed the theory exam: their own word, until the network
        // says it (§12 n.15); a test may still answer first.
        services.AddScoped<TrainingPeople>();
        services.AddScoped<TrainingMail>();
        services.AddScoped<TrainingRequests>();
        services.TryAddScoped<ITheoryExamSource, TraineeDeclaration>();

        // The staff's side (A7), the dates (A8), and their jobs: the night that closes the trainings nobody dated in time, in the
        // division's own zone like the core's nightly jobs; and the reminders of the sessions, every quarter of an hour.
        services.AddScoped<StaffTrainings>();
        services.AddScoped<TrainingDates>();
        services.AddScoped<TrainingSessions>();

        // The trainee's path as the staff reads it, and the bans (A10a).
        services.AddScoped<TrainingBans>();
        services.AddScoped<TraineePaths>();

        // The exams in the calendar (A10c).
        services.AddScoped<TrainingExams>();

        // The sessions as the site shows them, and the four blocks (A10b).
        services.AddScoped<PublicSessions>();
        services.AddScoped<IDataBlockProvider, UpcomingSessionsProvider>();
        services.AddScoped<IDataBlockProvider, MyTrainingProvider>();
        services.AddScoped<IDataBlockProvider, TrainerQueueProvider>();
        services.AddScoped<IDataBlockProvider, ApprovalQueueProvider>();

        services.AddScoped<TrainingExpiryJob>();
        services.AddScoped<TrainingRemindersJob>();
        services.AddQuartz(quartz => quartz
            .AddJob<TrainingExpiryJob>(job => job.WithIdentity(TrainingExpiryJob.JobName))
            .AddJob<TrainingRemindersJob>(job => job.WithIdentity(TrainingRemindersJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(TrainingRemindersJob.JobName)
                .WithIdentity($"{TrainingRemindersJob.JobName}-quarterly")
                .WithCronSchedule(TrainingRemindersJob.Cron)));
        services.AddOptions<QuartzOptions>()
            .Configure<IOptions<DivisionOptions>>((options, division) => options.AddTrigger(trigger => trigger
                .ForJob(TrainingExpiryJob.JobName)
                .WithIdentity($"{TrainingExpiryJob.JobName}-nightly")
                .WithCronSchedule(TrainingExpiryJob.Cron, schedule => schedule.InTimeZone(division.Value.ResolveTimeZone()))));
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReferenceEndpoints();
        endpoints.MapSheetItemEndpoints();
        endpoints.MapRequestEndpoints();
        endpoints.MapStaffEndpoints();
        endpoints.MapTraineePathEndpoints();
        endpoints.MapBanEndpoints();
        endpoints.MapExamEndpoints();
        endpoints.MapPublicSessionEndpoints();
    }
}
