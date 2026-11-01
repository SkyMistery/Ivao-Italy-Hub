import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import type { BlockComponentProps } from '../../../shared/modules';
import type { EventListData } from '../screens/cards';
import { EventCards } from '../screens/EventCards';

/**
 * `events.eventList` (design M4 §7.3, E4): the events to come and those in progress, the soonest first, as the cards of `/events` —
 * on the home page, on a page of the site, on a dashboard —, and the way to all of them. Always live: its other half is
 * `EventListProvider`, which `/events` reads too.
 *
 * The cards are the ones `/events` draws, from the same component: a block that drew its own would be the same screen written twice.
 */
export function EventListBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const answer = data as EventListData | null | undefined;
  const items = answer?.items;

  // `null` is a request that failed, `undefined` one on its way: the two never read the same (the core's `NoRows`).
  if (answer === null) {
    return <p className="text-muted-foreground text-sm">{t('events:blocks.unavailable')}</p>;
  }

  if (items === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      {items.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('events:public.none')}</p>
      ) : (
        <EventCards cards={items} />
      )}
      <RouterAnchor href="/events" className="text-sm underline">
        {t('events:blocks.eventList.all')}
      </RouterAnchor>
    </div>
  );
}
