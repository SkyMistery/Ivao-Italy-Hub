import { Badge, Dialog, H2, H3, Subtle, Tooltip } from '@ivao/atmosphere-react';
import { PlaneLanding, PlaneTakeoff, Repeat } from 'lucide-react';
import { Fragment, useEffect, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { useMoment } from '../../../shared/i18n/useMoment';
import { ListFilter } from '../../../shared/list';
import { EmptyState } from '../../../shared/ui';
import type { PublicEventAirportDto, PublicEventDto, PublicEventSlotDto } from '../api';
import { SLOT_DIRECTIONS } from '../schemas';

import { airportLabel } from './cards';
import { AirportName } from './EventCards';
import { VISITOR, type SlotViewer } from './myBookings';
import { SlotBooking } from './SlotBooking';
import {
  anyFilter,
  narrowSlots,
  oneDay,
  rotationLegs,
  slotAirport,
  slotFilterChoices,
  slotSections,
  slotTime,
  type SlotFilters,
} from './slotList';

/**
 * The public slots of an event (design M4 §7.1, E5; note 2026-10-07-gli-slot-sulla-pagina-dell-evento): by the airport of the event
 * they are at — one section each when the slots are at more than one —, the departures and the arrivals in two tables, each by its
 * time at the airport; a slot's main aircraft type, with the others it admits on hover, focus or tap; a leg of a rotation in its
 * own table, marked with an icon; free or taken, never who took it (plan §9.7). A row opens the slot, with every type it admits and
 * the legs of its rotation. The times are UTC, the network's; when they all fall on one day, the day is said once above them.
 * Everything comes in the page's one read: no read of its own.
 *
 * E6b books here (design M4 §3.3): the filters over the list — arrivals or departures, the hours at the airport of the event, an
 * aircraft type, an airline, a rotation —, which the page keeps in its address; «Book» and «Book the whole rotation» in the dialog
 * of a slot (`SlotBooking`); and the reader's own slots said as theirs — from their own bookings, never from the page.
 */
export function EventSlots({
  event,
  filters = {},
  onFilter,
  viewer = VISITOR,
}: {
  event: PublicEventDto;
  /** What the list is narrowed to: the page's address. */
  filters?: SlotFilters;
  /** A filter chosen or cleared. Without it the list draws no filters. */
  onFilter?: (patch: Partial<SlotFilters>) => void;
  viewer?: SlotViewer;
}) {
  const { t } = useTranslation();
  const moment = useMoment();
  const [openedId, setOpenedId] = useState<number | null>(null);
  // The callsign that opened the slot. The dialog has no trigger of its own, so nothing would take the focus back when it closes —
  // a keyboard would be left at the top of a page of hundreds of rows (the review of #228, second reading, point 1).
  const openedBy = useRef<HTMLElement | null>(null);
  const open = (slot: PublicEventSlotDto, by: HTMLElement | null) => {
    openedBy.current = by;
    setOpenedId(slot.id);
  };
  // The slot as the page reads it now, not as it was when the dialog opened: a booking, or a refusal, reads the page again (E6b).
  const opened = openedId === null ? null : (event.slots.find((slot) => slot.id === openedId) ?? null);

  useEffect(() => {
    // Once the dialog is gone, and its focus trap with it: inside it a focus elsewhere would be taken back.
    if (openedId === null && openedBy.current !== null) {
      openedBy.current.focus();
      openedBy.current = null;
    }
  }, [openedId]);

  const sameDay = oneDay(event.slots);
  const time = (value: string) => moment(value, sameDay ? { date: false } : {});
  const first = event.slots[0];
  const shown = narrowSlots(event.slots, filters);

  const sections = slotSections(
    shown,
    event.airports.map((airport) => airport.icao),
  );
  // An airport by the name the page knows it by: the event's, or the one a slot carries.
  const airportOf = (icao: string): PublicEventAirportDto =>
    event.airports.find((airport) => airport.icao === icao) ??
    event.slots
      .map((slot) => (slot.isArrival ? slot.arrival : slot.departure))
      .find((airport) => airport.icao === icao) ?? {
      icao,
      name: null,
    };

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:public.slots')}>
      <div className="flex flex-col gap-1">
        <H2>{t('events:public.slots')}</H2>
        {sameDay && first !== undefined ? (
          <Subtle className="text-sm">
            {t('events:public.slotsOn', { day: moment(slotTime(first), { time: false }) })}
          </Subtle>
        ) : null}
      </div>

      {onFilter === undefined ? null : (
        <SlotFiltersBar slots={event.slots} filters={filters} sameDay={sameDay} onFilter={onFilter} />
      )}

      {shown.length === 0 && anyFilter(filters) ? (
        <EmptyState title={t('events:public.filters.noSlots')} />
      ) : null}

      {sections.map((section) => {
        const tables = (
          <>
            {section.departures.length === 0 ? null : (
              <SlotTable
                arrivals={false}
                slots={section.departures}
                time={time}
                viewer={viewer}
                onOpen={open}
              />
            )}
            {section.arrivals.length === 0 ? null : (
              <SlotTable arrivals slots={section.arrivals} time={time} viewer={viewer} onOpen={open} />
            )}
          </>
        );

        // One airport is the page's; more than one, each its own section (§4 of the note).
        return sections.length === 1 ? (
          <Fragment key={section.icao}>{tables}</Fragment>
        ) : (
          <section
            key={section.icao}
            className="flex flex-col gap-4"
            aria-label={airportLabel(airportOf(section.icao))}
          >
            <H3>
              <AirportName airport={airportOf(section.icao)} />
            </H3>
            {tables}
          </section>
        );
      })}

      {opened === null ? null : (
        <SlotDetail
          slot={opened}
          event={event}
          airport={airportOf(slotAirport(opened))}
          time={time}
          viewer={viewer}
          onClose={() => setOpenedId(null)}
        />
      )}
    </section>
  );
}

