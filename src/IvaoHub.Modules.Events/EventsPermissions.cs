using IvaoHub.Core.Auth.Permissions;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The permissions of the events (design M4 §6.1, note 2026-09-29-chi-lavora-sugli-eventi): five areas, because every piece of
/// the work that another department does is a row of its own, with an area of its own. All of them are held on a department —
/// the base department of the module, which every event has — and who holds them is <c>division.json → positionGrants</c>
/// (§6.2), never this file.
/// <para>Two are denied to whoever a row is about, the super administrator included: nobody confirms their own no-show nor
/// approves handing over their own shift (<see cref="AtcEdit"/>), and nobody validates their own report of support
/// (<see cref="ReportsEdit"/>).</para>
/// </summary>
public static class EventsPermissions
{
    /// <summary>The event itself, its airports and their capacity, its rules of award, the questions of an event in person.</summary>
    public const string Area = "Events";

    /// <summary>The routes of an event, which the flight operations write (§1.4).</summary>
    public const string RoutesArea = "EventRoutes";

    /// <summary>The slots and the bookings, and the registrations and activities of an event in person.</summary>
    public const string BookingsArea = "EventBookings";

    /// <summary>The ATC: the positions to open, the availability of the controllers, the shifts.</summary>
    public const string AtcArea = "EventAtc";

    /// <summary>The reports of support of the members who took part.</summary>
    public const string ReportsArea = "EventReports";

    /// <summary>The events in the back office, drafts and past ones included, and their statistics.</summary>
    public const string View = "Events.View";

    /// <summary>Creating, changing, publishing and cancelling an event; its airports, their capacity, its rules of award.</summary>
    public const string Edit = "Events.Edit";

    /// <summary>
    /// Deleting an event nobody took part in yet. Only the coordinator and the assistant of the base department hold it, and no
    /// grant of a department that collaborates gives it: that is all "whoever collaborates does not delete" takes (§6.3).
    /// </summary>
    public const string Delete = "Events.Delete";

    public const string ManageSettings = "Events.ManageSettings";

    public const string RoutesView = "EventRoutes.View";

    public const string RoutesEdit = "EventRoutes.Edit";

    /// <summary>Slots, bookings, registrations and activities with the VIDs in them; the export the gate manager reads.</summary>
    public const string BookingsView = "EventBookings.View";

    /// <summary>Slots, the import, the generator, the activities; taking a booking or a registration away.</summary>
    public const string BookingsEdit = "EventBookings.Edit";

    /// <summary>Positions, availability, the roster and the register of reliability.</summary>
    public const string AtcView = "EventAtc.View";

    /// <summary>Positions, correcting the roster, confirming or excusing a no-show: never on a row about the one who holds it.</summary>
    public const string AtcEdit = "EventAtc.Edit";

    public const string ReportsView = "EventReports.View";

    /// <summary>Validating a report of support: never one's own.</summary>
    public const string ReportsEdit = "EventReports.Edit";

    public static readonly IReadOnlyList<PermissionDescriptor> All =
    [
        new(View, IsGlobal: false),
        new(Edit, IsGlobal: false),
        new(Delete, IsGlobal: false),
        new(ManageSettings, IsGlobal: false),
        new(RoutesView, IsGlobal: false),
        new(RoutesEdit, IsGlobal: false),
        new(BookingsView, IsGlobal: false),
        new(BookingsEdit, IsGlobal: false),
        new(AtcView, IsGlobal: false),
        new(AtcEdit, IsGlobal: false, DeniedToStakeholder: true),
        new(ReportsView, IsGlobal: false),
        new(ReportsEdit, IsGlobal: false, DeniedToStakeholder: true),
    ];
}
