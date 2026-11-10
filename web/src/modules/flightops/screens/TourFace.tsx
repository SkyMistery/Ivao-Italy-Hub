import { Badge, H1, Lead } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { mediaFileUrl } from '../../../shared/api/mediaUrl';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { StatTile } from '../../../shared/ui';
import type { PublicTourDto } from '../api';

import { tourFigures } from './tourPage';

/**
 * The face of a tour (design 11 §2, principle 3): what it is, when, and its numbers — the same pieces for the card of the
 * list, the head of its page and, in the next step, the preview of the back office, drawn at their sizes and written once.
 * Every number is the server's (principle 5): the legs, the miles, the time, the rating, the award are what `PublicTours`
 * answers; these only say them.
 */

/** The colour of a tour's state: open as the cards always drew it, the window after the close in orange, the past grey. */
const STATE_COLOURS = {
  Upcoming: 'indigo',
  Open: 'blue',
  Closing: 'orange',
  Closed: 'gray',
} as const;

/** The state and the kind of a tour, as the card and the head say them. */
export function TourBadges({ state, kind }: { state: string; kind: string }) {
  const { t } = useTranslation();

  return (
    <>
      <Badge
        variant="flat"
        color={STATE_COLOURS[state as keyof typeof STATE_COLOURS] ?? 'gray'}
        text={t(`flightops:tours.options.state.${state}`)}
      />
      <span className="text-muted-foreground text-sm">{t(`flightops:tours.options.kind.${kind}`)}</span>
    </>
  );
}

/**
 * The head of the page of a tour (design 11 §4): the banner, the state and the kind, the title, the summary, the period;
 * under them the strip of its numbers, one `StatTile` each. What used to be spread between the header, «What the tour asks»
 * and the totals of the legs, in one place.
 *
 * ⚠️ Not the core's `Hero`, which the design names: `Hero` draws its title as an `H2` in a box and takes no picture, and the
 * head of a page needs the page's one `H1` and the banner. Written with Atmosphere's pieces, as the head of an event is
 * (`EventHead`), so that nothing in the core changes (note 2026-11-10-la-pagina-di-un-tour).
 */
export function TourHead({ tour }: { tour: PublicTourDto }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const summary = read(tour.summary);
  const figures = tourFigures(tour, t, read);

  return (
    <header className="flex flex-col gap-6">
      {tour.bannerMediaId === null ? null : (
        <img
          src={mediaFileUrl(tour.bannerMediaId)}
          alt=""
          className="bg-muted max-h-80 w-full rounded-lg object-cover"
        />
      )}

      <div className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <TourBadges state={tour.state} kind={tour.kind} />
          {tour.parent === null ? null : (
            <RouterAnchor href={`/tours/${tour.parent.slug}`} className="text-sm underline">
              {t('flightops:public.partOf', { title: read(tour.parent.title) })}
            </RouterAnchor>
          )}
        </div>

        <H1>{read(tour.title)}</H1>
        {summary === '' ? null : <Lead>{summary}</Lead>}

        <p className="text-muted-foreground text-sm tabular-nums">
          {t('flightops:public.period', {
            from: moment(tour.releaseAt, { time: false }),
            to: moment(tour.closeAt, { time: false }),
          })}
        </p>
      </div>

      {figures.length === 0 ? null : (
        <div className="grid grid-cols-1 gap-3 min-[420px]:grid-cols-2 md:grid-cols-3">
          {figures.map((figure) => (
            <StatTile
              key={figure.key}
              label={figure.label}
              value={figure.value}
              {...(figure.hint === undefined ? {} : { hint: figure.hint })}
              Icon={figure.Icon}
            />
          ))}
        </div>
      )}
    </header>
  );
}
