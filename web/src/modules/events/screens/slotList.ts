import { SLOT_COLUMNS } from '../schemas';

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
