import { Badge, H1, Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { ExternalLink } from 'lucide-react';
import { Fragment, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { bootstrapQuery } from '../../../features/me/queries';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { calendarKindColour } from '../../../shared/ui';
import type { PublicEventDto } from '../api';

import { BookingOpening } from './BookingOpening';
import { headSpan } from './cards';

/**
 * The head of the page of an event, as dalberone drew it after the bench (note 2026-10-11-la-testa-della-pagina-di-un-evento): the
 * kind, the title and the summary; then two short lines in place of the facts in rows — when it runs, in UTC and where the division
 * lives; its bookings as a badge (`BookingOpening`), its airports by their codes, the name on hover, and who organises it when it is
 * not the division, with their page. On a phone each line wraps. The slots come right after it: what a pilot came for.
 */
export function EventHead({
  event,
  timezone,
  onOpen,
}: {
  event: PublicEventDto;
  timezone: string | undefined;
  onOpen: () => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const kinds = bootstrap?.calendarKinds ?? [];
  const summary = read(event.summary);
  const when = headSpan(event.startsAtUtc, event.endsAtUtc, moment, timezone);

  // The facts of the second line, in their order, each a piece the dots go between.
  const facts: { key: string; fact: ReactNode }[] = [];
  if (event.bookingOpensAtUtc !== null) {
    facts.push({
      key: 'bookings',
      fact: (
        <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
          <BookingOpening event={event} timezone={timezone} onOpen={onOpen} />
        </span>
      ),
    });
  }

  if (event.wholeDivision) {
    facts.push({ key: 'airports', fact: <span>{t('events:public.wholeDivision')}</span> });
  } else {
    for (const airport of event.airports) {
      facts.push({
        key: airport.icao,
        fact:
          airport.name === null || airport.name === '' ? (
            <span className="font-mono">{airport.icao}</span>
          ) : (
            <abbr title={airport.name} className="font-mono">
              {airport.icao}
            </abbr>
          ),
      });
    }
  }

  // Who organises it, only when it is not the division — on the division's own site that goes without saying —, and their page.
  if (event.organizer !== 'Division') {
    facts.push({ key: 'organizer', fact: <span>{t(`events:public.organizedBy.${event.organizer}`)}</span> });
    if (event.externalUrl !== null) {
      facts.push({
        key: 'organizerPage',
        fact: (
          <a
            href={event.externalUrl}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1 underline"
          >
            <ExternalLink aria-hidden className="size-3.5" />
            {t('events:public.organizerPage')}
          </a>
        ),
      });
    }
  }

  return (
    <header className="flex flex-col gap-3">
      <div>
        <Badge
          variant="flat"
          color={calendarKindColour(event.kind, kinds)}
          text={read(kinds.find((word) => word.key === event.kind)?.label) || event.kind}
        />
      </div>
      <H1>{read(event.title)}</H1>
      {summary === '' ? null : <Lead>{summary}</Lead>}

      <div className="flex flex-col gap-2">
        {/* On a phone the line wraps between UTC and the division's time, never inside the division's time. */}
        <p className="flex flex-wrap gap-x-1 tabular-nums">
          <span className="font-semibold">{t('calendar.utc', { at: when.utc })}</span>
          {when.local === null ? null : (
            <span className="text-muted-foreground whitespace-nowrap">
              {t('calendar.local', { at: when.local })}
            </span>
          )}
        </p>

        {facts.length === 0 ? null : (
          <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
            {facts.map(({ key, fact }, index) =>
              index === 0 ? (
                <Fragment key={key}>{fact}</Fragment>
              ) : (
                // A dot before each fact after the first, which only the eye needs, kept with its fact: a line that wraps never
                // ends on one.
                <span key={key} className="inline-flex items-center gap-x-2">
                  <span aria-hidden className="text-muted-foreground">
                    ·
                  </span>
                  {fact}
                </span>
              ),
            )}
          </p>
        )}
      </div>
    </header>
  );
}
