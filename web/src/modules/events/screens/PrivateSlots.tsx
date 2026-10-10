import { Badge, Dialog, H2, H3, Label, Subtle, Switch } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { PlaneLanding, PlaneTakeoff } from 'lucide-react';
import { Fragment, useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery } from '../../../features/me/queries';
import { loginHref } from '../../../shared/api/client';
import { ApiError } from '../../../shared/api/problem';
import { SchemaForm } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { Notice } from '../../../shared/ui';
import {
  useBookPrivate,
  type PrivateBookingDto,
  type PublicEventAirportDto,
  type PublicEventDto,
  type PublicPrivateSlotDto,
} from '../api';
import {
  privateFlightSchema,
  privatePairSchema,
  type PrivateFlightChoices,
  type PrivateFlightValues,
  type PrivatePairValues,
} from '../schemas';

import { bookingsOpen } from './bookingTimes';
import { airportLabel } from './cards';
import { AirportName } from './EventCards';
import { MY_EVENTS_PAGE, VISITOR, type SlotViewer } from './myBookings';
import {
  bookableIn,
  linkableDepartures,
  privateOneDay,
  privateRequest,
  privateSections,
  type PrivateHour,
} from './privateList';

/**
 * The private slots of an event (design M4 §3.4, §7.1, E7): by the airport of the event they are at — one section each when there is
 * more than one airport —, the departures and the arrivals in two tables, hour by hour of the event, each hour with how many of its
 * slots are free; free or taken, never who took it (plan §9.7), and the reader's own said as theirs, from their own bookings. A row opens
 * its hour, where a pilot books one of its free slots with the flight they fly — an arrival with its linked departure from the same
 * airport, when asked. Everything comes in the page's one read, as for the public slots: no read of its own.
 */
export function PrivateSlots({ event, viewer = VISITOR }: { event: PublicEventDto; viewer?: SlotViewer }) {
  const { t } = useTranslation();
  const moment = useMoment();
  const [opened, setOpened] = useState<{ icao: string; isArrival: boolean; fromUtc: string } | null>(null);
  // The hour that opened the dialog: it has no trigger of its own, so the focus is put back there when it closes (as for a slot, E5).
  const openedBy = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (opened === null && openedBy.current !== null) {
      openedBy.current.focus();
      openedBy.current = null;
    }
  }, [opened]);

  const sections = privateSections(
    event.privateSlots,
    event.airports.map((airport) => airport.icao),
    event.startsAtUtc,
    event.endsAtUtc,
  );
  const sameDay = privateOneDay(event.privateSlots);
  const time = (value: string) => moment(value, sameDay ? { date: false } : {});
  const first = event.privateSlots[0];
  const airportOf = (icao: string): PublicEventAirportDto =>
    event.airports.find((airport) => airport.icao === icao) ?? { icao, name: null };

  // The hour as the page reads it now: a booking, or a refusal, reads the page again.
  const section = opened === null ? undefined : sections.find((candidate) => candidate.icao === opened.icao);
  const hour =
    opened === null || section === undefined
      ? undefined
      : (opened.isArrival ? section.arrivals : section.departures).find(
          (candidate) => candidate.fromUtc === opened.fromUtc,
        );

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:private.title')}>
      <div className="flex flex-col gap-1">
        <H2>{t('events:private.title')}</H2>
        <Subtle className="text-sm">{t('events:private.lead')}</Subtle>
        {sameDay && first !== undefined ? (
          <Subtle className="text-sm">
            {t('events:public.slotsOn', { day: moment(first.timeUtc, { time: false }) })}
          </Subtle>
        ) : null}
      </div>

      {sections.map((airport) => {
        const open = (isArrival: boolean, fromUtc: string, by: HTMLElement | null) => {
          openedBy.current = by;
          setOpened({ icao: airport.icao, isArrival, fromUtc });
        };
        const tables = (
          <>
            {airport.departures.length === 0 ? null : (
              <HourTable
                arrivals={false}
                hours={airport.departures}
                time={time}
                viewer={viewer}
                onOpen={(fromUtc, by) => open(false, fromUtc, by)}
              />
            )}
            {airport.arrivals.length === 0 ? null : (
              <HourTable
                arrivals
                hours={airport.arrivals}
                time={time}
                viewer={viewer}
                onOpen={(fromUtc, by) => open(true, fromUtc, by)}
              />
            )}
          </>
        );

        return sections.length === 1 ? (
          <Fragment key={airport.icao}>{tables}</Fragment>
        ) : (
          <section
            key={airport.icao}
            className="flex flex-col gap-4"
            aria-label={airportLabel(airportOf(airport.icao))}
          >
            <H3>
              <AirportName airport={airportOf(airport.icao)} />
            </H3>
            {tables}
          </section>
        );
      })}

      {opened === null || hour === undefined ? null : (
        <PrivateHourDialog
          event={event}
          airport={airportOf(opened.icao)}
          isArrival={opened.isArrival}
          hour={hour}
          time={time}
          viewer={viewer}
          onClose={() => setOpened(null)}
        />
      )}
    </section>
  );
}

