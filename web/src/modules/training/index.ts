import type { ModuleManifest } from '../../shared/modules';

import { TRAINING_MANAGE_SETTINGS, TRAINING_MANAGE_SHEETS, TRAINING_VIEW } from './permissions';
import {
  banFormSearchSchema,
  bansSearchSchema,
  requestSearchSchema,
  sheetItemFormSearchSchema,
  sheetItemsSearchSchema,
  staffTrainingsSearchSchema,
} from './schemas';
import { BanForm, BansPage } from './screens/bans';
import { MinePage } from './screens/mine';
import { RequestPage } from './screens/request';
import { TrainingSettingsPage } from './screens/settings';
import { SheetItemForm, SheetItemsPage } from './screens/sheets';
import { StaffTrainingPage, StaffTrainingsPage } from './screens/staff';
import { TraineeTrainingPage } from './screens/traineeTraining';
import { TraineeLookupPage, TraineePathPage } from './screens/trainees';

/**
 * The training (M3), as the front end knows it: `IvaoHub.Modules.Training` on the other side. A4 is the skeleton — the
 * section of the back office, with the settings in it —, A5 the evaluation sheet: its items, per ladder and rating; A6 the
 * trainee's side, the request and their own trainings; A7 the staff's side, every training and the page of one, where a
 * request is accepted or refused and its trainer assigned; A8 the dates — proposed by the trainer with what they meet,
 * chosen by the trainee on the page of their training, or set by hand —, the session, and the closing; A9 what the session
 * came to — rescheduled, not attended, or reported with the sheet —, on the same two pages of a training; A10a a trainee's
 * path as the staff reads it, and the bans.
 */
export const trainingManifest: ModuleManifest = {
  key: 'training',
  blocks: [],
  routes: [
    // The trainee's pages (A6, A8): the request, their trainings and the page of one, only signed in — the login brings them
    // back here. Addresses of their own under the reserved segment, and none that takes every `/training/…`: an address below
    // it that the module does not answer is still the site's — a page that moved, or «not found».
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
      area: 'member',
      path: '/training/mine/$id',
      component: TraineeTrainingPage,
    },
    // The staff's side (A7): every training, open and closed, to whoever holds `Training.View` (R.1); what they may do on one
    // is the server's answer on its page.
    {
      area: 'staff',
      path: '/staff/training',
      permission: TRAINING_VIEW,
      validateSearch: staffTrainingsSearchSchema,
      component: StaffTrainingsPage,
    },
    {
      area: 'staff',
      path: '/staff/training/$id',
      permission: TRAINING_VIEW,
      component: StaffTrainingPage,
    },
    // A trainee's path, asked by VID, and the bans (A10a): read by whoever does training; banning and lifting ask more, on the
    // page and on the server. A ban given is not edited: its form answers `new` only.
    {
      area: 'staff',
      path: '/staff/training/trainees',
      permission: TRAINING_VIEW,
      component: TraineeLookupPage,
    },
    {
      area: 'staff',
      path: '/staff/training/trainees/$id',
      permission: TRAINING_VIEW,
      component: TraineePathPage,
    },
    {
      area: 'staff',
      path: '/staff/training/bans',
      permission: TRAINING_VIEW,
      validateSearch: bansSearchSchema,
      component: BansPage,
    },
    {
      area: 'staff',
      path: '/staff/training/bans/$id',
      permission: TRAINING_VIEW,
      validateSearch: banFormSearchSchema,
      component: BanForm,
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
