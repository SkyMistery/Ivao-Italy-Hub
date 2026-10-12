import { Badge } from '@ivao/atmosphere-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useMoment } from '../../../shared/i18n/useMoment';
import { LiveStatusStrip } from '../../../shared/ui';
import type { PublicEventDto } from '../api';

import { bookingStage, countdown, onTheEventsDay } from './bookingTimes';
import { headMoment } from './cards';

/**
 * When the pilots of an event book (design M4 §3.3, §7.1, E6b; asked by dalberone after the bench), as a badge in the line under the
 * title (note 2026-10-11-la-testa-della-pagina-di-un-evento): until they open, the moment they do, in UTC and where the division lives,
 * and how long is left, to the second; then «open» — each slot until its own off block, which the list of the slots says —; «closed»
 * once the event is cancelled or over. Nothing for an event that takes no booking.
 *
 * `onOpen` is told once, when the count reaches nothing: the page reads the event again, and the slots are offered.
 */
export function BookingOpening({
  event,
  timezone,
  onOpen,
}: {
  event: Pick<PublicEventDto, 'bookingOpensAtUtc' | 'cancelledAt' | 'endsAtUtc'>;
  timezone: string | undefined;
  onOpen: () => void;
}) {
  const { t } = useTranslation();
  const moment = useMoment();
  const opensAt = event.bookingOpensAtUtc;
  const opensMs = opensAt === null ? Number.NaN : Date.parse(opensAt);
  const [nowMs, setNowMs] = useState(() => Date.now());
  const stage = bookingStage(event, nowMs);
  const counting = stage === 'opens';

  // A second at a time while the count runs, and nothing once it has run out: only this badge is drawn again.
  useEffect(() => {
    if (!counting) {
      return undefined;
    }

    const timer = window.setInterval(() => {
      const now = Date.now();
      setNowMs(now);
      if (now >= opensMs) {
        window.clearInterval(timer);
        onOpen();
      }
    }, 1000);

    return () => window.clearInterval(timer);
  }, [counting, opensMs, onOpen]);

  if (stage === 'none' || opensAt === null) {
    return null;
  }

  if (stage !== 'opens') {
    return (
      <Badge
        variant="flat"
        color={stage === 'open' ? 'blue' : 'gray'}
        text={t(`events:public.booking.${stage}`)}
      />
    );
  }

  const at = headMoment(opensAt, moment, timezone);
  const left = countdown(nowMs, opensMs);

  return (
    <>
      <Badge
        variant="flat"
        color="gray"
        text={t('events:public.booking.opensOn', {
          at: [
            t('calendar.utc', { at: at.utc }),
            at.local === null ? '' : t('calendar.local', { at: at.local }),
          ]
            .filter((part) => part !== '')
            .join(' '),
        })}
      />
      {/* A timer is not announced every second: `role="timer"` is read when somebody asks for it, not as it ticks. */}
      {left === null ? null : (
        <span role="timer" className="font-semibold whitespace-nowrap tabular-nums">
          {left.days > 0
            ? t('events:public.booking.countdownDays', { days: left.days, time: left.time })
            : t('events:public.booking.countdown', { time: left.time })}
        </span>
      )}
    </>
  );
}

/**
 * Who is online at the airports of an event, on its day (design M4 §7.1; note 2026-10-06-chi-e-online-sugli-scali-di-un-evento, E4b):
 * the core's strip asked for the event's airports. «Its day» is read where the division lives: from the day it starts to the day it
 * ends, midnight to midnight. Not for an event of the whole division, which has no airports of its own — the strip of the division
 * is already at the top of the site — and not for a cancelled one. Inside the page, under its title: the slot of the layout above
 * belongs to the division's strip.
 */
export function EventDayStrip({
  event,
  timezone,
}: {
  event: Pick<PublicEventDto, 'startsAtUtc' | 'endsAtUtc' | 'wholeDivision' | 'cancelledAt' | 'airports'>;
  timezone: string | undefined;
}) {
  const { t } = useTranslation();
  const [now, setNow] = useState(() => new Date());

  // A page left open across midnight shows or hides it at the next minute.
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 60_000);
    return () => window.clearInterval(timer);
  }, []);

  if (
    timezone === undefined ||
    event.wholeDivision ||
    event.cancelledAt !== null ||
    event.airports.length === 0 ||
    !onTheEventsDay(event.startsAtUtc, event.endsAtUtc, now, timezone)
  ) {
    return null;
  }

  return (
    <section
      aria-label={t('events:public.onlineAtTheAirports')}
      className="overflow-hidden rounded-lg border"
    >
      <LiveStatusStrip airports={event.airports.map((airport) => airport.icao)} />
    </section>
  );
}
