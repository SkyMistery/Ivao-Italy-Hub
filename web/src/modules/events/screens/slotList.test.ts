import { describe, expect, test } from 'vitest';

import {
  anyFilter,
  narrowSlots,
  nextHour,
  oneDay,
  rotationLegs,
  sheetProblems,
  slotAirline,
  slotAirport,
  slotFilterChoices,
  slotSections,
  slotTime,
  type FilteredSlot,
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

/** A slot the filters read: a callsign and the types it admits besides the above. */
function flight(
  id: number,
  callsign: string,
  types: readonly string[],
  hours: readonly [number, number],
  options: { arrives?: boolean; rotation?: string } = {},
): FilteredSlot {
  return {
    ...slot(id, options.arrives ? 'XXCC' : 'XXAA', options.arrives ? 'XXAA' : 'XXCC', hours, options),
    callsign,
    aircraftTypes: types,
  };
}

describe('the filters of the slots (E6b)', () => {
  const slots = [
    flight(1, 'XYZ101', ['XA20', 'XA21'], [17, 18], { rotation: 'R1' }),
    flight(2, 'XYZ102', ['XA20'], [18.5, 19.5], { arrives: true, rotation: 'R1' }),
    flight(3, 'ABC7', ['XB30'], [19, 20]),
    flight(4, '9XY', ['XA21'], [21, 22], { arrives: true }),
  ];

  test('the airline of a slot is the letters its callsign starts with, up to its first digit', () => {
    expect(slotAirline('XYZ101')).toBe('XYZ');
    expect(slotAirline('abc7')).toBe('ABC');
    expect(slotAirline('9XY')).toBeNull();
    expect(slotAirline('NODIGITS')).toBeNull();
  });

  test('each filter narrows, and nothing written lets everything through', () => {
    const ids = (filters: Parameters<typeof narrowSlots>[1]) =>
      narrowSlots(slots, filters).map((one) => one.id);

    expect(ids({})).toEqual([1, 2, 3, 4]);
    expect(anyFilter({})).toBe(false);
    expect(ids({ direction: 'arrivals' })).toEqual([2, 4]);
    expect(ids({ direction: 'departures' })).toEqual([1, 3]);
    // A type the slot admits, the main one or another.
    expect(ids({ type: 'XA21' })).toEqual([1, 4]);
    expect(ids({ airline: 'XYZ' })).toEqual([1, 2]);
    expect(ids({ rotation: 'R1' })).toEqual([1, 2]);
    // The hours at the airport of the event — an arrival's on block —: from 19:00, and until 20:00 (a slot at 20:00 is not shown).
    expect(ids({ from: '2026-11-21T19' })).toEqual([2, 3, 4]);
    expect(ids({ until: '2026-11-21T20' })).toEqual([1, 2, 3]);
    expect(ids({ from: '2026-11-21T19', until: '2026-11-21T20', airline: 'ABC' })).toEqual([3]);
    expect(anyFilter({ until: '2026-11-21T20' })).toBe(true);
  });

  test('the choices are what the slots hold, once each and in order', () => {
    expect(slotFilterChoices(slots)).toEqual({
      hours: ['2026-11-21T17', '2026-11-21T19', '2026-11-21T22'],
      ends: ['2026-11-21T18', '2026-11-21T20', '2026-11-21T23'],
      types: ['XA20', 'XA21', 'XB30'],
      airlines: ['ABC', 'XYZ'],
      rotations: ['R1'],
    });
    expect(nextHour('2026-11-21T23')).toBe('2026-11-22T00');
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
