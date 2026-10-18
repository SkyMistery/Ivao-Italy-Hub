import type { ModuleManifest } from '../../shared/modules';

import { atcCoverageBlock, eventListBlock, myEventsBlock, staffQueueBlock } from './blocks';
import {
  EVENTS_EDIT,
  EVENTS_MANAGE_SETTINGS,
  EVENTS_VIEW,
  EVENT_ATC_VIEW,
  EVENT_BOOKINGS_EDIT,
  EVENT_BOOKINGS_VIEW,
  EVENT_ROUTES_EDIT,
} from './permissions';
import {
  eventEditorSearchSchema,
  eventPageSearchSchema,
  eventsPublicSearchSchema,
  eventsSearchSchema,
  shiftSearchSchema,
} from './schemas';
import { ActivityForm } from './screens/activities';
import { AirportForm } from './screens/airports';
import { AtcEventPage, AtcEventsPage, AtcPositionForm } from './screens/atc';
import { ShiftForm } from './screens/roster';
import { EventCancelPage, EventDuplicatePage, EventEditor, EventsPage } from './screens/events';
import { MyBookingsPage } from './screens/mine';
import { EventPublicPage, EventsPublicPage } from './screens/public';
import { QuestionForm } from './screens/questions';
import {
  ControllerRegisterPage,
  ControllersRegisterPage,
  PilotRegisterPage,
  PilotsRegisterPage,
} from './screens/registers';
import { RosterPage } from './screens/RosterPage';
import { RouteForm } from './screens/routes';
import { EventsSettingsPage } from './screens/settings';
import { SlotForm, SlotLoadPage } from './screens/slots';

/**
 * The events (M4), as the front end knows them: `IvaoHub.Modules.Events` on the other side. E2 is the skeleton — the
 * section of the back office, with the settings in it; E3a the events in it: their list, the page of one with its settings,
 * its description and its airports, and the page that cancels one; E4 the public side — `/events`, the page of an event, the
 * block of the list — and the routes of an event, in a tab of its page; E5 its slots, in a tab too, with the page that loads
 * them from a table, and listed on its page; E6b the bookings — booked from the page of an event, a member's own on
 * `/events/mine` and in the block `events.myEvents`, the staff's in a tab of the event's page; E8b the page that duplicates an
 * event onto a new date; E16 the events in person — their questions and their registrations in tabs of the event's page, a question on
 * a page of its own, a member's registrations on `/events/mine`; E11a the ATC of an event with a roster — its positions in a tab of the
 * event's page and in a section of their own for whoever does not read the events, a position with who is available on it, and a member's
 * availability on the event's page; E11b the roster of an event under its positions, and a shift on a page of its own; E12 the page of a
 * published roster, a controller's shifts and availabilities on `/events/mine` with «hand over», the staff's requests under the roster, and
 * the block of the coverage of the rosters; E17 the parallel activities of an event in person — in a tab of the event's page with their
 * seats, an activity on a page of its own, the turns with their free seats on the event's page, where a registered member takes and gives
 * back a seat; E13b the registers of the controllers and of the pilots, each with a page per person where the staff decide how a shift went
 * and take a booking not flown out of a record, the outcomes of the shifts under the ATC of an event over, a member's own registers on
 * `/events/mine`, and the block of the staff's queue. */