/**
 * The filters over the slots (design M4 §3.3, E6b): what the slots hold, each offered only when it narrows something — two
 * directions, more than one hour, type, airline or rotation. The hours are UTC at the airport of the event, written as the list
 * writes its times: the hour alone when every slot is on one day.
 */
function SlotFiltersBar({
  slots,
  filters,
  sameDay,
  onFilter,
}: {
  slots: readonly PublicEventSlotDto[];
  filters: SlotFilters;
  sameDay: boolean;
  onFilter: (patch: Partial<SlotFilters>) => void;
}) {
  const { t } = useTranslation();
  const moment = useMoment();
  const choices = slotFilterChoices(slots);
  const hour = (value: string) => moment(`${value}:00:00Z`, sameDay ? { date: false } : {});
  const both = slots.some((slot) => slot.isArrival) && slots.some((slot) => !slot.isArrival);
  const several = (values: readonly string[]) => values.length > 1;

  return (
    <div
      className="flex flex-wrap items-end gap-4"
      role="group"
      aria-label={t('events:public.filters.title')}
    >
      {both ? (
        <ListFilter
          className="min-w-36"
          id="slots-direction"
          label={t('events:public.filters.direction')}
          none={t('events:public.filters.bothDirections')}
          value={filters.direction}
          onChange={(direction) => onFilter({ direction: direction as SlotFilters['direction'] })}
          items={SLOT_DIRECTIONS.map((direction) => ({
            value: direction,
            label: t(`events:public.${direction}`),
          }))}
        />
      ) : null}
      {several(choices.hours) ? (
        <>
          <ListFilter
            className="min-w-36"
            id="slots-from"
            label={t('events:public.filters.from')}
            none={t('events:public.filters.anyHour')}
            value={filters.from}
            onChange={(from) => onFilter({ from })}
            items={choices.hours.map((value) => ({ value, label: hour(value) }))}
          />
          <ListFilter
            className="min-w-36"
            id="slots-until"
            label={t('events:public.filters.until')}
            none={t('events:public.filters.anyHour')}
            value={filters.until}
            onChange={(until) => onFilter({ until })}
            items={choices.ends.map((value) => ({ value, label: hour(value) }))}
          />
        </>
      ) : null}
      {several(choices.types) ? (
        <ListFilter
          className="min-w-36"
          id="slots-type"
          label={t('events:public.aircraft')}
          none={t('events:public.filters.anyType')}
          value={filters.type}
          onChange={(type) => onFilter({ type })}
          items={choices.types.map((value) => ({ value, label: value }))}
        />
      ) : null}
      {several(choices.airlines) ? (
        <ListFilter
          className="min-w-36"
          id="slots-airline"
          label={t('events:public.filters.airline')}
          none={t('events:public.filters.anyAirline')}
          value={filters.airline}
          onChange={(airline) => onFilter({ airline })}
          items={choices.airlines.map((value) => ({ value, label: value }))}
        />
      ) : null}
      {choices.rotations.length > 0 ? (
        <ListFilter
          className="min-w-36"
          id="slots-rotation"
          label={t('events:public.filters.rotation')}
          none={t('events:public.filters.anyRotation')}
          value={filters.rotation}
          onChange={(rotation) => onFilter({ rotation })}
          items={choices.rotations.map((value) => ({ value, label: value }))}
        />
      ) : null}
    </div>
  );
}

