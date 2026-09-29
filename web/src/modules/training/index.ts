import type { ModuleManifest } from '../../shared/modules';

import { TRAINING_MANAGE_SETTINGS, TRAINING_MANAGE_SHEETS } from './permissions';
import { requestSearchSchema, sheetItemFormSearchSchema, sheetItemsSearchSchema } from './schemas';
import { MinePage } from './screens/mine';
import { RequestPage } from './screens/request';
import { TrainingSettingsPage } from './screens/settings';
import { SheetItemForm, SheetItemsPage } from './screens/sheets';

/**
 * The training (M3), as the front end knows it: `IvaoHub.Modules.Training` on the other side. A4 is the skeleton — the
 * section of the back office, with the settings in it —, A5 the evaluation sheet: its items, per ladder and rating; A6 the
 * trainee's side, the request and their own trainings.
 */
export const trainingManifest: ModuleManifest = {
  key: 'training',
  blocks: [],
  routes: [
    // The trainee's pages (A6): the request and their trainings, only signed in — the login brings them back here. Two
    // addresses of their own under the reserved segment, and none that takes every `/training/…`: an address below it that
    // the module does not answer is still the site's — a page that moved, or «not found».
    {
      area: 'member',
      path: '/training/request',
      validateSearch: requestSearchSchema,
      component: RequestPage,
    },
    {
      area: 'member',
      path: '/training/mine',
      component: MinePage,
    },
    {
      area: 'staff',
      path: '/staff/training/sheets',
      permission: TRAINING_MANAGE_SHEETS,
      validateSearch: sheetItemsSearchSchema,
      component: SheetItemsPage,
    },
    {
      area: 'staff',
      path: '/staff/training/sheets/$id',
      permission: TRAINING_MANAGE_SHEETS,
      validateSearch: sheetItemFormSearchSchema,
      component: SheetItemForm,
    },
    {
      area: 'staff',
      path: '/staff/training/settings',
      permission: TRAINING_MANAGE_SETTINGS,
      component: TrainingSettingsPage,
    },
  ],
  i18nNamespaces: ['training'],
};
