/**
 * The permissions of the events the screens ask about, spelled as `EventsPermissions` spells them on the server
 * (design M4 §6.1).
 */
export const EVENTS_VIEW = 'Events.View';
export const EVENTS_EDIT = 'Events.Edit';
export const EVENTS_DELETE = 'Events.Delete';
export const EVENTS_MANAGE_SETTINGS = 'Events.ManageSettings';

/** The routes of an event, which the flight operations write (§1.4, E4). */
export const EVENT_ROUTES_VIEW = 'EventRoutes.View';
export const EVENT_ROUTES_EDIT = 'EventRoutes.Edit';

/** The slots and the bookings of an event (§1.5, E5): who reads them with whoever booked, who loads and corrects them. */
export const EVENT_BOOKINGS_VIEW = 'EventBookings.View';
export const EVENT_BOOKINGS_EDIT = 'EventBookings.Edit';

/**
 * The ATC of an event (§4.1, E11a): who reads its positions and who is available on them, who opens them — the ATC operations and the
 * staff of the FIRs, each on the FIR the server says.
 */
export const EVENT_ATC_VIEW = 'EventAtc.View';
export const EVENT_ATC_EDIT = 'EventAtc.Edit';

/** The reports of support of the members who took part (§5.3, E14a): read by the events' staff, validated by the members' department. */
export const EVENT_REPORTS_VIEW = 'EventReports.View';
