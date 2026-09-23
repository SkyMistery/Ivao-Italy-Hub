import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import type { LocalizedString } from '../../../shared/api/bootstrap';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import type { BlockComponentProps } from '../../../shared/modules';

/**
 * `flightops.reviewQueue` (design M2 §8.2): the reports waiting on the tours whoever is looking may validate, one line per
 * tour — how many, since when the oldest waits — each a link to the queue of that tour. The daily digest on a dashboard
 * (Carmine, 23 September 2026, T13b). Always live and with no property; its other half is `ReviewQueueProvider`.
 */

/** What `ReviewQueueProvider` answers with. */
export interface ReviewQueueData {
  items?: {
    tourId: number;
    title: LocalizedString | null;
    count: number;
    oldest: string;
  }[];
}

export function ReviewQueueBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const items = (data as ReviewQueueData | null | undefined)?.items;

  if (items === undefined || items.length === 0) {
    return (
      <p className="text-muted-foreground text-sm">
        {items === undefined ? t('common.loading') : t('flightops:blocks.reviewQueue.empty')}
      </p>
    );
  }

  return (
    <ul className="flex flex-col divide-y">
      {items.map((item) => (
        <li key={item.tourId} className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 py-3">
          <RouterAnchor href={`/staff/tours/review?tour=${item.tourId}`} className="font-semibold underline">
            {item.title === null ? `#${item.tourId}` : read(item.title)}
          </RouterAnchor>
          <span className="text-muted-foreground text-sm">
            {t('flightops:blocks.reviewQueue.waiting', { count: item.count, since: moment(item.oldest) })}
          </span>
        </li>
      ))}
    </ul>
  );
}
