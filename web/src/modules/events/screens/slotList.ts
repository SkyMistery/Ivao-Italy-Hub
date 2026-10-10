import { SLOT_COLUMNS, type SLOT_DIRECTIONS } from '../schemas';

/**
 * The public slots of an event as data (design M4 §3.1, §7.1, E5): the page lists them by the airport of the event they are at, its
 * departures and its arrivals apart, and opens one with the legs of its rotation (note 2026-10-07-gli-slot-sulla-pagina-dell-evento);
 * the page that loads a table lists what the server refused, row by row. Plain TypeScript beside the components, so that a test
 * reads it without drawing anything.
 */

/** A public slot as the page of its event gets it: what the sections and the detail read of it. */
export interface PublicSlot {
  readonly id: number;
  readonly offBlockUtc: string;
  readonly onBlockUtc: string;
  readonly rotation: string | null;
  readonly leg: number | null;
  readonly isArrival: boolean;
  readonly departure: { readonly icao: string };
  readonly arrival: { readonly icao: string };
}

/** A public slot as the filters of the page read it (E6b): its callsign and the aircraft types it admits, besides the above. */
export interface FilteredSlot extends PublicSlot {
  readonly callsign: string;
  readonly aircraftTypes: readonly string[];
}

export type SlotDirection = (typeof SLOT_DIRECTIONS)[number];

/**
 * What the list of the slots is narrowed to (design M4 §3.3, E6b): arrivals or departures, a stretch of hours at the airport of the
 * event, an aircraft type, an airline, a rotation. Each left out lets everything through; they all come from the address.
 */
export interface SlotFilters {
  readonly direction?: SlotDirection | undefined;
  /** The first hour shown, as `2026-10-26T18`: the hour of the time at the airport of the event, in UTC. */
  readonly from?: string | undefined;
  /** The hour the list stops at, written the same way: a slot at 20:00 is not shown «until 20:00», one at 19:59 is. */
  readonly until?: string | undefined;
  /** An aircraft type the slot admits — the main one or another. */
  readonly type?: string | undefined;
  /** The letters a callsign starts with (`slotAirline`). */
  readonly airline?: string | undefined;
  /** The code of a rotation: its legs. */
  readonly rotation?: string | undefined;
}

/** The hour of an instant, in UTC, as the filters write it: `2026-10-26T18`. */
export function slotHour(instant: string): string {
  return new Date(instant).toISOString().slice(0, 13);
}

/** The hour after one written so: `2026-10-26T19` after `2026-10-26T18`, the next day after `T23`. */
export function nextHour(hour: string): string {
  return new Date(Date.parse(`${hour}:00:00Z`) + 3_600_000).toISOString().slice(0, 13);
}

/**
 * The airline of a slot, as its callsign says it: the letters it starts with, up to its first digit — `XYZ` of `XYZ123`. A callsign
 * that starts with a digit names none. Read off the callsign because a slot has no column of its own for it: the flight number is
 * left empty as often as not.
 */
export function slotAirline(callsign: string): string | null {
  const letters = /^[A-Za-z]+(?=\d)/.exec(callsign.trim());
  return letters === null ? null : letters[0].toUpperCase();
}

/** Whether a slot passes every filter written. */
export function passes(slot: FilteredSlot, filters: SlotFilters): boolean {
  const hour = slotHour(slotTime(slot));

  return (
    (filters.direction === undefined || slot.isArrival === (filters.direction === 'arrivals')) &&
    (filters.from === undefined || hour >= filters.from) &&
    (filters.until === undefined || hour < filters.until) &&
    (filters.type === undefined || slot.aircraftTypes.includes(filters.type)) &&
    (filters.airline === undefined || slotAirline(slot.callsign) === filters.airline) &&
    (filters.rotation === undefined || slot.rotation === filters.rotation)
  );
}

/** The slots that pass every filter written, in the order they came. */
export function narrowSlots<TSlot extends FilteredSlot>(
  slots: readonly TSlot[],
  filters: SlotFilters,
): TSlot[] {
  return slots.filter((slot) => passes(slot, filters));
}

/** Whether any filter is written: then a list that holds nothing says so, rather than looking like an event with no slots. */
export function anyFilter(filters: SlotFilters): boolean {
  return Object.values(filters).some((value) => value !== undefined);
}

/**
 * What each filter offers: what the slots hold, once each and in order — an option no slot has would narrow to nothing. The hours a
 * list starts from are the hours of its slots, the hours it stops at the hours after them.
 */
export interface SlotFilterChoices {
  readonly hours: string[];
  readonly ends: string[];
  readonly types: string[];
  readonly airlines: string[];
  readonly rotations: string[];
}

