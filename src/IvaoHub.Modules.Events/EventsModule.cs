using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Privacy;
using IvaoHub.Modules.Events.After;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Export;
using IvaoHub.Modules.Events.Public;
using IvaoHub.Modules.Events.Registrations;
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
/// reminder of the day before, <c>events-reminders</c>; E7 its private slots — generated from the capacity of its airports, booked
/// with the flight the pilot flies and an arrival's linked departure, exported with the slot each is paired with —; E8b «Duplicate», a
/// draft of an event onto a new date, and the module's half of erasing a person's data; E16 the events in person — their questions,
/// a member's registration with the answers, the sums the staff read — and the statistics of an event, born whole; E11a the ATC of an
/// event with a roster — the positions it opens, which the staff of a FIR write on their FIR, and a controller's availability with its
/// windows, given from the event's page and read by the staff per position; E11b its roster — proposed once at the close by
/// <c>events-roster</c>, with the why of every shift, and corrected by the staff, with warnings where a shift leaves the rules; E17 the
/// parallel activities of an event in person — a member's seat in a turn, counted under a lock —, «Duplicate» for the questions and the
/// activities, and the short retention of the registrations, <c>events-retention</c>; E13a the checks after an event — <c>events-after</c>
/// finds the bookings flown, the shifts attended or a no-show to propose, and counts who flew without booking — and its statistics, with
/// «Recalculate» for the staff.
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

    /// <summary>
    /// The staff's side: the events (E3a), the ATC of the events with a roster (E11a) — for whoever writes the positions without reading
    /// the events, the staff of a FIR —, and the settings (E2).
    /// </summary>
    public override IReadOnlyList<NavItemDescriptor> StaffNavigation =>
    [
        new NavItemDescriptor("events:nav.events", "/staff/events", EventsPermissions.View),
        new NavItemDescriptor("events:nav.atc", "/staff/events/atc", EventsPermissions.AtcView),
        new NavItemDescriptor("events:nav.settings", "/staff/events/settings", EventsPermissions.ManageSettings),
    ];

    /// <summary>
    /// The events to come and in progress (E4), always live: on <c>/events</c> and on any page; the reader's bookings still to fly
    /// (E6b) and shifts (E12), on <c>/me</c>; and the coverage of the rosters of the next days (E12), on <c>/staff</c>. Their other halves
    /// are in <c>web/src/modules/events/</c>; the manifest test reads these literals.
    /// </summary>
    public override IReadOnlyList<BlockDescriptor> Blocks =>
    [
        new BlockDescriptor("events.eventList", Version: 1, BlockKind.Data, AlwaysLive: true),
        new BlockDescriptor("events.myEvents", Version: 1, BlockKind.Data, AlwaysLive: true),
        new BlockDescriptor("events.atcCoverage", Version: 1, BlockKind.Data, AlwaysLive: true),
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

        // Its life (E3b): what publishing it asks, and the job that projects it again when it is seen and when it ends — in UTC, which
        // its trigger says (E10j: a quarter of an hour falls on the same instants in every zone, and the zone is written all the same).
        services.AddScoped<EventPublishing>();
        services.AddScoped<EventReleaseJob>();

        // Every quarter of an hour, which falls on the same instants in every zone; the zone is said all the same (note
        // 2026-09-28-i-job-quando-passenger-spegne-l-hub, §7), and it is UTC, as the reminders of the bookings have it.
        services.AddQuartz(quartz => quartz
            .AddJob<EventReleaseJob>(job => job.WithIdentity(EventReleaseJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(EventReleaseJob.JobName)
                .WithIdentity(EventReleaseJob.TriggerName)
                .WithCronSchedule(EventReleaseJob.Cron, schedule => schedule.InTimeZone(TimeZoneInfo.Utc))));

        // Its public side (E4): the page of an event and the cards of the events seen, which the block and /events read.
        services.AddScoped<PublicEvents>();
        services.AddScoped<IDataBlockProvider, EventListProvider>();

        // Its public slots (E5): a table loaded all or nothing, and what a correction of one slot asks of the others. Its private
        // slots (E7): generated from the capacity of its airports.
        services.AddScoped<SlotLoading>();
        services.AddScoped<SlotSaving>();
        services.AddScoped<PrivateSlotGeneration>();

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

        // «Duplicate» (E8b): a draft of an event onto a new date, with its rows of the staff and nothing of the members.
        services.AddScoped<EventDuplication>();

        // The events in person (E16): a member's registrations with the answers to the event's questions, and the staff's list of them.
        services.AddScoped<MemberRegistrations>();
        services.AddScoped<StaffRegistrations>();

        // Their parallel activities (E17): a member's seats in the turns, under the locks of their registration and of the activity, the
        // staff's list of them, and the short retention — the registrations and the seats go, the sums stay in the statistics.
        services.AddScoped<MemberSeats>();
        services.AddScoped<StaffSeats>();
        services.AddScoped<InPersonRetention>();
        services.AddScoped<EventsRetentionJob>();
        services.AddQuartz(quartz => quartz
            .AddJob<EventsRetentionJob>(job => job.WithIdentity(EventsRetentionJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(EventsRetentionJob.JobName)
                .WithIdentity(EventsRetentionJob.TriggerName)
                .WithCronSchedule(EventsRetentionJob.Cron, schedule => schedule.InTimeZone(TimeZoneInfo.Utc))));

        // The ATC (E11a): the positions an event opens, read with their candidates by the staff, and a member's availability.
        services.AddScoped<StaffAvailability>();
        services.AddScoped<MemberAvailability>();

        // The roster (E11b): proposed once at the close of the availabilities by events-roster, in UTC, and corrected by the staff.
        services.AddScoped<RosterData>();
        services.AddScoped<RosterProposal>();
        services.AddScoped<StaffRoster>();
        services.AddScoped<ShiftLock>();
        services.AddScoped<EventRosterJob>();

        // Its publication at a date and the mails of the shifts (E12), the second half of events-roster; the page of a roster; a controller's
        // shifts and the requests to hand one over; and the coverage of the rosters of the next days for the staff.
        services.AddScoped<RosterPublication>();
        services.AddScoped<ShiftNotices>();
        services.AddScoped<PublicRoster>();
        services.AddScoped<MemberShifts>();
        services.AddScoped<StaffTransfers>();
        services.AddScoped<IDataBlockProvider, AtcCoverageProvider>();
        services.AddQuartz(quartz => quartz
            .AddJob<EventRosterJob>(job => job.WithIdentity(EventRosterJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(EventRosterJob.JobName)
                .WithIdentity($"{EventRosterJob.JobName}-quarterly")
                .WithCronSchedule(EventRosterJob.Cron, schedule => schedule.InTimeZone(TimeZoneInfo.Utc))));

        // After the event (E13a): events-after every hour, in UTC — the bookings flown, the shifts attended, who flew without booking —, and
        // the statistics, worked out by it and again by the staff.
        services.AddScoped<EventStatistics>();
        services.AddScoped<EventsAfterJob>();
        services.AddQuartz(quartz => quartz
            .AddJob<EventsAfterJob>(job => job.WithIdentity(EventsAfterJob.JobName))
            .AddTrigger(trigger => trigger
                .ForJob(EventsAfterJob.JobName)
                .WithIdentity(EventsAfterJob.TriggerName)
                .WithCronSchedule(EventsAfterJob.Cron, schedule => schedule.InTimeZone(TimeZoneInfo.Utc))));

        // The module's half of erasing a person's data (E8b): a booking of an event still to end goes, one of an event over stays
        // without them.
        services.AddScoped<IPersonalDataEraser, EventsPersonalData>();
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
        endpoints.MapEventQuestionEndpoints();
        endpoints.MapEventActivityEndpoints();
        endpoints.MapRegistrationEndpoints();
        endpoints.MapAtcPositionEndpoints();
        endpoints.MapAvailabilityEndpoints();
        endpoints.MapShiftEndpoints();
        endpoints.MapTransferEndpoints();
        endpoints.MapPublicRosterEndpoints();
        endpoints.MapStatsEndpoints();
    }
}
