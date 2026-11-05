import { afterEach, describe, expect, test } from 'vitest';

import { DEFAULT_DATE_FORMAT, formatMoment, setDateFormat } from './useMoment';

/**
 * The day is written in the division's order, whatever the language would have chosen (note 2026-10-31-il-formato-della-data,
 * from the plan of 22 October, §4.E): `dd/mm/yyyy` when `division.json` says nothing, `mm/dd/yyyy` or `yyyy/mm/dd` when it
 * says so. Only a date with its month in words keeps the language's order. The eleventh of March is the day that tells the
 * first two apart.
 */

const ELEVENTH_OF_MARCH = '2026-03-11T19:05:00Z';

afterEach(() => {
  setDateFormat(DEFAULT_DATE_FORMAT);
});

describe('formatMoment', () => {
  test('writes the day first by default, in English as in Italian', () => {
    expect(formatMoment('en', ELEVENTH_OF_MARCH)).toBe('11/03/2026 19:05');
    expect(formatMoment('it', ELEVENTH_OF_MARCH)).toBe('11/03/2026 19:05');
    expect(formatMoment('en', ELEVENTH_OF_MARCH, { time: false })).toBe('11/03/2026');
    expect(formatMoment('en', ELEVENTH_OF_MARCH, { dateStyle: 'short' })).toBe('11/03/2026 19:05');
  });

  test.each([
    ['mm/dd/yyyy', '03/11/2026 19:05'],
    ['yyyy/mm/dd', '2026/03/11 19:05'],
    ['dd/mm/yyyy', '11/03/2026 19:05'],
  ])('writes %s when the division chose it', (format, written) => {
    setDateFormat(format);

    expect(formatMoment('it', ELEVENTH_OF_MARCH)).toBe(written);
  });

  test('takes the day where it is asked for: 23:30 in UTC is already the next day in Rome', () => {
    expect(formatMoment('en', '2026-03-11T23:30:00Z', { timeZone: 'Europe/Rome' })).toBe('12/03/2026 00:30');
  });

  test('keeps the language for a date with its month in words', () => {
    setDateFormat('mm/dd/yyyy');

    expect(formatMoment('it', ELEVENTH_OF_MARCH, { dateStyle: 'long', time: false })).toBe('11 marzo 2026');
    expect(formatMoment('en', ELEVENTH_OF_MARCH, { dateStyle: 'long', time: false })).toBe('March 11, 2026');
  });

  test('leaves the time alone when the date is not asked for', () => {
    setDateFormat('yyyy/mm/dd');

    expect(formatMoment('en', ELEVENTH_OF_MARCH, { date: false })).toBe('19:05');
  });

  test('falls back to the default for a format it does not know', () => {
    setDateFormat('dd-mm-yyyy');

    expect(formatMoment('en', ELEVENTH_OF_MARCH)).toBe('11/03/2026 19:05');
  });
});
