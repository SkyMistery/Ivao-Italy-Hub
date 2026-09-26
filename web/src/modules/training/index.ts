import type { ModuleManifest } from '../../shared/modules';

import { TRAINING_MANAGE_SETTINGS } from './permissions';
import { TrainingSettingsPage } from './screens/settings';

/**
 * The training (M3), as the front end knows it: `IvaoHub.Modules.Training` on the other side. A4 is the skeleton — the
 * section of the back office, with the settings in it.
 */
export const trainingManifest: ModuleManifest = {
  key: 'training',
  blocks: [],
  routes: [
    {
      area: 'staff',
      path: '/staff/training/settings',
      permission: TRAINING_MANAGE_SETTINGS,
      component: TrainingSettingsPage,
    },
  ],
  i18nNamespaces: ['training'],
};
