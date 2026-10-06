import type { ModuleManifest } from '../../shared/modules';

import { eventListBlock } from './blocks';
import { EVENTS_EDIT, EVENTS_MANAGE_SETTINGS, EVENTS_VIEW, EVENT_ROUTES_EDIT } from './permissions';
import { eventEditorSearchSchema, eventsPublicSearchSchema, eventsSearchSchema } from './schemas';
import { AirportForm } from './screens/airports';
import { EventCancelPage, EventEditor, EventsPage } from './screens/events';
import { EventPublicPage, EventsPublicPage } from './screens/public';
import { RouteForm } from './screens/routes';
import { EventsSettingsPage } from './screens/settings';

/**
 * The events (M4), as the front end knows them: `IvaoHub.Modules.Events` on the other side. E2 is the skeleton — the
 * section of the back office, with the settings in it; E3a the events in it: their list, the page of one with its settings,
 * its description and its airports, and the page that cancels one; E4 the public side — `/events`, the page of an event, the
 * block of the list — and the routes of an event, in a tab of its page.
 */
export const eventsManifest: ModuleManifest = {
  key: 'events',
  blocks: [eventListBlock],
  routes: [
    // The public side (E4): the events to come and in progress, and one event by its address — where its calendar entry and its
    // line in the search lead. Under `_public`, so they wear the header, the footer and the language switcher of the site.
    {
      area: 'public',
      path: '/events',
      validateSearch: eventsPublicSearchSchema,
      component: EventsPublicPage,
    },
    {
      area: 'public',
      path: '/events/$slug',
      component: EventPublicPage,
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
  ],
  i18nNamespaces: ['events'],
};
