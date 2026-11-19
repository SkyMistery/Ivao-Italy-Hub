import { describe, expect, test } from 'vitest';

import { formatMoment, intervalEnd, intervalText, onSameDay } from './useMoment';

/**
 * An interval after its start (note 2026-11-18-la-fine-di-un-intervallo-nel-nucleo): its end a time on the same day, the date with
 * it past midnight, the days read in the zone asked; the whole line with a spaced dash. The cases of the events' `endOfHours` (#333,
 * item 6) and of the tours' `sessionEnd` (#334, item 4), which this replaces, are here as they were; the events' and the training's
 * tests of their lines call `intervalText` where they are.
 */

const moment = (value: unknown, options?: Parameters<typeof formatMoment>[2]) =>
  formatMoment('en', value, options);

describe('intervalEnd', () => {
  test('an interval that ends on the day it starts ends with a time', () => {
    expect(intervalEnd('2099-11-21T17:00:00Z', '2099-11-21T18:30:00Z', moment)).toBe(
      moment('2099-11-21T18:30:00Z', { date: false }),
    );
    expect(intervalEnd('2099-11-21T17:00:00Z', '2099-11-21T18:30:00Z', moment)).toBe('18:30');
  });

  test('an interval that ends past midnight ends with its date', () => {
    const end = intervalEnd('2099-11-21T23:00:00Z', '2099-11-22T02:45:00Z', moment);

    expect(end).toBe(moment('2099-11-22T02:45:00Z'));
    expect(end).toContain('22');
    expect(end).toContain('02:45');
  });

  test('the days are read in the zone asked: one UTC day may be two where the division lives, and two may be one', () => {
    // 21:00–22:30 UTC is one day in UTC, and 22:00–23:30 in Rome on 21 November: one day there too.
    expect(intervalEnd('2099-11-21T21:00:00Z', '2099-11-21T22:30:00Z', moment, 'Europe/Rome')).toBe('23:30');
    // 22:30–23:30 UTC is one day in UTC, and 23:30–00:30 in Rome: the end has its date there.
    expect(intervalEnd('2099-11-21T22:30:00Z', '2099-11-21T23:30:00Z', moment, 'Europe/Rome')).toBe(
      moment('2099-11-21T23:30:00Z', { timeZone: 'Europe/Rome' }),
    );
    // 23:00 UTC to 01:00 UTC the next day is two days in UTC, and 00:00–02:00 of the next day in Rome: one.
    expect(intervalEnd('2099-11-21T23:00:00Z', '2099-11-22T01:00:00Z', moment)).toBe(
      moment('2099-11-22T01:00:00Z'),
    );
    expect(intervalEnd('2099-11-21T23:00:00Z', '2099-11-22T01:00:00Z', moment, 'Europe/Rome')).toBe('02:00');
  });
});

test('onSameDay compares the days in the zone asked, UTC when none', () => {
  expect(onSameDay('2099-11-21T23:00:00Z', '2099-11-22T01:00:00Z', moment)).toBe(false);
  expect(onSameDay('2099-11-21T23:00:00Z', '2099-11-22T01:00:00Z', moment, 'Europe/Rome')).toBe(true);
  expect(onSameDay('2099-11-21T17:00:00Z', '2099-11-21T18:30:00Z', moment)).toBe(true);
});

describe('intervalText', () => {
  test('the dash is spaced on one day as on two (Carmine on #542)', () => {
    expect(intervalText('2099-11-21T17:00:00Z', '2099-11-21T22:00:00Z', moment)).toBe(
      `${moment('2099-11-21T17:00:00Z')} – 22:00`,
    );
    expect(intervalText('2099-11-21T23:00:00Z', '2099-11-22T01:00:00Z', moment)).toBe(
      `${moment('2099-11-21T23:00:00Z')} – ${moment('2099-11-22T01:00:00Z')}`,
    );
  });

  test('the start is read in the zone asked, and without an end the line is its start', () => {
    expect(intervalText('2099-11-21T23:00:00Z', '2099-11-22T01:00:00Z', moment, 'Europe/Rome')).toBe(
      `${moment('2099-11-21T23:00:00Z', { timeZone: 'Europe/Rome' })} – 02:00`,
    );
    expect(intervalText('2099-11-21T17:00:00Z', null, moment)).toBe(moment('2099-11-21T17:00:00Z'));
  });
});
