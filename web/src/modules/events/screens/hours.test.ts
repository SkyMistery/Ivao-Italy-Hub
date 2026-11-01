import { expect, test } from 'vitest';

import { formatMoment } from '../../../shared/i18n/useMoment';

import { endOfHours } from './hours';

/** The end of some hours after their start (#333, item 6): a time on the same day, the date with it past midnight. */

const moment = (value: unknown, options?: Parameters<typeof formatMoment>[2]) =>
  formatMoment('en', value, options);

test('hours that end on the day they start end with a time', () => {
  expect(endOfHours('2099-11-21T17:00:00Z', '2099-11-21T18:30:00Z', moment)).toBe(
    moment('2099-11-21T18:30:00Z', { date: false }),
  );
  expect(endOfHours('2099-11-21T17:00:00Z', '2099-11-21T18:30:00Z', moment)).toBe('18:30');
});

test('hours that end past midnight end with their date', () => {
  const end = endOfHours('2099-11-21T23:00:00Z', '2099-11-22T02:45:00Z', moment);

  expect(end).toBe(moment('2099-11-22T02:45:00Z'));
  expect(end).toContain('22');
  expect(end).toContain('02:45');
});
