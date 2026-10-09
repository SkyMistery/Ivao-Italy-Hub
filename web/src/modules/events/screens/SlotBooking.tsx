import { Button, Label, RadioGroupItem, RadioGroupRoot } from '@ivao/atmosphere-react';
import { Check, X } from 'lucide-react';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { loginHref } from '../../../shared/api/client';
import { describeProblem } from '../../../shared/forms';
import { Notice } from '../../../shared/ui';
import {
  useBook,
  useBookRotation,
  type PublicEventDto,
  type PublicEventSlotDto,
  type RotationBookingDto,
} from '../api';

import { bookingsOpen, slotOpen } from './bookingTimes';
import { MY_EVENTS_PAGE, type SlotViewer } from './myBookings';

/**
 * «Book» in the dialog of a slot (design M4 §3.3, E6b; note 2026-10-07-gli-slot-sulla-pagina-dell-evento §6): the pilot chooses the
 * aircraft they fly among the types the slot admits — the main one first, and chosen until they choose another — and books the slot,
 * or the whole rotation of a leg with that aircraft. What the server answers is said here: the booking made, the legs booked and the
 * ones that were not with why, or the refusal.
 *
 * Offered only when it can be accepted: to a signed in member, on a slot nobody took, while the bookings of the event are open and
 * the off block of the slot is to come. Otherwise it says why not — the slot is the reader's own, it is closed, the bookings open
 * later, a visitor signs in first —, and nothing on a slot another pilot took: the page never says by whom.
 */
export function SlotBooking({
  slot,
  event,
  viewer,
  nowMs,
}: {
  slot: PublicEventSlotDto;
  event: Pick<PublicEventDto, 'bookingOpensAtUtc' | 'cancelledAt'>;
  viewer: SlotViewer;
  nowMs: number;
}) {
  const { t, i18n } = useTranslation();
  const choiceId = useId();
  const [aircraft, setAircraft] = useState(slot.aircraftTypes[0] ?? '');
  const book = useBook();
  const rotation = useBookRotation();
  const busy = book.isPending || rotation.isPending;
  const refusal = describeProblem(book.error ?? rotation.error, t, i18n.language);
  const mine = viewer.mine.get(slot.id);

  if (book.isSuccess) {
    return <Booked aircraft={book.data.aircraftIcao} />;
  }

  if (rotation.isSuccess) {
    return <RotationBooked result={rotation.data} />;
  }

  if (mine !== undefined) {
    return <Booked aircraft={mine.aircraftIcao} />;
  }

  if (slot.taken || event.cancelledAt !== null) {
    return null;
  }

  if (!bookingsOpen(event, nowMs)) {
    return <p className="text-muted-foreground text-sm">{t('events:public.booking.notOpen')}</p>;
  }

  if (!slotOpen(slot, nowMs)) {
    return <p className="text-muted-foreground text-sm">{t('events:errors.slotClosed')}</p>;
  }

  if (!viewer.signedIn) {
    return (
      <p className="text-sm">
        <a href={loginHref(location.href)} className="underline">
          {t('events:public.booking.signIn')}
        </a>
      </p>
    );
  }

  return (
    <section className="flex flex-col gap-3" aria-label={t('events:public.booking.panel')}>
      {slot.aircraftTypes.length > 1 ? (
        <RadioGroupRoot
          aria-label={t('events:public.booking.aircraft')}
          value={aircraft}
          onValueChange={setAircraft}
          className="flex flex-wrap gap-x-4 gap-y-2"
        >
          {slot.aircraftTypes.map((type, index) => {
            const id = `${choiceId}-${type}`;

            return (
              <div key={type} className="flex items-center gap-2">
                <RadioGroupItem id={id} value={type} />
                <Label htmlFor={id} className="cursor-pointer font-mono font-normal">
                  {type}
                  {index === 0 ? (
                    <span className="text-muted-foreground font-sans">
                      {' '}
                      ({t('events:public.detail.main')})
                    </span>
                  ) : null}
                </Label>
              </div>
            );
          })}
        </RadioGroupRoot>
      ) : null}

      {refusal === null ? null : <Notice tone="error" title={refusal} />}

      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          disabled={busy || aircraft === ''}
          onClick={() => {
            rotation.reset();
            book.mutate({ slotId: slot.id, aircraftIcao: aircraft });
          }}
        >
          {t('events:public.booking.book', { aircraft })}
        </Button>
        {slot.rotation === null ? null : (
          <Button
            type="button"
            variant="outline"
            disabled={busy || aircraft === ''}
            onClick={() => {
              book.reset();
              rotation.mutate({ slotId: slot.id, aircraftIcao: aircraft });
            }}
          >
            {t('events:public.booking.bookRotation')}
          </Button>
        )}
      </div>
    </section>
  );
}

/** A slot the reader holds — just booked, or already theirs —, with the aircraft they chose, and the way to their bookings. */
function Booked({ aircraft }: { aircraft: string }) {
  const { t } = useTranslation();

  return (
    <Notice
      tone="success"
      title={t('events:public.booking.yours', { aircraft })}
      description={
        <RouterAnchor href={MY_EVENTS_PAGE} className="underline">
          {t('events:public.booking.toMine')}
        </RouterAnchor>
      }
    />
  );
}

/**
 * What «book the whole rotation» did (§3.3): each leg booked, and each leg that was not with why — taken by another pilot, already
 * the reader's, closed, too close to another of theirs, not for that aircraft. Nothing booked at all is said too.
 */
function RotationBooked({ result }: { result: RotationBookingDto }) {
  const { t } = useTranslation();

  return (
    <section className="flex flex-col gap-2" aria-label={t('events:public.booking.rotationResult')}>
      <Notice
        tone={result.booked.length === 0 ? 'warning' : 'success'}
        title={t(
          result.booked.length === 0
            ? 'events:public.booking.rotationNone'
            : 'events:public.booking.rotationDone',
          { booked: result.booked.length, of: result.booked.length + result.notBooked.length },
        )}
      />
      <ul className="flex flex-col gap-1 text-sm">
        {result.booked.map((leg) => (
          <li key={leg.slotId} className="flex items-center gap-2">
            <Check aria-hidden className="size-4 shrink-0 text-green-600" />
            <span className="font-mono">{leg.callsign}</span>
            <span className="sr-only">{t('events:public.booking.legBooked')}</span>
          </li>
        ))}
        {result.notBooked.map((leg) => (
          <li key={leg.slotId} className="flex items-start gap-2">
            <X aria-hidden className="text-muted-foreground mt-0.5 size-4 shrink-0" />
            <span>
              <span className="font-mono">{leg.callsign}</span>: {t(leg.reason)}
            </span>
          </li>
        ))}
      </ul>
      {result.booked.length === 0 ? null : (
        <RouterAnchor href={MY_EVENTS_PAGE} className="text-sm underline">
          {t('events:public.booking.toMine')}
        </RouterAnchor>
      )}
    </section>
  );
}
