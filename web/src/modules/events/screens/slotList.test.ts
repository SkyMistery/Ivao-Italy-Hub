import { describe, expect, test } from 'vitest';

import { oneDay, sheetProblems, slotGroups, type PublicSlot } from './slotList';

/** A slot of the page, by its off block hour from the start of a day; an on block an hour later — the next day past 23. */
function slot(
  id: number,
  hour: number,
  rotation: string | null = null,
  leg: number | null = null,
): PublicSlot {
  const at = (h: number) => new Date(Date.UTC(2026, 10, 21) + h * 3_600_000).toISOString();

  return { id, offBlockUtc: at(hour), onBlockUtc: at(hour + 1), rotation, leg };
}

describe('the slots of the page of an event', () => {
  test('a rotation is grouped where its first leg falls, its legs by their places, a slot alone where it falls', () => {
    // As the server sends them: by off block.
    const groups = slotGroups([
      slot(1, 8),
      slot(2, 9, 'R1', 1),
      slot(3, 10),
      slot(4, 11, 'R2', 1),
      slot(5, 12, 'R1', 3),
      slot(6, 12, 'R1', 2),
      slot(7, 14, 'R2', 2),
    ]);

    expect(groups.map((group) => [group.rotation, group.slots.map((one) => one.id)])).toEqual([
      [null, [1]],
      ['R1', [2, 6, 5]],
      [null, [3]],
      ['R2', [4, 7]],
    ]);
  });

  test('no slots, no groups', () => {
    expect(slotGroups([])).toEqual([]);
  });

  test('the day is said once when every time falls on it', () => {
    expect(oneDay([slot(1, 8), slot(2, 20)])).toBe(true);
    // An on block past midnight is another day.
    expect(oneDay([slot(1, 8), slot(2, 23)])).toBe(false);
    expect(oneDay([])).toBe(true);
  });
});

describe('what the server refused of a table', () => {
  test('the whole table first, then row by row, and in a row by the order of the columns', () => {
    const problems = sheetProblems({
      'rows[12].on_block_utc': ['events:errors.onBlockBeforeOffBlock'],
      'rows[3].aircraft_types': ['events:errors.aircraftUnknown'],
      'rows[12].callsign': ['events:errors.slotTwice', 'events:errors.callsignFormat'],
      text: ['events:errors.noPublicSlots'],
      'rows[12].notes': ['events:errors.sheetColumnMissing'],
    });

    expect(problems.map((problem) => [problem.row, problem.column])).toEqual([
      [null, null],
      [3, 'aircraft_types'],
      [12, 'callsign'],
      [12, 'on_block_utc'],
      [12, 'notes'],
    ]);
    expect(problems[2]?.keys).toEqual(['events:errors.slotTwice', 'events:errors.callsignFormat']);
  });
});
