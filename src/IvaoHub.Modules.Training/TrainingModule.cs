using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Modules;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Settings;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Training;

/// <summary>
/// The training of the division (M3): the section "Training" of the site and of the back office, which takes over from the
/// training system of today and is designed in <c>docs/internal/07-design-m3.md</c>. A4 is its skeleton: the context, the
/// permissions, the settings, and what the settings are chosen from.
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

    public override IReadOnlyList<NavItemDescriptor> StaffNavigation =>
    [
        new NavItemDescriptor("training:nav.settings", "/staff/training/settings", TrainingPermissions.ManageSettings),
    ];

    /// <summary>The pages of the training, the public ones and the member's, live under <c>/training</c>, so no page may be «training».</summary>
    public override IReadOnlyList<string> ReservedSegments => ["training"];

    public override ModuleSettingsDescriptor Settings { get; } =
        ModuleSettingsDescriptor.Create<TrainingSettings, TrainingSettingsSaveValidator>(
            TrainingPermissions.ManageSettings,
            new TrainingSettings());

    public override IEnumerable<Type> DbContextTypes => [typeof(TrainingDbContext)];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TrainingDbContext>(ModuleKey);
        services.AddScoped<TrainingReference>();
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapReferenceEndpoints();
}