export function slotFilterChoices(slots: readonly FilteredSlot[]): SlotFilterChoices {
  const sorted = (values: Iterable<string>) => [...new Set(values)].sort();
  const hours = sorted(slots.map((slot) => slotHour(slotTime(slot))));

  return {
    hours,
    ends: hours.map(nextHour),
    types: sorted(slots.flatMap((slot) => slot.aircraftTypes)),
    airlines: sorted(slots.map((slot) => slotAirline(slot.callsign)).filter((airline) => airline !== null)),
    rotations: sorted(slots.map((slot) => slot.rotation).filter((rotation) => rotation !== null)),
  };
}

/**
 * The airport of the event a slot is at: where a departure leaves from, where an arrival lands. The server read it off the two
 * airports when the slot was written — a flight between two airports of the event is a departure of the first (design M4 §1.5).
 */
export function slotAirport(slot: PublicSlot): string {
  return slot.isArrival ? slot.arrival.icao : slot.departure.icao;
}

/** The time of a slot at the airport of the event: the off block of a departure, the on block of an arrival. */
export function slotTime(slot: PublicSlot): string {
  return slot.isArrival ? slot.onBlockUtc : slot.offBlockUtc;
}

/** The slots at one airport of the event: its departures and its arrivals, each by its time there. */
export interface SlotSection<TSlot extends PublicSlot> {
  readonly icao: string;
  readonly departures: readonly TSlot[];
  readonly arrivals: readonly TSlot[];
}

/**
 * The slots by the airport of the event they are at, in the order of the event's airports — one it no longer lists after them, by
 * its code —, and in each the departures and the arrivals apart, by their time there, then as the server sent them. A rotation's
 * legs stay each in its own table: the leg out among the departures, the leg back among the arrivals.
 */
export function slotSections<TSlot extends PublicSlot>(
  slots: readonly TSlot[],
  airports: readonly string[],
): SlotSection<TSlot>[] {
  const byAirport = new Map<string, { departures: TSlot[]; arrivals: TSlot[] }>();

  for (const slot of slots) {
    const icao = slotAirport(slot);
    const section = byAirport.get(icao) ?? { departures: [], arrivals: [] };
    byAirport.set(icao, section);
    (slot.isArrival ? section.arrivals : section.departures).push(slot);
  }

  const place = (icao: string) => {
    const index = airports.indexOf(icao);
    return index < 0 ? airports.length : index;
  };
  const byTime = (one: TSlot, other: TSlot) =>
    Date.parse(slotTime(one)) - Date.parse(slotTime(other)) || one.id - other.id;

  return [...byAirport.entries()]
    .sort(([one], [other]) => place(one) - place(other) || one.localeCompare(other))
    .map(([icao, section]) => ({
      icao,
      departures: section.departures.sort(byTime),
      arrivals: section.arrivals.sort(byTime),
    }));
}

/** The legs of a rotation, by their places — what the detail of one of them lists. */
export function rotationLegs<TSlot extends PublicSlot>(slots: readonly TSlot[], rotation: string): TSlot[] {
  return slots
    .filter((slot) => slot.rotation === rotation)
    .sort(
      (one, other) =>
        (one.leg ?? 0) - (other.leg ?? 0) || Date.parse(one.offBlockUtc) - Date.parse(other.offBlockUtc),
    );
}

/** Whether every time of the slots falls on one day in UTC: then the list shows the hours, and the day once above them. */
export function oneDay(slots: readonly PublicSlot[]): boolean {
  const days = new Set(
    slots.flatMap((slot) => [slot.offBlockUtc.slice(0, 10), slot.onBlockUtc.slice(0, 10)]),
  );
  return days.size <= 1;
}

/** One refusal of a load: the row of the table and its column when it is about a cell, and its i18n keys. */
export interface SheetProblem {
  readonly row: number | null;
  readonly column: string | null;
  readonly keys: readonly string[];
}

const CELL = /^rows\[(\d+)\]\.(\w+)$/;

/**
 * What the server refused of a table, in the order of the table: by row, and in a row by the order of its columns; what is about
 * the whole table first. The server keys a cell as `rows[12].aircraft_types` — the row as the table numbers it, the column as its
 * header names it.
 */
export function sheetProblems(errors: Readonly<Record<string, readonly string[]>>): SheetProblem[] {
  const order = (column: string | null) => {
    const index = column === null ? -1 : SLOT_COLUMNS.indexOf(column as (typeof SLOT_COLUMNS)[number]);
    return index < 0 ? SLOT_COLUMNS.length : index;
  };

  return Object.entries(errors)
    .map(([field, keys]): SheetProblem => {
      const cell = CELL.exec(field);
      return cell === null
        ? { row: null, column: null, keys }
        : { row: Number(cell[1]), column: cell[2] ?? null, keys };
    })
    .sort((one, other) =>
      one.row === other.row
        ? order(one.column) - order(other.column)
        : one.row === null
          ? -1
          : other.row === null
            ? 1
            : one.row - other.row,
    );
}
