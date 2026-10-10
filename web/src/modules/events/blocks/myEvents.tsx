import { H4, Subtle } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import type { BlockComponentProps } from '../../../shared/modules';
import type { MyBookingDto } from '../api';
import { eventHref } from '../screens/cards';
import { MY_EVENTS_PAGE, bookingsByEvent } from '../screens/myBookings';

/**
 * `events.myEvents` (design M4 §7.3, E6b): the reader's bookings still to fly, on `/me`, under their events — each flight with the
 * aircraft chosen and when it leaves, in UTC —, and the way to all of them on `/events/mine`, where they are withdrawn. «No booking —
 * go to the events» when there is none. Always live and with no property; its other half is `MyEventsProvider`, the same answer
 * `/events/mine` reads. The shifts of the ATC join it with their phase (E12).
 */

/** What `MyEventsProvider` answers with: the bookings still to fly, or `signedIn: false` for a visitor. */
export interface MyEventsData {
  signedIn?: boolean;
  bookings?: MyBookingDto[];
}

export function MyEventsBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const mine = data as MyEventsData | null | undefined;

  if (mine?.signedIn === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (!mine.signedIn || mine.bookings === undefined) {
    return <p className="text-muted-foreground text-sm">{t('events:blocks.myEvents.signIn')}</p>;
  }

  if (mine.bookings.length === 0) {
    return (
      <p className="text-muted-foreground text-sm">
        {t('events:blocks.myEvents.none')}{' '}
        <RouterAnchor href="/events" className="underline">
          {t('events:blocks.myEvents.toEvents')}
        </RouterAnchor>
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <ul className="flex flex-col divide-y">
        {bookingsByEvent(mine.bookings).map((group) => (
          <li key={group.eventId} className="flex flex-col gap-2 py-3">
            <H4 className="text-base">
              <RouterAnchor href={eventHref(group.slug)} className="underline">
                {read(group.title) || group.slug}
              </RouterAnchor>
            </H4>
            <ul className="flex flex-col gap-1 text-sm">
              {group.bookings.map((booking) => (
                <li key={booking.id} className="flex flex-wrap items-baseline gap-x-2 tabular-nums">
                  <span className="font-mono font-semibold">{booking.callsign}</span>
                  <span className="font-mono">{booking.aircraftIcao}</span>
                  <span>
                    <span className="font-mono">{booking.departureIcao}</span> →{' '}
                    <span className="font-mono">{booking.arrivalIcao}</span>
                  </span>
                  <Subtle>{t('calendar.utc', { at: moment(booking.offBlockUtc) })}</Subtle>
                </li>
              ))}
            </ul>
          </li>
        ))}
      </ul>
      <RouterAnchor href={MY_EVENTS_PAGE} className="text-sm underline">
        {t('events:blocks.myEvents.all')}
      </RouterAnchor>
    </div>
  );
}
