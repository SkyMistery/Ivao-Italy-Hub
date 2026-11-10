import { useTranslation } from 'react-i18next';

import type { LocalizedString } from '../../../shared/api/bootstrap';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import type { BlockComponentProps } from '../../../shared/modules';
import { TourCards, type TourCard } from '../screens/TourCards';

/**
 * `flightops.tourCards` (design M2 §8.2): the tours the public sees, as the cards of `/tours`, on a page or on a
 * dashboard. Always live — a list of tours captured the day a page was published would keep a tour that closed months
 * ago, and miss the one that opened this morning.
 *
 * The cards themselves are the ones `/tours` draws, from the same component: a block that drew its own would be the
 * same screen written twice. Since phase 6 they carry the line of numbers of design 11 §3, and the block follows.
 */

/** What `TourCardsProvider` answers with. */
export interface TourCardsData {
  items?: (Omit<TourCard, 'title' | 'summary' | 'award'> & {
    title: LocalizedString | null;
    summary: LocalizedString | null;
    award: { id: number; name: LocalizedString | null } | null;
  })[];
}

export function TourCardsBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const items = (data as TourCardsData | null | undefined)?.items;

  if (items === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (items.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('flightops:public.none')}</p>;
  }

  return (
    <TourCards
      tours={items.map((item) => ({
        ...item,
        title: item.title === null ? '' : read(item.title),
        summary: item.summary === null ? '' : read(item.summary),
        award: item.award === null ? null : read(item.award.name),
      }))}
    />
  );
}
