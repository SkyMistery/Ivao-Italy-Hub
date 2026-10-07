import { describe, expect, test } from 'vitest';

import {
  oneDay,
  rotationLegs,
  sheetProblems,
  slotAirport,
  slotSections,
  slotTime,
  type PublicSlot,
} from './slotList';

/** A slot of the page: from and to, its two hours from the start of a day — the next day past 23 —, a departure unless it arrives. */
function slot(
  id: number,
  from: string,
  to: string,
  hours: readonly [number, number],
  options: { arrives?: boolean; rotation?: string; leg?: number } = {},
): PublicSlot {
  const at = (h: number) => new Date(Date.UTC(2026, 10, 21) + h * 3_600_000).toISOString();

  return {
    id,
    departure: { icao: from },
    arrival: { icao: to },
    offBlockUtc: at(hours[0]),
    onBlockUtc: at(hours[1]),
    isArrival: options.arrives ?? false,
    rotation: options.rotation ?? null,
    leg: options.leg ?? null,
  };
}

describe('the slots of the page of an event', () => {
  test('a slot is at the airport of the event it leaves or lands at, and its time there is that one', () => {
    const out = slot(1, 'XXAA', 'XXCC', [8, 9]);
    const back = slot(2, 'XXCC', 'XXAA', [10, 11], { arrives: true });

    expect([slotAirport(out), slotTime(out)]).toEqual(['XXAA', out.offBlockUtc]);
    expect([slotAirport(back), slotTime(back)]).toEqual(['XXAA', back.onBlockUtc]);
  });

  test('one section per airport in the order of the event, the departures and the arrivals apart, each by its time there', () => {
    // As the server sends them: by off block.
    const sections = slotSections(
      [
        slot(6, 'XXZZ', 'XXCC', [6, 7]),
        slot(1, 'XXCC', 'XXBB', [7, 12], { arrives: true }),
        slot(2, 'XXAA', 'XXCC', [8, 9], { rotation: 'R1', leg: 1 }),
        slot(8, 'XXAA', 'XXDD', [8, 10]),
        slot(3, 'XXDD', 'XXAA', [8, 11], { arrives: true }),
        slot(4, 'XXBB', 'XXCC', [9, 10]),
        slot(5, 'XXCC', 'XXAA', [9, 10], { arrives: true, rotation: 'R1', leg: 2 }),
        // Between two airports of the event: a departure of the first, never an arrival of the second.
        slot(7, 'XXAA', 'XXBB', [10, 11]),
      ],
      ['XXAA', 'XXBB'],
    );

    expect(
      sections.map((section) => [
        section.icao,
        section.departures.map((one) => one.id),
        section.arrivals.map((one) => one.id),
      ]),
    ).toEqual([
      // The arrival landing at 10 before the one landing at 11, though it took off later; two departures at 8 as sent.
      ['XXAA', [2, 8, 7], [5, 3]],
      ['XXBB', [4], [1]],
      // An airport the event no longer lists comes after its own.
      ['XXZZ', [6], []],
    ]);
  });

  test('no slots, no sections', () => {
    expect(slotSections([], ['XXAA'])).toEqual([]);
  });

  test('the legs of a rotation by their places, and only its own', () => {
    const legs = rotationLegs(
      [
        slot(5, 'XXCC', 'XXAA', [9, 10], { arrives: true, rotation: 'R1', leg: 2 }),
        slot(9, 'XXAA', 'XXCC', [8, 9], { rotation: 'R2', leg: 1 }),
        slot(2, 'XXAA', 'XXCC', [6, 7], { rotation: 'R1', leg: 1 }),
      ],
      'R1',
    );

    expect(legs.map((leg) => leg.id)).toEqual([2, 5]);
  });

  test('the day is said once when every time falls on it', () => {
    expect(oneDay([slot(1, 'XXAA', 'XXCC', [8, 9]), slot(2, 'XXAA', 'XXCC', [20, 21])])).toBe(true);
    // An on block past midnight is another day.
    expect(oneDay([slot(1, 'XXAA', 'XXCC', [8, 9]), slot(2, 'XXAA', 'XXCC', [23, 24])])).toBe(false);
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
