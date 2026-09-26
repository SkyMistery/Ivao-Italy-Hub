import type { ModuleManifest } from '../../shared/modules';

import { TRAINING_MANAGE_SETTINGS, TRAINING_MANAGE_SHEETS } from './permissions';
import { sheetItemFormSearchSchema, sheetItemsSearchSchema } from './schemas';
import { TrainingSettingsPage } from './screens/settings';
import { SheetItemForm, SheetItemsPage } from './screens/sheets';

/**
 * The training (M3), as the front end knows it: `IvaoHub.Modules.Training` on the other side. A4 is the skeleton — the
 * section of the back office, with the settings in it —, A5 the evaluation sheet: its items, per ladder and rating.
 */
export const trainingManifest: ModuleManifest = {
  key: 'training',
  blocks: [],
  routes: [
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
