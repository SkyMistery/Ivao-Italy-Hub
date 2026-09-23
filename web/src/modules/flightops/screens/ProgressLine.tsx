import { Progress } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import type { StartedTourDto } from '../api';

import { measureWords, percentOf } from './progress';

/**
 * A pilot's progress in one tour, in the three places that show it (T15b): the bar, the measure in the unit of the tour's
 * kind, and the next leg while there is one. Written once for the cards, the block `flightops.myTours` and the pilot's page.
 */
export function ProgressLine({
  tour,
}: {
  tour: Pick<StartedTourDto, 'done' | 'target' | 'unit' | 'completedAt'> & {
    next?: StartedTourDto['next'];
  };
}) {
  const { t } = useTranslation();
  const completed = tour.completedAt !== null;
  const words = measureWords(tour);
  const said = completed ? t('flightops:progress.completed') : t(words.key, words.values);

  return (
    <div className="flex flex-col gap-1">
      <Progress value={percentOf(tour, completed)} aria-label={said} className="h-2" />
      <p className="text-muted-foreground text-sm tabular-nums">
        {said}
        {!completed && tour.next !== undefined && tour.next !== null
          ? ` · ${t('flightops:progress.next', {
              number: tour.next.number,
              from: tour.next.departureIcao,
              to: tour.next.arrivalIcao,
            })}`
          : null}
      </p>
    </div>
  );
}