/** The departures or the arrivals of one airport of the event: the flight, its main type, the other airport, the time there. */
function SlotTable({
  arrivals,
  slots,
  time,
  viewer,
  onOpen,
}: {
  arrivals: boolean;
  slots: readonly PublicEventSlotDto[];
  time: (value: string) => string;
  viewer: SlotViewer;
  onOpen: (slot: PublicEventSlotDto, by: HTMLElement | null) => void;
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
            <th className="py-2 pr-3 font-medium">{t('events:slots.fields.callsign')}</th>
            <th className="py-2 pr-3 font-medium">{t('events:public.aircraft')}</th>
            <th className="py-2 pr-3 font-medium">
              {t(arrivals ? 'events:public.origin' : 'events:public.destination')}
            </th>
            <th className="py-2 pr-3 font-medium">
              {t(arrivals ? 'events:public.onBlock' : 'events:public.offBlock')}
            </th>
            <th className="py-2 pr-3 font-medium">{t('events:slots.fields.stand')}</th>
            <th className="py-2 font-medium">{t('events:public.slotState')}</th>
          </tr>
        </thead>
        <tbody>
          {slots.map((slot) => (
            <tr
              key={slot.id}
              className="hover:bg-muted/50 cursor-pointer border-b align-top last:border-0"
              onClick={(click) => {
                // The whole row opens the slot, as asked and decided (§6 of the note): on a phone it is the target a thumb finds.
                // A click that ends selecting some text — a flight number to copy — opens nothing.
                if (window.getSelection()?.isCollapsed === false) {
                  return;
                }

                onOpen(
                  slot,
                  click.currentTarget.querySelector<HTMLElement>('button[aria-haspopup="dialog"]'),
                );
              }}
            >
              <td className="py-2 pr-3">
                <span className="inline-flex items-center gap-1">
                  {/* The row opens with a click; the button is the way the keyboard and a reader reach it. */}
                  <button
                    type="button"
                    aria-haspopup="dialog"
                    className="font-mono underline-offset-4 hover:underline focus-visible:underline"
                    onClick={(click) => {
                      click.stopPropagation();
                      onOpen(slot, click.currentTarget);
                    }}
                  >
                    {slot.callsign}
                  </button>
                  {slot.rotation === null ? null : <RotationMark />}
                </span>
                {slot.flightNumber === null ? null : <Subtle className="text-xs">{slot.flightNumber}</Subtle>}
              </td>
              <td className="py-2 pr-3">
                <AircraftTypes types={slot.aircraftTypes} />
              </td>
              <td className="py-2 pr-3">
                <AirportName airport={arrivals ? slot.departure : slot.arrival} />
              </td>
              <td className="py-2 pr-3 tabular-nums">{time(slotTime(slot))}</td>
              <td className="py-2 pr-3">{slot.stand ?? '—'}</td>
              <td className="py-2">
                <SlotState taken={slot.taken} yours={viewer.mine.has(slot.id)} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** A slot's main aircraft type — the first it admits —, and the others it admits too on hover, focus or tap. */
function AircraftTypes({ types }: { types: readonly string[] }) {
  const { t } = useTranslation();
  const [main, ...others] = types;

  if (others.length === 0) {
    return <span className="font-mono">{main}</span>;
  }

  return (
    <Hint text={t('events:public.otherTypes', { types: others.join(', ') })}>
      <span className="font-mono">{main}</span>
      <span className="text-muted-foreground text-xs">
        {t('events:public.moreTypes', { count: others.length })}
      </span>
    </Hint>
  );
}

/** A leg of a rotation: an icon that says so, and that its row opens with the other legs. */
function RotationMark() {
  const { t } = useTranslation();
  const text = t('events:public.rotationHint');

  return (
    <Hint text={text} label={text}>
      <Repeat aria-hidden className="text-primary size-4" />
    </Hint>
  );
}

/**
 * Atmosphere's tooltip, opened by a tap too (§2 of the note): a phone has no hover, and a Radix tooltip opens only for a mouse that
 * moves and on focus. The trigger is a button, so the keyboard reaches it and its words are read on focus; a press turns it over,
 * a tap included, and opens no row it sits in. **A piece of this screen**, not a component of the module (Carmine, point 8 on
 * #228): the day a second screen wants a tooltip that opens on a tap, that is a decision to bring to him, not a copy.
 *
 * ⚠️ What a press turns over is what was shown **when the press began**: between the press and its click Radix closes the tooltip
 * (a press on its trigger, a tap anywhere) and opens it again (a tap focuses the button after the finger lifts), so the state at
 * the click says nothing. A click without a press — the keyboard's, `detail` 0 — turns over what is shown, and a press that never
 * becomes a click (a scroll begun on the button) is forgotten.
 *
 * With a `label` the tooltip only shows the button's own name: it is not given as its description too, or a reader would hear it
 * twice.
 */
function Hint({ text, label, children }: { text: string; label?: string; children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const shownAtPress = useRef<boolean | null>(null);

  return (
    <Tooltip content={text} open={open} onOpenChange={setOpen}>
      <button
        type="button"
        {...(label === undefined ? {} : { 'aria-label': label, 'aria-describedby': undefined })}
        className="inline-flex items-center gap-1 rounded-sm"
        onPointerDown={() => {
          shownAtPress.current = open;
        }}
        onPointerCancel={() => {
          shownAtPress.current = null;
        }}
        onClick={(click) => {
          // Radix closes a tooltip on a click; here the click is how a phone opens it.
          click.preventDefault();
          click.stopPropagation();
          const shown = click.detail === 0 ? open : (shownAtPress.current ?? open);
          shownAtPress.current = null;
          setOpen(!shown);
        }}
      >
        {children}
      </button>
    </Tooltip>
  );
}

/**
 * Free or taken, and nothing of whoever took it — unless the reader did, which their own bookings say (E6b): a slot of theirs is
 * «yours», so that a pilot finds what they booked without reading «taken» on it.
 */
function SlotState({ taken, yours = false }: { taken: boolean; yours?: boolean }) {
  const { t } = useTranslation();

  return yours ? (
    <Badge variant="flat" color="blue" text={t('events:public.yours')} />
  ) : (
    <Badge
      variant="flat"
      color={taken ? 'gray' : 'green'}
      text={t(taken ? 'events:public.taken' : 'events:public.free')}
    />
  );
}

/**
 * One slot (§6 of the note): every aircraft type it admits, the main one first; where and when it leaves and lands; its stand; free
 * or taken; the legs of its rotation, this one marked; and «Book» (E6b), with the type the pilot flies chosen among those it admits.
 */
function SlotDetail({
  slot,
  event,
  airport,
  time,
  viewer,
  onClose,
}: {
  slot: PublicEventSlotDto;
  event: PublicEventDto;
  airport: PublicEventAirportDto;
  time: (value: string) => string;
  viewer: SlotViewer;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const legs = slot.rotation === null ? [] : rotationLegs(event.slots, slot.rotation);
  // The moment the dialog opened decides what it offers: a slot whose off block passes while it is open is refused by the server.
  const [openedAt] = useState(() => Date.now());
  const title = slot.flightNumber === null ? slot.callsign : `${slot.callsign} · ${slot.flightNumber}`;

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={title}
      description={t(
        slot.isArrival ? 'events:public.detail.arrivalAt' : 'events:public.detail.departureFrom',
        {
          airport: airportLabel(airport),
        },
      )}
    >
      <dl className="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-2 text-sm">
        <dt className="text-muted-foreground">{t('events:public.detail.types')}</dt>
        <dd>
          <ul className="flex flex-wrap gap-x-3 gap-y-1">
            {slot.aircraftTypes.map((type, index) => (
              <li key={type} className="font-mono">
                {type}
                {index === 0 ? (
                  <span className="text-muted-foreground font-sans"> ({t('events:public.detail.main')})</span>
                ) : null}
              </li>
            ))}
          </ul>
        </dd>

        <dt className="text-muted-foreground">{t('events:public.detail.departure')}</dt>
        <dd>
          <AirportName airport={slot.departure} /> ·{' '}
          <span className="tabular-nums">{time(slot.offBlockUtc)}</span>
        </dd>

        <dt className="text-muted-foreground">{t('events:public.detail.arrival')}</dt>
        <dd>
          <AirportName airport={slot.arrival} /> ·{' '}
          <span className="tabular-nums">{time(slot.onBlockUtc)}</span>
        </dd>

        <dt className="text-muted-foreground">{t('events:slots.fields.stand')}</dt>
        <dd>{slot.stand ?? '—'}</dd>

        <dt className="text-muted-foreground">{t('events:public.slotState')}</dt>
        <dd>
          <SlotState taken={slot.taken} yours={viewer.mine.has(slot.id)} />
        </dd>
      </dl>

      {slot.rotation === null || legs.length === 0 ? null : (
        <section
          className="flex flex-col gap-2"
          aria-label={t('events:public.detail.legs', { rotation: slot.rotation })}
        >
          <h3 className="text-sm font-semibold">
            {t('events:public.detail.legs', { rotation: slot.rotation })}
          </h3>
          <ol className="flex flex-col gap-1 text-sm">
            {legs.map((leg) => (
              <li
                key={leg.id}
                {...(leg.id === slot.id ? { 'aria-current': 'true' as const } : {})}
                className={leg.id === slot.id ? 'font-semibold' : undefined}
              >
                {leg.leg === null ? null : <>{t('events:public.leg', { leg: leg.leg })} · </>}
                <span className="font-mono">{leg.callsign}</span> ·{' '}
                <span className="font-mono">{leg.departure.icao}</span>{' '}
                <span className="tabular-nums">{time(leg.offBlockUtc)}</span> →{' '}
                <span className="font-mono">{leg.arrival.icao}</span>{' '}
                <span className="tabular-nums">{time(leg.onBlockUtc)}</span>
                {leg.id === slot.id ? (
                  <span className="text-muted-foreground font-normal">
                    {' '}
                    ({t('events:public.detail.thisLeg')})
                  </span>
                ) : null}
              </li>
            ))}
          </ol>
        </section>
      )}

      <SlotBooking slot={slot} event={event} viewer={viewer} nowMs={openedAt} />
    </Dialog>
  );
}
