import type { ModuleManifest } from '../../shared/modules';

import { EVENTS_EDIT, EVENTS_MANAGE_SETTINGS, EVENTS_VIEW } from './permissions';
import { eventEditorSearchSchema, eventsSearchSchema } from './schemas';
import { AirportForm } from './screens/airports';
import { EventCancelPage, EventEditor, EventsPage } from './screens/events';
import { EventsSettingsPage } from './screens/settings';

/**
 * The events (M4), as the front end knows them: `IvaoHub.Modules.Events` on the other side. E2 is the skeleton — the
 * section of the back office, with the settings in it; E3a the events in it: their list, the page of one with its settings,
 * its description and its airports, and the page that cancels one.
 */
export const eventsManifest: ModuleManifest = {
  key: 'events',
  blocks: [],
  routes: [
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
  ],
  i18nNamespaces: ['events'],
};
