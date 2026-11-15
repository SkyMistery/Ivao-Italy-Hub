import type { z } from 'zod';

import { enumOptionsOf, type Suggestion } from '../../shared/forms';

import type { AtcPositionDto } from './queries';

/**
 * The ATC positions of the division in a block that names them (design of the editor, phase 3, §5.8): the frequency table's
 * stations. Chosen, a position is **copied** into the row — its callsign as IVAO spells it, its name, kind and frequency — and
 * the row then keeps what was copied: a SOP is approved as it is written, and changes when somebody publishes it again
 * (Carmine, 31 October 2026, answer 6 of the design). What the editor adds is the comparison: a row that is no longer what
 * IVAO says now, and «Realign», which copies it again.
 */

/** Where a block keeps its positions: what `atcPositionListOf` answers. */
export interface AtcPositionList {
  list: string;
  field: string;
  entry: z.ZodObject<z.ZodRawShape>;
}

/** One value of a row that IVAO now says otherwise. */
export interface AtcPositionChange {
  field: string;
  written: string;
  ivao: string;
}

/** A row of the block whose position IVAO now describes otherwise, by its place in the list. */
export interface AtcPositionDrift {
  index: number;
  callsign: string;
  changes: AtcPositionChange[];
}

/**
 * What choosing `position` copies into the fields beside the callsign: the name, when the entry has a field for it and IVAO
 * gave one; the kind, when the entry's kinds include IVAO's; the frequency, once the synchronisation has brought it (until
 * then it is written by hand, and nothing writes an empty one over it).
 */
export function copiedFrom(
  position: AtcPositionDto,
  entry: AtcPositionList['entry'],
): Record<string, string> {
  const copied: Record<string, string> = {};

  if (Object.hasOwn(entry.shape, 'name') && position.name !== '') {
    copied.name = position.name;
  }

  if (enumOptionsOf(entry, 'kind')?.includes(position.type) === true) {
    copied.kind = position.type;
  }

  if (
    Object.hasOwn(entry.shape, 'frequency') &&
    typeof position.frequency === 'string' &&
    position.frequency !== ''
  ) {
    copied.frequency = position.frequency;
  }

  return copied;
}

/**
 * The positions as the callsign field offers them: the callsign is the value, the name and frequency say which one it is,
 * the FIR groups them — a couple of hundred stations are a wall without headings — and each carries what it copies.
 */
export function positionSuggestions(
  positions: readonly AtcPositionDto[],
  entry: AtcPositionList['entry'],
): Suggestion[] {
  return positions.map((position) => ({
    value: position.callsign,
    label: [position.name || position.callsign, position.frequency].filter(Boolean).join(' · '),
    ...(position.fir === null ? {} : { group: position.fir }),
    fills: copiedFrom(position, entry),
  }));
}

/** The rows of a block, read leniently: a body written by hand may hold anything there. */
function rowsOf(props: Record<string, unknown>, where: AtcPositionList): Record<string, unknown>[] {
  const rows = props[where.list];
  return Array.isArray(rows)
    ? rows.map((row) => (typeof row === 'object' && row !== null ? (row as Record<string, unknown>) : {}))
    : [];
}

const text = (value: unknown) => (typeof value === 'string' ? value.trim() : '');

/**
 * Whether two frequencies are the same one: `118.7` written by hand is IVAO's `118.700`. Compared as numbers when both are,
 * as words otherwise.
 */
function sameFrequency(written: string, ivao: string): boolean {
  const [a, b] = [Number(written), Number(ivao)];
  return written !== '' && Number.isFinite(a) && Number.isFinite(b) ? a === b : written === ivao;
}

/** The position a row names, by its callsign in any case: the one IVAO lists, or none. */
function positionOf(
  row: Record<string, unknown>,
  where: AtcPositionList,
  byCallsign: ReadonlyMap<string, AtcPositionDto>,
): AtcPositionDto | undefined {
  return byCallsign.get(text(row[where.field]).toUpperCase());
}

const indexed = (positions: readonly AtcPositionDto[]) =>
  new Map(positions.map((position) => [position.callsign.toUpperCase(), position]));

/**
 * The rows that are not what IVAO says now about their position: the callsign spelt otherwise, a kind or frequency that has
 * changed, a name written that is no longer IVAO's. A name **not** written is not a difference — a table written before
 * PR 3.14 has none, and every row of it would otherwise be flagged — and neither is a station IVAO does not list.
 */
export function driftOf(
  props: Record<string, unknown>,
  where: AtcPositionList,
  positions: readonly AtcPositionDto[],
): AtcPositionDrift[] {
  const byCallsign = indexed(positions);

  return rowsOf(props, where).flatMap((row, index) => {
    const position = positionOf(row, where, byCallsign);
    if (position === undefined) {
      return [];
    }

    const changes: AtcPositionChange[] = [];
    const written = text(row[where.field]);
    if (written !== position.callsign) {
      changes.push({ field: where.field, written, ivao: position.callsign });
    }

    for (const [field, ivao] of Object.entries(copiedFrom(position, where.entry))) {
      const held = text(row[field]);
      const same = field === 'frequency' ? sameFrequency(held, ivao) : held === ivao;
      if (!same && !(field === 'name' && held === '')) {
        changes.push({ field, written: held, ivao });
      }
    }

    return changes.length === 0 ? [] : [{ index, callsign: position.callsign, changes }];
  });
}

/** The block's props with the row at `index` copied again from IVAO's position, the rest of the row as it was. */
export function realigned(
  props: Record<string, unknown>,
  where: AtcPositionList,
  positions: readonly AtcPositionDto[],
  index: number,
): Record<string, unknown> {
  const rows = rowsOf(props, where);
  const row = rows[index];
  const position = row === undefined ? undefined : positionOf(row, where, indexed(positions));
  if (row === undefined || position === undefined) {
    return props;
  }

  const copied = { ...row, [where.field]: position.callsign, ...copiedFrom(position, where.entry) };
  return { ...props, [where.list]: rows.map((each, at) => (at === index ? copied : each)) };
}

/**
 * Whether IVAO's positions are there but their frequencies not yet: the column is filled by the night's synchronisation, and
 * until it has run since the update the table's frequencies are written by hand (§5.8). The block says so.
 */
export function frequenciesPending(positions: readonly AtcPositionDto[]): boolean {
  return positions.length > 0 && positions.every((position) => (position.frequency ?? '') === '');
}
