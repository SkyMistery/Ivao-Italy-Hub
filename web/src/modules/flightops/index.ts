import { listSearchSchema } from '../../shared/list';
import type { ModuleManifest } from '../../shared/modules';

import { tourEditorSearchSchema } from './schemas';
import { TOURS_EDIT, TOURS_MANAGE_SETTINGS, TOURS_MANAGE_TEMPLATES, TOURS_VIEW } from './permissions';
import {
  AircraftGroupForm,
  AircraftGroupsPage,
  AircraftProfileForm,
  AircraftProfilesPage,
} from './screens/aircraft';
import { FlightOpsSettingsPage } from './screens/settings';
import {
  TourEditor,
  TourFromTemplatePage,
  TourSaveAsTemplatePage,
  TourTemplatesPage,
  ToursPage,
} from './screens/tours';

/**
 * The tours (M2), as the front end knows them: `IvaoHub.Modules.FlightOps` on the other side. T5 is the
 * skeleton — the aircraft data and the settings, in the back office — and T6 the tours and their templates.
 * Blocks and public pages come with the phases that give them something to show.
 */
export const flightOpsManifest: ModuleManifest = {
  key: 'flightops',
  blocks: [],
  routes: [
    {
      area: 'staff',
      path: '/staff/tours',
      permission: TOURS_VIEW,
      validateSearch: listSearchSchema,
      component: ToursPage,
    },
    {
      area: 'staff',
      path: '/staff/tours/templates',
      permission: TOURS_VIEW,
      validateSearch: listSearchSchema,
      component: TourTemplatesPage,
    },
    {
      area: 'staff',
      path: '/staff/tours/from-template',
      permission: TOURS_EDIT,
      component: TourFromTemplatePage,
    },
    {
      area: 'staff',
      path: '/staff/tours/$id',
      permission: TOURS_VIEW,
      validateSearch: tourEditorSearchSchema,
      component: TourEditor,
    },
    {
      area: 'staff',
      path: '/staff/tours/$id/save-as-template',
      permission: TOURS_MANAGE_TEMPLATES,
      component: TourSaveAsTemplatePage,
    },
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
