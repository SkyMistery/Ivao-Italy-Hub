import type { TFunction } from 'i18next';

import { personName } from '../../../shared/ui';
import type { TrainingHistoryEntryDto, TrainingMemberDto } from '../api';

import type { FormatMoment } from './dates';

/**
 * What a line of the history of a training says (A13b; note `2026-09-30-lo-storico-di-un-training`): the sentence of its step, with
 * who took it — the hub, when nobody did —, the trainers and the dates it names, and the reason of a refusal or of a closing on a
 * line of its own. What happened is the server's reading of the core's audit log (`TrainingHistory`): this only says it.
 *
 * A date inside a sentence is said in UTC, as the other sentences of the page say one; the moment of the line itself is drawn in
 * UTC and where the division lives, as every moment on its own is. A person is written the way every page writes one
 * (`personName`), so a person whose data was erased is a deleted person.
 */
export interface HistorySays {
  readonly text: string;
  readonly reason: string | null;
}

export function historySays(entry: TrainingHistoryEntryDto, t: TFunction, moment: FormatMoment): HistorySays {
  const who = (person: TrainingMemberDto | null) =>
    person === null ? t('training:staff.history.hub') : personName(person, t);
  const when = (value: string | null) =>
    value === null ? '' : t('training:time.utc', { when: moment(value) });
  const name = who(entry.by);
  const said = (text: string, reason: string | null = null): HistorySays => ({
    text,
    reason: reason === null ? null : t('training:staff.decision.reason', { reason }),
  });

  switch (entry.event) {
    case 'Requested':
      return said(t('training:staff.history.requested', { name }));
    case 'RejectedForTheory':
      return said(t('training:staff.history.rejectedForTheory'));
    case 'Accepted':
      return said(t('training:staff.history.accepted', { name }));
    case 'Rejected':
      return said(t('training:staff.history.rejected', { name }), entry.reason);
    case 'Assigned':
      return said(t('training:staff.history.assigned', { name, trainer: who(entry.trainer) }));
    case 'TrainerChanged':
      return said(
        t('training:staff.history.trainerChanged', {
          name,
          from: who(entry.previousTrainer),
          to: who(entry.trainer),
        }),
      );
    case 'DatesChanged':
      return said(t('training:staff.history.datesChanged', { name }));
    case 'DateChosen':
      return said(t('training:staff.history.dateChosen', { name, date: when(entry.date) }));
    case 'DateSet':
      return said(t('training:staff.history.dateSet', { name, date: when(entry.date) }));
    case 'DateMoved':
      return said(
        t('training:staff.history.dateMoved', {
          name,
          from: when(entry.previousDate),
          to: when(entry.date),
        }),
      );
    case 'Rescheduled':
      return said(t('training:staff.history.rescheduled', { name, date: when(entry.date) }));
    case 'NoShow':
      return said(t('training:staff.history.noShow', { name, date: when(entry.date) }));
    case 'Completed':
      return said(t('training:staff.history.completed', { name }));
    case 'Closed':
      // Nobody closed it: the hub, because the trainee chose no date in time (§2.5), which has no reason of the staff's.
      return entry.by === null
        ? said(t('training:staff.history.closedByHub'))
        : said(t('training:staff.history.closed', { name }), entry.reason);
    case 'Cancelled':
      return said(t('training:staff.history.cancelled', { name }));
    case 'Changed':
      return said(t('training:staff.history.changed', { name }));
    case 'Erased':
      return said(t('training:staff.history.erased', { name }));
  }
}