export const eventsManifest: ModuleManifest = {
  key: 'events',
  blocks: [eventListBlock, myEventsBlock, atcCoverageBlock, staffQueueBlock],
  routes: [
    // The public side (E4): the events to come and in progress, and one event by its address — where its calendar entry and its
    // line in the search lead. Under `_public`, so they wear the header, the footer and the language switcher of the site. The page
    // of an event keeps in its address what its slots are narrowed to (E6b).
    {
      area: 'public',
      path: '/events',
      validateSearch: eventsPublicSearchSchema,
      component: EventsPublicPage,
    },
    {
      area: 'public',
      path: '/events/$slug',
      validateSearch: eventPageSearchSchema,
      component: EventPublicPage,
    },
    // The roster of an event, once published (E12): every shift and every piece nobody holds, the names to a signed in reader.
    {
      area: 'public',
      path: '/events/$slug/roster',
      component: RosterPage,
    },
    // A member's own bookings (E6b, design M4 §7.1): under `_member`, whose guard sends a visitor to the login and back. No event
    // takes the address `mine` (the server refuses it).
    {
      area: 'member',
      path: '/events/mine',
      component: MyBookingsPage,
    },
    {
      area: 'staff',
      path: '/staff/events',
      permission: EVENTS_VIEW,
      validateSearch: eventsSearchSchema,
      component: EventsPage,
    },
    {
      area: 'staff',
      path: '/staff/events/settings',
      permission: EVENTS_MANAGE_SETTINGS,
      component: EventsSettingsPage,
    },
    // The ATC of the events with a roster (E11a), for whoever holds the ATC's area — the staff of a FIR, who do not read the events —:
    // the events, the ATC of one, and a position with who is available on it. Whoever reads the event has the same in a tab of its page.
    {
      area: 'staff',
      path: '/staff/events/atc',
      permission: EVENT_ATC_VIEW,
      component: AtcEventsPage,
    },
    {
      area: 'staff',
      path: '/staff/events/atc/$id',
      permission: EVENT_ATC_VIEW,
      component: AtcEventPage,
    },
    {
      area: 'staff',
      path: '/staff/events/atc/$id/positions/$positionId',
      permission: EVENT_ATC_VIEW,
      component: AtcPositionForm,
    },
    // A shift of the roster (E11b): «Add» on an uncovered piece opens it with that position and those hours, «Move» on a shift.
    {
      area: 'staff',
      path: '/staff/events/atc/$id/shifts/$shiftId',
      permission: EVENT_ATC_VIEW,
      validateSearch: shiftSearchSchema,
      component: ShiftForm,
    },
    // The registers (E13b): the controllers' and the pilots', each with a page per person. Above `/staff/events/$id`, which would take
    // `controllers` and `pilots` as an id.
    {
      area: 'staff',
      path: '/staff/events/controllers',
      permission: EVENT_ATC_VIEW,
      component: ControllersRegisterPage,
    },
    {
      area: 'staff',
      path: '/staff/events/controllers/$vid',
      permission: EVENT_ATC_VIEW,
      component: ControllerRegisterPage,
    },
    {
      area: 'staff',
      path: '/staff/events/pilots',
      permission: EVENT_BOOKINGS_VIEW,
      component: PilotsRegisterPage,
    },
    {
      area: 'staff',
      path: '/staff/events/pilots/$vid',
      permission: EVENT_BOOKINGS_VIEW,
      component: PilotRegisterPage,
    },
    {
      area: 'staff',
      path: '/staff/events/$id',
      permission: EVENTS_VIEW,
      validateSearch: eventEditorSearchSchema,
      component: EventEditor,
    },
    {
      area: 'staff',
      path: '/staff/events/$id/cancel',
      permission: EVENTS_EDIT,
      component: EventCancelPage,
    },
    // «Duplicate» (E8b): the start and the address of a new draft copied from the event.
    {
      area: 'staff',
      path: '/staff/events/$id/duplicate',
      permission: EVENTS_EDIT,
      component: EventDuplicatePage,
    },
    // A question of an event in person (E16): written by whoever writes the event.
    {
      area: 'staff',
      path: '/staff/events/$id/questions/$questionId',
      permission: EVENTS_EDIT,
      component: QuestionForm,
    },
    // A parallel activity of an event in person (E17): written by whoever writes the bookings of the event, its area.
    {
      area: 'staff',
      path: '/staff/events/$id/activities/$activityId',
      permission: EVENT_BOOKINGS_EDIT,
      component: ActivityForm,
    },
    {
      area: 'staff',
      path: '/staff/events/$id/airports/$airportId',
      permission: EVENTS_EDIT,
      component: AirportForm,
    },
    // A route of an event (E4): written by whoever holds the routes' area, the flight operations among them, who do not write
    // the event.
    {
      area: 'staff',
      path: '/staff/events/$id/routes/$routeId',
      permission: EVENT_ROUTES_EDIT,
      component: RouteForm,
    },
    // The slots of an event (E5): loaded from a table, all or nothing, and each one corrected in its form — by whoever writes the
    // bookings of the event.
    {
      area: 'staff',
      path: '/staff/events/$id/slots/load',
      permission: EVENT_BOOKINGS_EDIT,
      component: SlotLoadPage,
    },
    {
      area: 'staff',
      path: '/staff/events/$id/slots/$slotId',
      permission: EVENT_BOOKINGS_EDIT,
      component: SlotForm,
    },
  ],
  i18nNamespaces: ['events'],
};
