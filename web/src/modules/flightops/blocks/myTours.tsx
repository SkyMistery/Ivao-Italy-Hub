import { H4 } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import type { BlockComponentProps } from '../../../shared/modules';
import type { MyToursDto } from '../api';
import { ProgressLine } from '../screens/ProgressLine';
import { hoursAndMinutes } from '../screens/progress';

/**
 * `flightops.myTours` (design M2 §8.2; note 2026-09-23-completamento-validatori-piloti-ban §3.4): the reader's own tours on
 * `/me` — what waits for them first (a report to correct, an answer to read), then the tours they started with how far and
 * the next leg, then the summary: legs accepted, time flown, tours completed. No count of errors, and nothing of anybody
 * else. Always live and with no property; its other half is `MyToursProvider`, the same answer `/api/flightops/my-tours` gives.
 */

/** What `MyToursProvider` answers with: the pilot's tours, or `signedIn: false` for a visitor. */
export type MyToursData = Partial<MyToursDto> & { signedIn?: boolean };

export function MyToursBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const mine = data as MyToursData | null | undefined;

  if (mine?.signedIn === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (!mine.signedIn || mine.tours === undefined || mine.summary === undefined) {
    return <p className="text-muted-foreground text-sm">{t('flightops:blocks.myTours.signIn')}</p>;
  }

  const flown = hoursAndMinutes(mine.summary.minutesFlown);

  return (
    <div className="flex flex-col gap-4">
      {(mine.toModify ?? []).length === 0 && (mine.answered ?? []).length === 0 ? null : (
        <ul className="flex flex-col gap-1">
          {(mine.toModify ?? []).map((report) => (
            <li key={`report-${report.pirepId}`}>
              <RouterAnchor
                href={`/tours/${report.slug}/report?report=${report.pirepId}`}
                className="font-semibold underline"
              >
                {t('flightops:blocks.myTours.toModify', {
                  tour: read(report.tourTitle),
                  from: report.departureIcao,
                  to: report.arrivalIcao,
                })}
              </RouterAnchor>
            </li>
          ))}
          {(mine.answered ?? []).map((thread) => (
            <li key={`thread-${thread.id}`}>
              <RouterAnchor href={`/me/contacts/${thread.id}`} className="font-semibold underline">
                {t('flightops:blocks.myTours.answered', { subject: thread.subject })}
              </RouterAnchor>{' '}
              <span className="text-muted-foreground text-sm">
                {moment(thread.updatedAt, { time: false })}
              </span>
            </li>
          ))}
        </ul>
      )}

      {mine.tours.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          {t('flightops:blocks.myTours.none')}{' '}
          <RouterAnchor href="/tours" className="underline">
            {t('flightops:blocks.myTours.toTours')}
          </RouterAnchor>
        </p>
      ) : (
        <ul className="flex flex-col divide-y">
          {mine.tours.map((tour) => (
            <li key={tour.tourId} className="flex flex-col gap-2 py-3">
              <H4 className="text-base">
                <RouterAnchor href={`/tours/${tour.slug}`} className="underline">
                  {read(tour.title)}
                </RouterAnchor>
              </H4>
              <ProgressLine tour={tour} />
            </li>
          ))}
        </ul>
      )}

      <p className="text-muted-foreground text-sm tabular-nums">
        {t('flightops:blocks.myTours.summary', {
          legs: mine.summary.legsAccepted,
          hours: flown.hours,
          minutes: flown.minutes,
          tours: mine.summary.toursCompleted,
        })}
      </p>
    </div>
  );
}
