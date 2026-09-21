using FluentValidation;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Modules;
using IvaoHub.Modules.FlightOps.Aircraft;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Shape;
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
        new NavItemDescriptor("flightops:nav.templates", "/staff/tours/templates", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.aircraftProfiles", "/staff/tours/aircraft-profiles", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.aircraftGroups", "/staff/tours/aircraft-groups", TourPermissions.View),
        new NavItemDescriptor("flightops:nav.settings", "/staff/tours/settings", TourPermissions.ManageSettings),
    ];

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

        // No reports before T11, which replaces the answer with its own.
        services.TryAddScoped<ITourReports, NoTourReportsYet>();

        services.AddScoped<TourReleaseJob>();
        services.AddQuartz(quartz => quartz
            .AddJob<TourReleaseJob>(job => job.WithIdentity(TourReleaseJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(TourReleaseJob.JobName)
                .WithIdentity($"{TourReleaseJob.JobName}-quarterly")
                .WithCronSchedule(TourReleaseJob.Cron)));
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAircraftEndpoints();
        endpoints.MapTourEndpoints();
        endpoints.MapLegEndpoints();
        endpoints.MapShapeEndpoints();
    }
}
