import type { ModuleManifest } from '../../shared/modules';

import { approvalQueueBlock, myTrainingBlock, trainerQueueBlock, upcomingSessionsBlock } from './blocks';
import { TRAINING_MANAGE_SETTINGS, TRAINING_MANAGE_SHEETS, TRAINING_VIEW } from './permissions';
import {
  banFormSearchSchema,
  bansSearchSchema,
  examsSearchSchema,
  requestSearchSchema,
  sheetItemFormSearchSchema,
  sheetItemsSearchSchema,
  staffTrainingsSearchSchema,
} from './schemas';
import { BanForm, BansPage } from './screens/bans';
import { ExamForm, ExamsPage } from './screens/exams';
import { MinePage } from './screens/mine';
import { PublicSessionPage, TrainingPublicPage } from './screens/public';
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
 * path as the staff reads it, and the bans; A10b the public side — the sessions still to be held and the page of one — and the
 * four blocks of the pages and the dashboards; A10c the exams in the calendar, the list and the form of the staff, and the exams
 * still to come beside the sessions; A11b the heads of a FIR, who read and assign the trainings of their FIR on the same screens,
 * as the server answers them.
 */
export const trainingManifest: ModuleManifest = {
  key: 'training',
  blocks: [upcomingSessionsBlock, myTrainingBlock, trainerQueueBlock, approvalQueueBlock],
  routes: [
    // The public side (A10b): the sessions still to be held with «Request training», and one session by its address — the one
    // every entry of the calendar points at. Under `_public`, with the header and the footer of the site; the people in a session
    // only for a signed in reader, as the server answers.
    {
      area: 'public',
      path: '/training',
      component: TrainingPublicPage,
    },
    {
      area: 'public',
      path: '/training/sessions/$id',
      component: PublicSessionPage,
    },
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
    // The staff's side (A7): every training, open and closed, to whoever holds `Training.View` (R.1) — a head of a FIR the ones of
    // their FIR (A11b), which the server narrows the list to —; what they may do on one is the server's answer on its page.
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
    // The exams in the calendar (A10c): read by whoever does training; entering one asks more, and which ones the reader may
    // change is the server's answer on each row.
    {
      area: 'staff',
      path: '/staff/training/exams',
      permission: TRAINING_VIEW,
      validateSearch: examsSearchSchema,
      component: ExamsPage,
    },
    {
      area: 'staff',
      path: '/staff/training/exams/$id',
      permission: TRAINING_VIEW,
      component: ExamForm,
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
