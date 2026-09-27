import { Badge } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { useMoment } from '../../../shared/i18n/useMoment';
import type { BlockComponentProps } from '../../../shared/modules';
import type { StaffTrainingRowDto } from '../api';

import { QueuePart, QueueRow } from './queue';

/**
 * `training.trainerQueue` (design M3 §4.3, §2.5): the reader's trainings to move, as their trainer — first, in evidence, the ones whose
 * trainee has let the dates wait longer than the division gives (`responseReminderDays`), with the days; then the dates to propose and
 * the reports to write —, each a link to the page of the training, where the step is taken. Which is which is the server's answer
 * (`TrainerQueue`), for whoever is looking. Always live and with no property; its other half is `TrainerQueueProvider`.
 */

/** What `TrainerQueueProvider` answers with: the three parts, or `signedIn: false` for a visitor. */
export interface TrainerQueueData {
  signedIn?: boolean;
  toPropose?: StaffTrainingRowDto[];
  waiting?: { training: StaffTrainingRowDto; days: number }[];
  toReport?: StaffTrainingRowDto[];
}

export function TrainerQueueBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const moment = useMoment();
  const queue = data as TrainerQueueData | null | undefined;

  if (queue?.signedIn === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  const { toPropose, waiting, toReport } = queue;
  if (!queue.signedIn || toPropose === undefined || waiting === undefined || toReport === undefined) {
    return <p className="text-muted-foreground text-sm">{t('training:blocks.signIn')}</p>;
  }

  if (toPropose.length === 0 && waiting.length === 0 && toReport.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('training:blocks.trainerQueue.empty')}</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      {waiting.length === 0 ? null : (
        <QueuePart title={t('training:blocks.trainerQueue.waiting')}>
          {waiting.map((entry) => (
            <QueueRow key={entry.training.id} row={entry.training}>
              <Badge
                variant="flat"
                color="orange"
                text={t('training:blocks.trainerQueue.waitingDays', { count: entry.days })}
              />
            </QueueRow>
          ))}
        </QueuePart>
      )}

      {toPropose.length === 0 ? null : (
        <QueuePart title={t('training:blocks.trainerQueue.toPropose')}>
          {toPropose.map((row) => (
            <QueueRow key={row.id} row={row}>
              {t('training:mine.requestedAt', { date: moment(row.createdAt, { time: false }) })}
            </QueueRow>
          ))}
        </QueuePart>
      )}

      {toReport.length === 0 ? null : (
        <QueuePart title={t('training:blocks.trainerQueue.toReport')}>
          {toReport.map((row) => (
            <QueueRow key={row.id} row={row}>
              {row.scheduledStartUtc === null
                ? null
                : t('training:time.utc', { when: moment(row.scheduledStartUtc) })}
            </QueueRow>
          ))}
        </QueuePart>
      )}
    </div>
  );
}
