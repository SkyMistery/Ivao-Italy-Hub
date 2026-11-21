import { afterEach, describe, expect, test } from 'vitest';

import { parseMoment, writeMoment } from './parseMoment';
import { DEFAULT_DATE_FORMAT, setDateFormat } from './useMoment';

/**
 * A date written by hand is read back in the division's order (note 2026-11-18-il-calendario-libero-e-occupato, the question
 * note 2026-10-31-il-formato-della-data §3.2 left open). The eleventh of March tells the first two orders apart.
 */

afterEach(() => {
  setDateFormat(DEFAULT_DATE_FORMAT);
});

describe('parseMoment', () => {
  test.each([
    ['dd/mm/yyyy', '11/03/2026 19:05'],
    ['mm/dd/yyyy', '03/11/2026 19:05'],
    ['yyyy/mm/dd', '2026/03/11 19:05'],
  ])('reads %s, the order the division chose', (format, written) => {
    setDateFormat(format);

    expect(parseMoment(written, true)).toBe('2026-03-11T19:05:00Z');
  });

  test('reads back what the field writes, in every order', () => {
    for (const format of ['dd/mm/yyyy', 'mm/dd/yyyy', 'yyyy/mm/dd']) {
      setDateFormat(format);
      expect(parseMoment(writeMoment('2026-12-01T07:30:00Z', true), true)).toBe('2026-12-01T07:30:00Z');
      expect(parseMoment(writeMoment('2026-12-01T00:00:00Z', false), false)).toBe('2026-12-01T00:00:00Z');
    }
  });

  test('takes the separators a keyboard gives, and single digits', () => {
    expect(parseMoment(' 1-3-2026 9.05 ', true)).toBe('2026-03-01T09:05:00Z');
    expect(parseMoment('1.3.2026', false)).toBe('2026-03-01T00:00:00Z');
  });

  test('is nothing for nothing written', () => {
    expect(parseMoment('   ', true)).toBeUndefined();
  });

  test.each([
    ['31/11/2026 10:00', 'a day the month does not have'],
    ['11/13/2026 10:00', 'a thirteenth month'],
    ['11/03/2026 24:00', 'an hour after the last'],
    ['11/03/2026 10:60', 'a minute after the last'],
    ['11/03/26 10:00', 'a year of two digits'],
    ['tomorrow', 'words'],
    ['11/03/2026', 'a day without the time an instant needs'],
  ])('refuses %s: %s', (written) => {
    expect(parseMoment(written, true)).toBeNull();
  });

  test('refuses a time on a field that holds a day', () => {
    expect(parseMoment('11/03/2026 10:00', false)).toBeNull();
  });
});