/** The departures or the arrivals of one airport, hour by hour: the hour, how many of its slots are free, and the reader's. */
function HourTable({
  arrivals,
  hours,
  time,
  viewer,
  onOpen,
}: {
  arrivals: boolean;
  hours: readonly PrivateHour<PublicPrivateSlotDto>[];
  time: (value: string) => string;
  viewer: SlotViewer;
  onOpen: (fromUtc: string, by: HTMLElement | null) => void;
}) {
  const { t } = useTranslation();
  const Icon = arrivals ? PlaneLanding : PlaneTakeoff;

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <caption className="pb-2 text-left font-semibold">
          <span className="inline-flex items-center gap-2">
            <Icon aria-hidden className="size-4" />
            {t(arrivals ? 'events:public.arrivals' : 'events:public.departures')}
          </span>
        </caption>
        <thead>
          <tr className="text-muted-foreground border-b text-left">
            <th className="py-2 pr-3 font-medium">{t('events:private.hour')}</th>
            <th className="py-2 pr-3 font-medium">{t('events:private.free')}</th>
            <th className="py-2 font-medium">{t('events:public.slotState')}</th>
          </tr>
        </thead>
        <tbody>
          {hours.map((hour) => {
            const free = hour.slots.filter((slot) => !slot.taken).length;
            const yours = hour.slots.some((slot) => viewer.mine.has(slot.id));

            return (
              <tr
                key={hour.fromUtc}
                className="hover:bg-muted/50 cursor-pointer border-b align-top last:border-0"
                onClick={(click) => {
                  // The whole row opens the hour, as a slot's row does (E5); a click that ends selecting text opens nothing.
                  if (window.getSelection()?.isCollapsed === false) {
                    return;
                  }

                  onOpen(
                    hour.fromUtc,
                    click.currentTarget.querySelector<HTMLElement>('button[aria-haspopup="dialog"]'),
                  );
                }}
              >
                <td className="py-2 pr-3">
                  {/* The row opens with a click; the button is the way the keyboard and a reader reach it. */}
                  <button
                    type="button"
                    aria-haspopup="dialog"
                    className="tabular-nums underline-offset-4 hover:underline focus-visible:underline"
                    onClick={(click) => {
                      click.stopPropagation();
                      onOpen(hour.fromUtc, click.currentTarget);
                    }}
                  >
                    {t('events:private.range', { from: time(hour.fromUtc), to: time(hour.toUtc) })}
                  </button>
                </td>
                <td className="py-2 pr-3 tabular-nums">
                  {t('events:private.freeOf', { free, of: hour.slots.length })}
                </td>
                <td className="py-2">
                  {yours ? (
                    <Badge variant="flat" color="blue" text={t('events:public.yours')} />
                  ) : (
                    <Badge
                      variant="flat"
                      color={free === 0 ? 'gray' : 'green'}
                      text={t(free === 0 ? 'events:private.full' : 'events:public.free')}
                    />
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/**
 * One hour of private slots at an airport (§3.4): the reader's own in it, and the booking of one of its free slots with the flight they
 * fly. Offered only when the server would take it: to a signed in member, on an event whose bookings are open, with a free slot still
 * to come; otherwise it says why not — the bookings open later, the hour is full or past, a visitor signs in first and comes back.
 */
function PrivateHourDialog({
  event,
  airport,
  isArrival,
  hour,
  time,
  viewer,
  onClose,
}: {
  event: PublicEventDto;
  airport: PublicEventAirportDto;
  isArrival: boolean;
  hour: PrivateHour<PublicPrivateSlotDto>;
  time: (value: string) => string;
  viewer: SlotViewer;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  // The moment the dialog opened decides what it offers: a slot whose time passes while it is open is refused by the server.
  const [openedAt] = useState(() => Date.now());
  const mine = hour.slots.flatMap((slot) => {
    const booking = viewer.mine.get(slot.id);
    return booking === undefined ? [] : [{ slot, booking }];
  });

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t(isArrival ? 'events:private.dialog.arrivals' : 'events:private.dialog.departures', {
        airport: airportLabel(airport),
      })}
      description={t('events:private.dialog.hour', { from: time(hour.fromUtc), to: time(hour.toUtc) })}
    >
      {/* Two flights' fields are taller than a window, a phone's first: the dialog's own content scrolls, its title stays. */}
      <div className="scroll-thin -mr-2 flex max-h-[65vh] flex-col gap-4 overflow-y-auto pr-2">
        {mine.length === 0 ? null : (
          <ul className="flex flex-col gap-1 text-sm">
            {mine.map(({ slot, booking }) => (
              <li key={slot.id} className="flex items-center gap-2">
                <Badge variant="flat" color="blue" text={t('events:public.yours')} />
                <span className="tabular-nums">{time(slot.timeUtc)}</span>
                <span className="font-mono">{booking.callsign}</span>
              </li>
            ))}
          </ul>
        )}

        <PrivateBooking
          event={event}
          airport={airport}
          isArrival={isArrival}
          hour={hour}
          time={time}
          viewer={viewer}
          nowMs={openedAt}
        />
      </div>
    </Dialog>
  );
}

/**
 * The flight of a private slot (§3.4, E7): the slot among the free ones of the hour, the callsign, the aircraft, the other airport and
 * the time there — the generated form of its schema, so that every refusal of the server lands on its field. An arrival may take its
 * departure from the same airport too: the switch adds the departure's own flight, flown with the same aircraft, and the two are booked
 * together or not at all. A 409 is «try again», nothing booked.
 */
export function PrivateBooking({
  event,
  airport,
  isArrival,
  hour,
  time,
  viewer,
  nowMs,
}: {
  event: Pick<PublicEventDto, 'bookingOpensAtUtc' | 'cancelledAt' | 'privateSlots'>;
  airport: PublicEventAirportDto;
  isArrival: boolean;
  hour: PrivateHour<PublicPrivateSlotDto>;
  time: (value: string) => string;
  viewer: SlotViewer;
  nowMs: number;
}) {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const book = useBookPrivate();
  const switchId = useId();
  const [tryAgain, setTryAgain] = useState(false);

  // The slots as the page reads them now: a booking of another pilot, read again, takes its slot out of the choices.
  const free = bookableIn(hour, nowMs);
  const departures = isArrival ? linkableDepartures(event.privateSlots, airport.icao, hour.fromUtc) : [];
  const choices: PrivateFlightChoices = {
    slots: free.map((slot) => ({ value: String(slot.id), label: time(slot.timeUtc) })),
    departures: departures.map((slot) => ({ value: String(slot.id), label: time(slot.timeUtc) })),
  };

  // The form drawn — with the departure or without — and what it starts with; what it holds as it is written, so that the switch of
  // the departure keeps it (as the page that loads the slots keeps a table, E5). The generated form says what it holds a moment
  // after each keystroke (its live delay): the time a hand takes to reach the switch.
  const [drawn, setDrawn] = useState<{ withDeparture: boolean; values: PrivatePairValues }>(() => ({
    withDeparture: false,
    values: {
      slotId: choices.slots[0]?.value ?? '',
      callsign: '',
      aircraftIcao: '',
      otherIcao: '',
      departure: { slotId: choices.departures[0]?.value ?? '', callsign: '', otherIcao: '' },
    },
  }));
  const written = useRef<PrivatePairValues>(drawn.values);

  const turn = (withDeparture: boolean) => {
    const values = written.current;
    // The departure flies on as the arrival came, unless the pilot says otherwise.
    setDrawn({
      withDeparture,
      values: {
        ...values,
        departure: { ...values.departure, callsign: values.departure.callsign || values.callsign },
      },
    });
  };

  if (book.isSuccess) {
    return <PrivateBooked result={book.data} />;
  }

  if (event.cancelledAt !== null) {
    return null;
  }

  if (!bookingsOpen(event, nowMs)) {
    return <p className="text-muted-foreground text-sm">{t('events:public.booking.notOpen')}</p>;
  }

  if (free.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('events:private.dialog.noneFree')}</p>;
  }

  if (!viewer.signedIn) {
    // Back to this page, its query and its hash: `loginHref` makes a path of this site of the window's address.
    return (
      <p className="text-sm">
        <a href={loginHref(window.location.href)} className="underline">
          {t('events:private.dialog.signIn')}
        </a>
      </p>
    );
  }

  const submit = async (values: PrivateFlightValues | PrivatePairValues) => {
    setTryAgain(false);
    try {
      await book.mutateAsync(privateRequest(values));
    } catch (error) {
      // A 409 is «try again», nothing booked (E6a): the core's sentence for a 409 is about somebody else's change instead.
      if (error instanceof ApiError && error.status === 409) {
        setTryAgain(true);
        return;
      }

      throw error;
    }
  };

  const form = {
    locales: bootstrap?.division.locales ?? [],
    labels: `events:private.${isArrival ? 'arrival' : 'departure'}`,
    division: {
      defaultLocale: bootstrap?.division.defaultLocale ?? '',
      timezone: bootstrap?.division.timezone ?? 'UTC',
    },
    submitLabel: t('events:private.dialog.book'),
  };

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:private.dialog.panel')}>
      {choices.departures.length === 0 ? null : (
        <div className="flex items-center gap-2">
          <Switch id={switchId} checked={drawn.withDeparture} onCheckedChange={turn} />
          <Label htmlFor={switchId}>{t('events:private.dialog.withDeparture')}</Label>
        </div>
      )}

      {tryAgain ? <Notice tone="error" title={t('events:errors.bookingTryAgain')} /> : null}

      {drawn.withDeparture ? (
        <SchemaForm
          key="pair"
          schema={privatePairSchema(choices)}
          defaults={drawn.values}
          onChange={(values) => {
            written.current = values;
          }}
          onSubmit={submit}
          {...form}
        />
      ) : (
        <SchemaForm
          key="one"
          schema={privateFlightSchema(choices)}
          defaults={withoutDeparture(drawn.values)}
          onChange={(values) => {
            written.current = { ...values, departure: written.current.departure };
          }}
          onSubmit={submit}
          {...form}
        />
      )}
    </section>
  );
}

/** The flight of the form without a departure: what the pilot wrote of the booking itself. */
function withoutDeparture(values: PrivatePairValues): PrivateFlightValues {
  return {
    slotId: values.slotId,
    callsign: values.callsign,
    aircraftIcao: values.aircraftIcao,
    otherIcao: values.otherIcao,
    ...(values.otherTimeUtc === undefined ? {} : { otherTimeUtc: values.otherTimeUtc }),
  };
}

/** What booking a private slot made: the flight booked, its linked departure too, and the way to the reader's bookings. */
function PrivateBooked({ result }: { result: PrivateBookingDto }) {
  const { t } = useTranslation();

  return (
    <Notice
      tone="success"
      title={
        result.departure === null
          ? t('events:private.dialog.booked', { callsign: result.booking.callsign })
          : t('events:private.dialog.bookedWithDeparture', {
              callsign: result.booking.callsign,
              departure: result.departure.callsign,
            })
      }
      description={
        <RouterAnchor href={MY_EVENTS_PAGE} className="underline">
          {t('events:public.booking.toMine')}
        </RouterAnchor>
      }
    />
  );
}
