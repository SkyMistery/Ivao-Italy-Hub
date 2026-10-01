using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Modules;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The events of the division (M4): the section "Events" of the site and of the back office, which takes over from the booking
/// system of today and is designed in <c>docs/internal/09-design-m4.md</c>. E2 is its skeleton: the context with the event whole
/// and its airports, the catalogue of the five areas, the settings of M4a and the section of the back office that holds them.
/// <para>It does not belong to a department (note 2026-09-13-moduli-non-subordinati-ai-dipartimenti): every event has a base
/// department, <c>division.json → modules.events.baseDepartment</c>, and who does what is the grants of <c>positionGrants</c>,
/// never a rule written here. Nor does it know the network, the kinds of event of a division or its airports: the kinds are
/// words of the calendar, the airports and the ratings the core's, and a test of the module holds it to that.</para>
/// </summary>
public sealed class EventsModule : ModuleBase
{
    public const string ModuleKey = "events";

    public override string Key => ModuleKey;

    public override IReadOnlyList<PermissionDescriptor> Permissions => EventsPermissions.All;

    /// <summary>The staff's side: for now the settings (E2); the list of the events and the page of one arrive with E3a.</summary>
    public override IReadOnlyList<NavItemDescriptor> StaffNavigation =>
    [
        new NavItemDescriptor("events:nav.settings", "/staff/events/settings", EventsPermissions.ManageSettings),
    ];

    /// <summary>
    /// The pages of the events, the public ones and the member's, live under <c>/events</c> (§0.4), so no page may be «events».
    /// </summary>
    public override IReadOnlyList<string> ReservedSegments => ["events"];

    public override ModuleSettingsDescriptor Settings { get; } =
        ModuleSettingsDescriptor.Create<EventsSettings, EventsSettingsSaveValidator>(
            EventsPermissions.ManageSettings,
            new EventsSettings());

    public override IEnumerable<Type> DbContextTypes => [typeof(EventsDbContext)];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<EventsDbContext>(ModuleKey);
}
