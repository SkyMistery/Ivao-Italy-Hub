import { listSearchSchema } from '../../shared/list';
import type { ModuleManifest } from '../../shared/modules';

import { errorCatalogBlock } from './blocks';
import { tourEditorSearchSchema, tourRuleSearchSchema } from './schemas';
import {
  TOURS_EDIT,
  TOURS_MANAGE_RULES,
  TOURS_MANAGE_SETTINGS,
  TOURS_MANAGE_TEMPLATES,
  TOURS_VIEW,
} from './permissions';
import {
  AircraftGroupForm,
  AircraftGroupsPage,
  AircraftProfileForm,
  AircraftProfilesPage,
} from './screens/aircraft';
import { ErrorForm, ErrorsPage, RuleForm, RulesPage, TourRuleForm } from './screens/rules';
import { FlightOpsSettingsPage } from './screens/settings';
import { CallsignRuleForm, HubForm, RotationForm, TourConstraintForm } from './screens/shape';
import {
  TourEditor,
  TourFromTemplatePage,
  TourSaveAsTemplatePage,
  TourTemplatesPage,
  ToursPage,
} from './screens/tours';

/**
 * The tours (M2), as the front end knows them: `IvaoHub.Modules.FlightOps` on the other side. T5 is the
 * skeleton — the aircraft data and the settings, in the back office — T6 the tours and their templates, T7 their legs
 * and their shape: hubs and rotations, subtours, callsign constraints, each row in a form of its own under its tour; T9
 * the rules and the errors, and the first block, the public errors. Public pages come with the phases that give them
 * something to show.
 */
export const flightOpsManifest: ModuleManifest = {
  key: 'flightops',
  blocks: [errorCatalogBlock],
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
      path: '/staff/tours/$id/hubs/$hubId',
      permission: TOURS_EDIT,
      component: HubForm,
    },
    {
      area: 'staff',
      path: '/staff/tours/$id/rotations/$rotationId',
      permission: TOURS_EDIT,
      component: RotationForm,
    },
    {
      area: 'staff',
      path: '/staff/tours/$id/callsigns/$ruleId',
      permission: TOURS_EDIT,
      component: CallsignRuleForm,
    },
    {
      area: 'staff',
      path: '/staff/tours/$id/constraints/$constraintId',
      permission: TOURS_EDIT,
      component: TourConstraintForm,
    },
    {
      area: 'staff',
      path: '/staff/tours/$id/rules/$tourRuleId',
      permission: TOURS_MANAGE_RULES,
      validateSearch: tourRuleSearchSchema,
      component: TourRuleForm,
    },
    {
      area: 'staff',
      path: '/staff/tours/rules',
      permission: TOURS_VIEW,
      validateSearch: listSearchSchema,
      component: RulesPage,
    },
    {
      area: 'staff',
      path: '/staff/tours/rules/$id',
      permission: TOURS_MANAGE_RULES,
      component: RuleForm,
    },
    {
      area: 'staff',
      path: '/staff/tours/errors',
      permission: TOURS_VIEW,
      validateSearch: listSearchSchema,
      component: ErrorsPage,
    },
    {
      area: 'staff',
      path: '/staff/tours/errors/$id',
      permission: TOURS_MANAGE_RULES,
      component: ErrorForm,
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
