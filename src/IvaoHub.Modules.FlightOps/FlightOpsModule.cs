using FluentValidation;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Preferences;
using IvaoHub.Modules.FlightOps.Aircraft;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Threads;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;

namespace IvaoHub.Modules.FlightOps;

/// <summary>
/// The tours of the division (M2): the section "Tours" of the site and of the back office, inherited from
/// Toursystem and designed in <c>docs/internal/05-design-m2.md</c>. T5 is its skeleton: the context, the
/// permissions, the settings, and the aircraft data every tour will read.
/// <para>It does not belong to a department (note 2026-09-13-moduli-non-subordinati-ai-dipartimenti): its
/// rows have a base department, <c>division.json → modules.flightops.baseDepartment</c>, and who does what
/// is the grants of <c>positionGrants</c>, never a rule written here.</para>
/// </summary>
public sealed class FlightOpsModule : ModuleBase
{
    public const string ModuleKey = "flightops";

    public override string Key => ModuleKey;

    public override IReadOnlyList<PermissionDescriptor> Permissions => TourPermissions.All;

    public override IReadOnlyList<NavItemDescriptor> StaffNavigation =>
    [
        new NavItemDescriptor("flightops:nav.tours", "/staff/tours", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.review", "/staff/tours/review", TourPermissions.Validate),
        new NavItemDescriptor("flightops:nav.issues", "/staff/tours/issues", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.templates", "/staff/tours/templates", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.rules", "/staff/tours/rules", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.errors", "/staff/tours/errors", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.aircraftProfiles", "/staff/tours/aircraft-profiles", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.aircraftGroups", "/staff/tours/aircraft-groups", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.settings", "/staff/tours/settings", TourPermissions.ManageSettings),
    ];

    /// <summary>
    /// The public errors (T9), the cards of the tours (T10), the queue of the validators (T13b) and what else waits for the staff
    /// (T14b), all always live. Each has
    /// its other half in <c>web/src/modules/flightops/</c>; the manifest test reads this literal.
    /// </summary>
    public override IReadOnlyList<BlockDescriptor> Blocks =>
    [
        new BlockDescriptor("flightops.errorCatalog", Version: 1, BlockKind.Data, AlwaysLive: true),
        new BlockDescriptor("flightops.tourCards", Version: 1, BlockKind.Data, AlwaysLive: true),
        new BlockDescriptor("flightops.reviewQueue", Version: 1, BlockKind.Data, AlwaysLive: true),
        new BlockDescriptor("flightops.openIssues", Version: 1, BlockKind.Data, AlwaysLive: true),
    ];

    /// <summary>The public pages of the tours (T10): <c>/tours</c> and <c>/tours/{slug}</c>, so no page may be «tours».</summary>
    public override IReadOnlyList<string> ReservedSegments => ["tours"];

    /// <summary>How the validator orders the queue (§4.1), kept on their user so they find it on any computer (T13).</summary>
    public override IReadOnlyList<PreferenceDescriptor> Preferences =>
    [
        PreferenceDescriptor.OneOf(ReviewQueueOrder.PreferenceKey, ReviewQueueOrder.ByDate, ReviewQueueOrder.ByTour),
    ];

    /// <summary>The outcomes to the pilot, the digest to the validators, the issues on the legs to the mailbox (§3.5, §4.2.2, §3.11).</summary>
    public override IReadOnlyList<string> NotificationTypes => FlightOpsNotifications.All;

    public override ModuleSettingsDescriptor Settings { get; } =
        ModuleSettingsDescriptor.Create<FlightOpsSettings, FlightOpsSettingsSaveValidator>(
            TourPermissions.ManageSettings,
            new FlightOpsSettings());

    public override IEnumerable<Type> DbContextTypes => [typeof(FlightOpsDbContext)];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<FlightOpsDbContext>(ModuleKey);

        // The rules of its payloads, found by the CRUD engine in the container like the core's.
        services.AddValidatorsFromAssemblyContaining<FlightOpsModule>(includeInternalTypes: true);

        services.AddScoped<TourSaving>();
        services.AddScoped<TourReadiness>();
        services.AddScoped<AllowedAircraftCheck>();
        services.AddScoped<LegBook>();
        services.AddScoped<LegRequest>();
        services.AddScoped<TourChildren>();
        services.AddScoped<OpenParameterCheck>();
        services.AddScoped<EffectiveRules>();
        services.AddScoped<PublicTours>();
        services.AddScoped<IDataBlockProvider, ErrorCatalogProvider>();
        services.AddScoped<IDataBlockProvider, TourCardsProvider>();

        // The reports a tour and its legs have (T11); a test may still answer for them first.
        services.TryAddScoped<ITourReports, PirepTourReports>();
        services.AddScoped<PirepSubmission>();
        services.AddScoped<AtcProposer>();
        services.AddScoped<PirepReview>();
        services.AddScoped<IDataBlockProvider, ReviewQueueProvider>();

        // Disputes, clarifications and issues on the legs (T14b): the threads are the core's, the tours say what they cite.
        services.AddScoped<FlightOpsReferences>();
        services.AddScoped<IContactReferenceResolver>(provider => provider.GetRequiredService<FlightOpsReferences>());
        services.AddScoped<PirepDisputes>();
        services.AddScoped<IDataBlockProvider, OpenIssuesProvider>();

        services.AddScoped<TourReleaseJob>();
        services.AddScoped<PirepWithdrawalJob>();
        services.AddScoped<TrackRetentionJob>();
        services.AddScoped<ReviewDigestJob>();
        services.AddQuartz(quartz => quartz
            .AddJob<TourReleaseJob>(job => job.WithIdentity(TourReleaseJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(TourReleaseJob.JobName)
                .WithIdentity($"{TourReleaseJob.JobName}-quarterly")
                .WithCronSchedule(TourReleaseJob.Cron))
            .AddJob<PirepWithdrawalJob>(job => job.WithIdentity(PirepWithdrawalJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(PirepWithdrawalJob.JobName)
                .WithIdentity($"{PirepWithdrawalJob.JobName}-daily")
                .WithCronSchedule(PirepWithdrawalJob.Cron))
            .AddJob<TrackRetentionJob>(job => job.WithIdentity(TrackRetentionJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(TrackRetentionJob.JobName)
                .WithIdentity($"{TrackRetentionJob.JobName}-daily")
                .WithCronSchedule(TrackRetentionJob.Cron))
            .AddJob<ReviewDigestJob>(job => job.WithIdentity(ReviewDigestJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(ReviewDigestJob.JobName)
                .WithIdentity($"{ReviewDigestJob.JobName}-daily")
                .WithCronSchedule(ReviewDigestJob.Cron)));
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAircraftEndpoints();
        endpoints.MapTourEndpoints();
        endpoints.MapLegEndpoints();
        endpoints.MapShapeEndpoints();
        endpoints.MapRuleEndpoints();
        endpoints.MapPirepEndpoints();
        endpoints.MapReviewEndpoints();
        endpoints.MapLegIssueEndpoints();
    }
}
