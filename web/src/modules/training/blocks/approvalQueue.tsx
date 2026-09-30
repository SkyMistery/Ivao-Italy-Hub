import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { useMoment } from '../../../shared/i18n/useMoment';
import type { BlockComponentProps } from '../../../shared/modules';
import type { StaffTrainingRowDto } from '../api';

import { QueuePart, QueueRow } from './queue';
import { queueHref } from './reading';

/**
 * `training.approvalQueue` (design M3 §4.3): the requests the reader may accept or refuse and the trainings they may assign, the
 * oldest request first — how many, a link to the list of each, and the oldest of them, each a link to its page. What waits for whom
 * is the one handler's answer on each row, the server's (never a training of the reader's own; for a head of a FIR, only the ones of
 * their FIR to assign, A11b). Always live and with no property; its other half is `ApprovalQueueProvider`.
 */

/** One queue: how many wait for the reader, and the oldest of them. */
interface QueueData {
  count: number;
  oldest: StaffTrainingRowDto[];
}

/** What `ApprovalQueueProvider` answers with: the two queues, or `signedIn: false` for a visitor. */
export interface ApprovalQueueData {
  signedIn?: boolean;
  toApprove?: QueueData;
  toAssign?: QueueData;
}

export function ApprovalQueueBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const moment = useMoment();
  const queue = data as ApprovalQueueData | null | undefined;

  if (queue?.signedIn === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  const { toApprove, toAssign } = queue;
  if (!queue.signedIn || toApprove === undefined || toAssign === undefined) {
    return <p className="text-muted-foreground text-sm">{t('training:blocks.signIn')}</p>;
  }

  if (toApprove.count === 0 && toAssign.count === 0) {
    return <p className="text-muted-foreground text-sm">{t('training:blocks.approvalQueue.empty')}</p>;
  }

  const parts = [
    { queue: 'toApprove', data: toApprove },
    { queue: 'toAssign', data: toAssign },
  ] as const;

  return (
    <div className="flex flex-col gap-4">
      {parts.map((part) =>
        part.data.count === 0 ? null : (
          <QueuePart
            key={part.queue}
            title={
              <RouterAnchor href={queueHref(part.queue)} className="underline">
                {t(`training:blocks.approvalQueue.${part.queue}`, { count: part.data.count })}
              </RouterAnchor>
            }
          >
            {part.data.oldest.map((row) => (
              <QueueRow key={row.id} row={row}>
                {t('training:mine.requestedAt', { date: moment(row.createdAt, { time: false }) })}
              </QueueRow>
            ))}
          </QueuePart>
        ),
      )}
    </div>
  );
}
