using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Modules;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Export;
using IvaoHub.Modules.Events.Public;
using IvaoHub.Modules.Events.Settings;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The events of the division (M4): the section "Events" of the site and of the back office, which takes over from the booking
/// system of today and is designed in <c>docs/internal/09-design-m4.md</c>. E2 is its skeleton: the context with the event whole
/// and its airports, the catalogue of the five areas, the settings of M4a and the section of the back office that holds them;
/// E3a the event in the staff's back office: its list with the views of its state, its page — the generated form with the
/// switches its kind presets, the description, the banner — its airports with their capacity, and cancelling and deleting it;
/// E3b its life: publishing it, its calendar entry, its line in the search and its files, and the job that projects it again when
/// it is seen and when it ends; E4 its public side — the page of an event, the list of <c>/events</c> as the block
/// <c>events.eventList</c> — and the routes the flight operations write; E5 its public slots — loaded from a table, with their
/// rotations, listed and corrected in the back office and listed on its page — and the export the gate manager reads with a
/// personal token; E6a the bookings on the server — a pilot books a public slot or a whole rotation and withdraws, the staff take a
/// booking away with a reason, and an event somebody booked tells them when it is cancelled or its times change —; E6b their pages —
/// the staff's list of the bookings of an event, the block <c>events.myEvents</c> of a pilot's bookings still to fly — and the
/// reminder of the day before, <c>events-reminders</c>.
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

    /// <summary>The staff's side: the events (E3a), and the settings (E2).</summary>
    public override IReadOnlyList<NavItemDescriptor> StaffNavigation =>
    [
        new NavItemDescriptor("events:nav.events", "/staff/events", EventsPermissions.View),
        new NavItemDescriptor("events:nav.settings", "/staff/events/settings", EventsPermissions.ManageSettings),
    ];

    /// <summary>
    /// The events to come and in progress (E4), always live: on <c>/events</c> and on any page; and the reader's bookings still to fly
    /// (E6b), on <c>/me</c>. Their other halves are in <c>web/src/modules/events/</c>; the manifest test reads these literals.
    /// </summary>
    public override IReadOnlyList<BlockDescriptor> Blocks =>
    [
        new BlockDescriptor("events.eventList", Version: 1, BlockKind.Data, AlwaysLive: true),
        new BlockDescriptor("events.myEvents", Version: 1, BlockKind.Data, AlwaysLive: true),
    ];

    /// <summary>
    /// The pages of the events, the public ones and the member's, live under <c>/events</c> (§0.4), so no page may be «events».
    /// </summary>
    public override IReadOnlyList<string> ReservedSegments => ["events"];

    /// <summary>The mails of the events (design M4 §8.3): each declared by the phase of the change it tells, cancelling first (E3a).</summary>
    public override IReadOnlyList<string> NotificationTypes => EventsNotifications.All;

    /// <summary>
    /// The gate manager of the division (design M4 §7.4, E5): its personal tokens open the export of the bookings of an event and
    /// nothing else, and only for whoever reads the bookings. The word for it is <c>events:tokenAudiences.bookings</c>.
    /// </summary>
    public override IReadOnlyList<TokenAudienceDescriptor> TokenAudiences =>
        [new TokenAudienceDescriptor(BookingsExport.Audience, EventsPermissions.BookingsView)];

    public override ModuleSettingsDescriptor Settings { get; } =
        ModuleSettingsDescriptor.Create<EventsSettings, EventsSettingsSaveValidator>(
            EventsPermissions.ManageSettings,
            new EventsSettings());

    public override IEnumerable<Type> DbContextTypes => [typeof(EventsDbContext)];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<EventsDbContext>(ModuleKey);

        // The rules of its payloads, found by the CRUD engine in the container like the core's (E3a, the first resource).
        services.AddValidatorsFromAssemblyContaining<EventsModule>(includeInternalTypes: true);

        // The event in the staff's back office (E3a): what a save of it asks of other rows, and the rows of its staff.
        services.AddScoped<EventSaving>();
        services.AddScoped<EventChildren>();

        // Its life (E3b): what publishing it asks, and the job that projects it again when it is seen and when it ends.
        services.AddScoped<EventPublishing>();
        services.AddScoped<EventReleaseJob>();
        services.AddQuartz(quartz => quartz
            .AddJob<EventReleaseJob>(job => job.WithIdentity(EventReleaseJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(EventReleaseJob.JobName)
                .WithIdentity($"{EventReleaseJob.JobName}-quarterly")
                .WithCronSchedule(EventReleaseJob.Cron)));

        // Its public side (E4): the page of an event and the cards of the events seen, which the block and /events read.
        services.AddScoped<PublicEvents>();
        services.AddScoped<IDataBlockProvider, EventListProvider>();

        // Its public slots (E5): a table loaded all or nothing, and what a correction of one slot asks of the others.
        services.AddScoped<SlotLoading>();
        services.AddScoped<SlotSaving>();

        // The bookings (E6a): the pilot's verbs under their lock, and the mails of the module.
        services.AddScoped<PilotBookings>();
        services.AddScoped<EventsMail>();

        // Their pages (E6b): the staff's list with the pilots' names, the block of a pilot's bookings, and the reminder of the day before.
        services.AddScoped<EventsPeople>();
        services.AddScoped<StaffBookings>();
        services.AddScoped<IDataBlockProvider, MyEventsProvider>();
        services.AddScoped<BookingRemindersJob>();
        services.AddQuartz(quartz => quartz
            .AddJob<BookingRemindersJob>(job => job.WithIdentity(BookingRemindersJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(BookingRemindersJob.JobName)
                .WithIdentity($"{BookingRemindersJob.JobName}-quarterly")
                .WithCronSchedule(BookingRemindersJob.Cron, schedule => schedule.InTimeZone(TimeZoneInfo.Utc))));
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapEventEndpoints();
        endpoints.MapEventAirportEndpoints();
        endpoints.MapEventRouteEndpoints();
        endpoints.MapEventSlotEndpoints();
        endpoints.MapPublicEventEndpoints();
        endpoints.MapBookingsExport();
        endpoints.MapBookingEndpoints();
    }
}
