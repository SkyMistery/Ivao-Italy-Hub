import { listSearchSchema } from '../../shared/list';
import type { ModuleManifest } from '../../shared/modules';

import { TOURS_MANAGE_SETTINGS, TOURS_VIEW } from './permissions';
import {
  AircraftGroupForm,
  AircraftGroupsPage,
  AircraftProfileForm,
  AircraftProfilesPage,
} from './screens/aircraft';
import { FlightOpsSettingsPage } from './screens/settings';

/**
 * The tours (M2), as the front end knows them: `IvaoHub.Modules.FlightOps` on the other side. T5 is the
 * skeleton — the aircraft data and the settings, in the back office. Blocks and public pages come with the
 * phases that give them something to show.
 */
export const flightOpsManifest: ModuleManifest = {
  key: 'flightops',
  blocks: [],
  routes: [
    {
      area: 'staff',
      path: '/staff/tours/aircraft-profiles',
      permission: TOURS_VIEW,
      validateSearch: listSearchSchema,
      component: AircraftProfilesPage,
    },
    {
      area: 'staff',
      path: '/staff/tours/aircraft-profiles/$id',
      permission: TOURS_VIEW,
      component: AircraftProfileForm,
    },
    {
      area: 'staff',
      path: '/staff/tours/aircraft-groups',
      permission: TOURS_VIEW,
      validateSearch: listSearchSchema,
      component: AircraftGroupsPage,
    },
    {
      area: 'staff',
      path: '/staff/tours/aircraft-groups/$id',
      permission: TOURS_VIEW,
      component: AircraftGroupForm,
    },
    {
      area: 'staff',
      path: '/staff/tours/settings',
      permission: TOURS_MANAGE_SETTINGS,
      component: FlightOpsSettingsPage,
    },
  ],
  i18nNamespaces: ['flightops'],
};
