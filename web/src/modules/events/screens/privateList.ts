import type { PrivateBookingRequest } from '../api';
import type { PrivateFlightValues, PrivatePairValues } from '../schemas';

/**
 * The private slots of an event as data (design M4 §3.2, §3.4, §7.1, E7): the page offers them by airport, direction and hour, and the
 * dialog of an hour books one with the flight the pilot writes — an arrival with its linked departure from the same airport, when
 * asked. Plain TypeScript beside the components, so that a test reads it without drawing anything.
 */

/** A private slot as the page of its event gets it (`PublicPrivateSlotDto`): an airport, a direction, the time there, taken or not. */
export interface PrivateSlot {
  readonly id: number;
  readonly airportIcao: string;
  readonly isArrival: boolean;
  readonly timeUtc: string;
  readonly taken: boolean;
}

/**
 * One hour of the event at one airport, in one direction: its private slots by their time. The hours are the event's — from its start,
 * an hour each, the last one cut at its end —, the hours the generator gave each its part of the capacity in.
 */
export interface PrivateHour<TSlot extends PrivateSlot> {
  readonly fromUtc: string;
  readonly toUtc: string;
  readonly slots: readonly TSlot[];
}

/** The private slots at one airport of the event: its departures and its arrivals, hour by hour. */
export interface PrivateSection<TSlot extends PrivateSlot> {
  readonly icao: string;
  readonly departures: readonly PrivateHour<TSlot>[];
  readonly arrivals: readonly PrivateHour<TSlot>[];
}

const HOUR = 3_600_000;

/**
 * The private slots by airport — in the order of the event's airports, one it no longer lists after them by its code —, and in each the
 * departures and the arrivals hour by hour, each hour's slots by their time, then as the server sent them.
 */
export function privateSections<TSlot extends PrivateSlot>(
  slots: readonly TSlot[],
  airports: readonly string[],
  startsAtUtc: string,
  endsAtUtc: string,
): PrivateSection<TSlot>[] {
  const starts = Date.parse(startsAtUtc);
  const ends = Date.parse(endsAtUtc);
  const byAirport = new Map<string, { departures: Map<number, TSlot[]>; arrivals: Map<number, TSlot[]> }>();

  for (const slot of slots) {
    const section = byAirport.get(slot.airportIcao) ?? {
      departures: new Map<number, TSlot[]>(),
      arrivals: new Map<number, TSlot[]>(),
    };
    byAirport.set(slot.airportIcao, section);

    const hours = slot.isArrival ? section.arrivals : section.departures;
    const index = Math.floor((Date.parse(slot.timeUtc) - starts) / HOUR);
    hours.set(index, [...(hours.get(index) ?? []), slot]);
  }

  const place = (icao: string) => {
    const index = airports.indexOf(icao);
    return index < 0 ? airports.length : index;
  };
  const byTime = (one: TSlot, other: TSlot) =>
    Date.parse(one.timeUtc) - Date.parse(other.timeUtc) || one.id - other.id;
  // The last hour is cut at the end of the event; a slot after the end, which no generation makes, keeps a whole hour.
  const hoursOf = (hours: Map<number, TSlot[]>): PrivateHour<TSlot>[] =>
    [...hours.entries()]
      .sort(([one], [other]) => one - other)
      .map(([index, inTheHour]) => {
        const from = starts + index * HOUR;
        const to = from >= ends ? from + HOUR : Math.min(from + HOUR, ends);

        return {
          fromUtc: new Date(from).toISOString(),
          toUtc: new Date(to).toISOString(),
          slots: inTheHour.sort(byTime),
        };
      });

  return [...byAirport.entries()]
    .sort(([one], [other]) => place(one) - place(other) || one.localeCompare(other))
    .map(([icao, section]) => ({
      icao,
      departures: hoursOf(section.departures),
      arrivals: hoursOf(section.arrivals),
    }));
}

/** The slots of an hour a pilot may still book at `nowMs`: free, and their time at the airport to come. */
export function bookableIn<TSlot extends PrivateSlot>(hour: PrivateHour<TSlot>, nowMs: number): TSlot[] {
  return hour.slots.filter((slot) => !slot.taken && Date.parse(slot.timeUtc) > nowMs);
}

/**
 * The private departures an arrival of an hour may link (§3.4): from the same airport, free, and leaving after that hour has begun —
 * the server says when one leaves too soon after the arrival lands —, by their time.
 */
export function linkableDepartures<TSlot extends PrivateSlot>(
  slots: readonly TSlot[],
  icao: string,
  afterUtc: string,
): TSlot[] {
  const after = Date.parse(afterUtc);

  return slots
    .filter(
      (slot) =>
        !slot.isArrival && slot.airportIcao === icao && !slot.taken && Date.parse(slot.timeUtc) > after,
    )
    .sort((one, other) => Date.parse(one.timeUtc) - Date.parse(other.timeUtc) || one.id - other.id);
}

/** Whether every private slot falls on one day in UTC: then the page shows the hours, and the day once above them. */
export function privateOneDay(slots: readonly PrivateSlot[]): boolean {
  return new Set(slots.map((slot) => slot.timeUtc.slice(0, 10))).size <= 1;
}

/**
 * What the dialog of an hour sends (`PrivateBookingRequest`): the slot chosen, the aircraft and the flight — codes in capitals, as the
 * server keeps them —, and the linked departure's own flight when the form has one. An empty time is none: the server says it is
 * required.
 */
export function privateRequest(values: PrivateFlightValues | PrivatePairValues): PrivateBookingRequest {
  const flight = (written: { callsign: string; otherIcao: string; otherTimeUtc?: string | undefined }) => ({
    callsign: written.callsign.trim().toUpperCase(),
    otherIcao: written.otherIcao.trim().toUpperCase(),
    otherTimeUtc:
      written.otherTimeUtc === undefined || written.otherTimeUtc === '' ? null : written.otherTimeUtc,
  });

  return {
    slotId: Number(values.slotId),
    aircraftIcao: values.aircraftIcao.trim().toUpperCase(),
    ...flight(values),
    departure:
      'departure' in values ? { slotId: Number(values.departure.slotId), ...flight(values.departure) } : null,
  };
}
