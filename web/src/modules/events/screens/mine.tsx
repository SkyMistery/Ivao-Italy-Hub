import { Badge, Button, H1, H2, H3, Lead, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Repeat } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { PageMetadata } from '../../../shared/seo/PageMetadata';
import { ConfirmDialog, EmptyState, Notice } from '../../../shared/ui';
import { myBookingsQuery, useWithdrawBooking, type MyBookingDto } from '../api';

import { STATE_COLOURS, eventHref } from './cards';
import { bookingsByEvent, splitBookings } from './myBookings';

/**
 * `/events/mine` (design M4 §7.1, E6b), for a signed in member: their bookings, the flights still to fly first — by their off block —,
 * then the past ones, the latest first; each under its event, with the aircraft they chose, and «withdraw» until its off block (§3.6).
 * One answer, `GET /api/events/mine/bookings`, the one the block `events.myEvents` reads too. The shifts of the ATC, the record of the
 * flights not flown and the reports of support join it with their phases (M4b).
 */
export function MyBookingsPage() {
  const { t, i18n } = useTranslation();
  const mine = useQuery(myBookingsQuery());
  // The bookings to come and the past ones are told apart from when the page was drawn, and stay so while it is read.
  const [drawnAt] = useState(() => Date.now());

  if (mine.isPending) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-3xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (mine.data === undefined) {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-10">
        <Notice tone="error" title={describeProblem(mine.error, t, i18n.language) ?? t('errors.unknown')} />
      </div>
    );
  }

  const { upcoming, past } = splitBookings(mine.data, drawnAt);

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-4 py-10">
      {/* The tab says what the heading says (#224, E4c), as /events does. */}
      <PageMetadata title={t('events:mine.title')} description={t('events:mine.lead')} />

      <header className="flex flex-col gap-2">
        <H1>{t('events:mine.title')}</H1>
        <Lead>{t('events:mine.lead')}</Lead>
      </header>

      {mine.data.length === 0 ? (
        <EmptyState
          title={t('events:mine.none')}
          description={t('events:mine.noneDescription')}
          action={
            <Button asChild>
              <RouterAnchor href="/events">{t('events:mine.toEvents')}</RouterAnchor>
            </Button>
          }
        />
      ) : (
        <>
          <section className="flex flex-col gap-4" aria-label={t('events:mine.upcoming')}>
            <H2>{t('events:mine.upcoming')}</H2>
            {upcoming.length === 0 ? (
              <p className="text-muted-foreground text-sm">
                {t('events:mine.noneUpcoming')}{' '}
                <RouterAnchor href="/events" className="underline">
                  {t('events:mine.toEvents')}
                </RouterAnchor>
              </p>
            ) : (
              <BookingGroups bookings={upcoming} />
            )}
          </section>

          {past.length === 0 ? null : (
            <section className="flex flex-col gap-4" aria-label={t('events:mine.past')}>
              <H2>{t('events:mine.past')}</H2>
              <BookingGroups bookings={past} />
            </section>
          )}
        </>
      )}
    </article>
  );
}

/** The bookings under their events, each event once, in the order its first booking comes. */
function BookingGroups({ bookings }: { bookings: readonly MyBookingDto[] }) {
  const { t } = useTranslation();
  const read = useLocalized();

  return (
    <div className="flex flex-col gap-6">
      {bookingsByEvent(bookings).map((group) => (
        <section
          key={group.eventId}
          className="bg-card text-card-foreground border-border flex flex-col gap-3 rounded-lg border p-4"
          aria-label={read(group.title) || group.slug}
        >
          <div className="flex flex-wrap items-center gap-2">
            <H3 className="text-lg">
              <RouterAnchor href={eventHref(group.slug)} className="underline">
                {read(group.title) || group.slug}
              </RouterAnchor>
            </H3>
            <Badge
              variant="flat"
              color={STATE_COLOURS[group.state] ?? 'gray'}
              text={t(`events:events.options.state.${group.state}`)}
            />
          </div>
          <ul className="flex flex-col divide-y">
            {group.bookings.map((booking) => (
              <BookingItem key={booking.id} booking={booking} />
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

/**
 * One booking: the flight — callsign and number, the aircraft chosen, from and to with their times in UTC, the stand, its leg of a
 * rotation — and «withdraw» while its off block is to come. Withdrawing is asked once more: the slot goes back to everybody.
 */
function BookingItem({ booking }: { booking: MyBookingDto }) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  const withdraw = useWithdrawBooking();
  const refusal = describeProblem(withdraw.error, t, i18n.language);

  return (
    <li className="flex flex-col gap-2 py-3 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex min-w-0 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-mono font-semibold">{booking.callsign}</span>
          {booking.flightNumber === null ? null : <Subtle className="text-sm">{booking.flightNumber}</Subtle>}
          <Badge variant="flat" color="blue" text={booking.aircraftIcao} />
          {booking.rotation === null ? null : (
            <span className="text-muted-foreground inline-flex items-center gap-1 text-sm">
              <Repeat aria-hidden className="size-4" />
              {booking.leg === null
                ? t('events:mine.rotation', { rotation: booking.rotation })
                : t('events:mine.rotationLeg', { rotation: booking.rotation, leg: booking.leg })}
            </span>
          )}
        </div>
        <span className="text-sm tabular-nums">
          <span className="font-mono">{booking.departureIcao}</span>{' '}
          {t('calendar.utc', { at: moment(booking.offBlockUtc) })} →{' '}
          <span className="font-mono">{booking.arrivalIcao}</span>{' '}
          {t('calendar.utc', { at: moment(booking.onBlockUtc) })}
        </span>
        {booking.stand === null ? null : (
          <Subtle className="text-sm">{t('events:mine.stand', { stand: booking.stand })}</Subtle>
        )}
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
      </div>

      {booking.withdrawable ? (
        <div className="flex shrink-0 items-center">
          <ConfirmDialog
            triggerText={t('events:mine.withdraw')}
            triggerVariant="secondary"
            title={t('events:mine.withdrawTitle', { callsign: booking.callsign })}
            description={t('events:mine.withdrawDescription')}
            confirmText={t('events:mine.withdraw')}
            confirmVariant="destructive"
            disabled={withdraw.isPending}
            onConfirm={() => withdraw.mutate(booking.id)}
          />
        </div>
      ) : null}
    </li>
  );
}
