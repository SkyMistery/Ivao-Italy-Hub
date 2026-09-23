import { Badge, CardRoot, H4 } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { mediaFileUrl } from '../../../shared/api/mediaUrl';
import { useMoment } from '../../../shared/i18n/useMoment';

/**
 * The cards of the tours (design M2 §8.1): the picture, the title, the summary, what the tour is, how many legs and how
 * far. Written once and drawn twice — by `/tours` and by the block `flightops.tourCards` — because they are the same
 * cards, and a second copy is a second thing to keep in step.
 *
 * No progress bar and no next leg: those belong to whoever is logged in and arrive with the pilot's pages (T15b). A card is
 * the same for a visitor and for a pilot.
 */

/** One card, with its translated strings already read: the two callers read them from different shapes. */
export interface TourCard {
  readonly id: number;
  readonly slug: string;
  readonly kind: string;
  readonly title: string;
  readonly summary: string;
  readonly coverMediaId: number | null;
  readonly state: string;
  readonly releaseAt: string;
  readonly closeAt: string;
  readonly legs: number;
  readonly totalNm: number;
}

export function TourCards({ tours }: { tours: readonly TourCard[] }) {
  const { t } = useTranslation();
  const moment = useMoment();

  return (
    <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
      {tours.map((tour) => (
        <article key={tour.id} className="h-full">
          <RouterAnchor href={`/tours/${tour.slug}`} className="block h-full">
            <CardRoot className="flex h-full flex-col overflow-hidden">
              {tour.coverMediaId === null ? null : (
                <img
                  src={mediaFileUrl(tour.coverMediaId)}
                  alt=""
                  className="bg-muted h-40 w-full object-cover"
                  loading="lazy"
                />
              )}
              <div className="flex flex-col gap-2 p-5">
                <span className="flex flex-wrap items-center gap-2">
                  <Badge
                    variant="flat"
                    color={tour.state === 'Upcoming' ? 'gray' : 'blue'}
                    text={t(`flightops:tours.options.state.${tour.state}`)}
                  />
                  <span className="text-muted-foreground text-sm">
                    {t(`flightops:tours.options.kind.${tour.kind}`)}
                  </span>
                </span>
                <H4>{tour.title}</H4>
                {tour.summary === '' ? null : <p className="text-muted-foreground">{tour.summary}</p>}
                <p className="text-muted-foreground text-sm tabular-nums">
                  {tour.state === 'Upcoming'
                    ? t('flightops:public.opensOn', { date: moment(tour.releaseAt, { time: false }) })
                    : t('flightops:public.closesOn', { date: moment(tour.closeAt, { time: false }) })}
                </p>
                {tour.legs === 0 ? null : (
                  <p className="text-muted-foreground text-sm tabular-nums">
                    {/* ⚠️ Not the editor's `legs.totals`, which says "flown": on a card these are the legs
                        the tour is made of, and nobody has flown them. Seen on the bench at 1500 px. */}
                    {t('flightops:public.legsAndDistance', {
                      count: tour.legs,
                      distance: Math.round(tour.totalNm),
                    })}
                  </p>
                )}
              </div>
            </CardRoot>
          </RouterAnchor>
        </article>
      ))}
    </div>
  );
}
