import { SLOT_COLUMNS } from '../schemas';

/**
 * The public slots of an event as data (design M4 §3.1, §7.1, E5): the page lists them with their rotations grouped, and the page
 * that loads a table lists what the server refused, row by row. Plain TypeScript beside the components, so that a test reads it
 * without drawing anything.
 */

/** A public slot as the page of its event gets it: what the grouping reads of it. */
export interface PublicSlot {
  readonly id: number;
  readonly offBlockUtc: string;
  readonly onBlockUtc: string;
  readonly rotation: string | null;
  readonly leg: number | null;
}

/** The slots of one rotation, by their places — or one slot alone, with no rotation. */
export interface SlotGroup<TSlot extends PublicSlot> {
  readonly rotation: string | null;
  readonly slots: readonly TSlot[];
}

/**
 * The slots with their rotations grouped (§7.1): a rotation's legs together, by their places, where its first leg falls among the
 * slots by off block; a slot alone where its own off block falls. The server sends them by off block.
 */
export function slotGroups<TSlot extends PublicSlot>(slots: readonly TSlot[]): SlotGroup<TSlot>[] {
  const groups: { rotation: string | null; slots: TSlot[] }[] = [];
  const byRotation = new Map<string, { rotation: string; slots: TSlot[] }>();

  for (const slot of slots) {
    if (slot.rotation === null) {
      groups.push({ rotation: null, slots: [slot] });
      continue;
    }

    const known = byRotation.get(slot.rotation);
    if (known === undefined) {
      const group = { rotation: slot.rotation, slots: [slot] };
      byRotation.set(slot.rotation, group);
      groups.push(group);
    } else {
      known.slots.push(slot);
    }
  }

  for (const group of byRotation.values()) {
    group.slots.sort((one, other) => (one.leg ?? 0) - (other.leg ?? 0));
  }

  return groups;
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
