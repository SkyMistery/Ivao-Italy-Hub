import type { ModuleManifest } from '../../shared/modules';

import { EVENTS_MANAGE_SETTINGS } from './permissions';
import { EventsSettingsPage } from './screens/settings';

/**
 * The events (M4), as the front end knows them: `IvaoHub.Modules.Events` on the other side. E2 is the skeleton — the
 * section of the back office, with the settings in it.
 */
export const eventsManifest: ModuleManifest = {
  key: 'events',
  blocks: [],
  routes: [
    {
      area: 'staff',
      path: '/staff/events/settings',
      permission: EVENTS_MANAGE_SETTINGS,
      component: EventsSettingsPage,
    },
  ],
  i18nNamespaces: ['events'],
};
