import { CardRoot, H4 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Award } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery } from '../../../features/me/queries';
import { mediaFileUrl } from '../../../shared/api/mediaUrl';
import { useMoment } from '../../../shared/i18n/useMoment';
import { RatingBadge } from '../../../shared/ui';
import { myToursQuery } from '../api';

import { ProgressLine } from './ProgressLine';
import { TourBadges } from './TourFace';

/**
 * The cards of the tours (design 11 §3, phase 6): the cover, the state, the kind, the title, the summary on two lines, the
 * period; under them a line of numbers — legs and miles, the minimum rating, the award, the subtours of a container. Written
 * once and drawn by `/tours`, by the subtours of a container's page and by the block `flightops.tourCards`, because they are
 * the same cards; their state and kind are the head's own pieces (`TourBadges`, principle 3), and their numbers the
 * server's (principle 5).
 *
 * A card is the same for a visitor and for a pilot, and so is what the server sends (T10). The progress bar and the next
 * leg of a tour the pilot started (design M2 §8.1) are drawn on top from the pilot's own answer, `/api/flightops/my-tours`,
 * asked only when somebody is signed in (T15b, note 2026-09-24-le-pagine-delle-persone §3.1) — wherever the cards are drawn.
 */

/** One card, with its translated strings already read: the callers read them from different shapes. */
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
  readonly minPilotRatingName: string | null;
  /** The award the tour gives, by name; none when it gives none. */
  readonly award: string | null;
  /** How many subtours a container has; nought on any other tour. */
  readonly subtours: number;
}

export function TourCards({ tours }: { tours: readonly TourCard[] }) {
  const { t } = useTranslation();
  const moment = useMoment();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const signedIn = bootstrap?.user !== null && bootstrap?.user !== undefined;
  const mine = useQuery({ ...myToursQuery, enabled: signedIn });
  const started = new Map((mine.data?.tours ?? []).map((row) => [row.tourId, row]));

  return (
    <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
      {tours.map((tour) => {
        const progress = started.get(tour.id);

        return (
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
                <div className="flex flex-1 flex-col gap-2 p-5">
                  <span className="flex flex-wrap items-center gap-2">
                    <TourBadges state={tour.state} kind={tour.kind} />
                  </span>
                  <H4>{tour.title}</H4>
                  {tour.summary === '' ? null : (
                    <p className="text-muted-foreground line-clamp-2">{tour.summary}</p>
                  )}
                  <p className="text-muted-foreground text-sm tabular-nums">
                    {t('flightops:public.period', {
                      from: moment(tour.releaseAt, { time: false }),
                      to: moment(tour.closeAt, { time: false }),
                    })}
                  </p>
                  <div className="mt-auto flex flex-wrap items-center gap-x-3 gap-y-1 pt-1 text-sm">
                    {tour.legs === 0 ? null : (
                      <span className="tabular-nums">
                        {/* ⚠️ Not the editor's `legs.totals`: on a card these are the legs the tour is made of,
                          said to a visitor, and nobody has flown them. Seen on the bench at 1500 px. The
                          editor's said "flown" in English too, until the gallery showed it (T20c). */}
                        {t('flightops:public.legsAndDistance', {
                          count: tour.legs,
                          distance: Math.round(tour.totalNm),
                        })}
                      </span>
                    )}
                    {tour.subtours === 0 ? null : (
                      <span className="tabular-nums">
                        {t('flightops:public.subtourCount', { count: tour.subtours })}
                      </span>
                    )}
                    {tour.minPilotRatingName === null ? null : (
                      <RatingBadge kind="Pilot" shortName={tour.minPilotRatingName} />
                    )}
                    {tour.award === null ? null : (
                      <span className="text-muted-foreground inline-flex items-center gap-1">
                        <Award aria-hidden className="size-4" />
                        <span className="sr-only">{t('flightops:public.figures.award')}</span>
                        {tour.award}
                      </span>
                    )}
                  </div>
                  {progress === undefined ? null : <ProgressLine tour={progress} />}
                </div>
              </CardRoot>
            </RouterAnchor>
          </article>
        );
      })}
    </div>
  );
}
