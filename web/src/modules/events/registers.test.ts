import { expect, test } from 'vitest';

import { attendanceSchema, decidableAttendances, unflownExcuseSchema } from './schemas';
import { isLimitKey, isLimitRefusal } from './screens/limits';
import { registerHref, registerTitle } from './screens/registerPeople';
import { ApiError } from '../../shared/api/problem';
import { createTestI18n } from '../../test/harness';

/**
 * The forms of the registers (M4, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 1, 6 and 8): the outcomes a shift is still
 * offered, the form of «Decide» with them, and the refusals of a booking that the record explains. The rules are the server's: the form offers
 * what it would accept.
 */

test('a shift is offered every outcome but the one it has: none is final', () => {
  expect(decidableAttendances('Unknown')).toEqual(['NoShow', 'Excused', 'Attended']);
  expect(decidableAttendances('NoShowProposed')).toEqual(['NoShow', 'Excused', 'Attended']);
  // The no-show taken away first.
  expect(decidableAttendances('NoShow')).toEqual(['Excused', 'Attended']);
  expect(decidableAttendances('Attended')).toEqual(['NoShow', 'Excused']);
  expect(decidableAttendances('Excused')).toEqual(['NoShow', 'Attended']);
});

test('the form of «Decide» takes only the outcomes it offers, and a note the server asks to excuse', () => {
  const takeAway = attendanceSchema(['Excused']);

  expect(takeAway.safeParse({ attendance: 'Excused', note: 'the network fell' }).success).toBe(true);
  expect(takeAway.safeParse({ attendance: 'NoShow', note: '' }).success).toBe(false);
  expect(
    attendanceSchema(decidableAttendances('NoShowProposed')).safeParse({ attendance: 'NoShow', note: '' })
      .success,
  ).toBe(true);
  expect(unflownExcuseSchema.safeParse({ note: 'the hub was down' }).success).toBe(true);
});

test('a refusal for the limits is told apart from any other', () => {
  expect(isLimitKey('events:errors.bookingRestrictedWindow')).toBe(true);
  expect(isLimitKey('events:errors.bookingIncompatible')).toBe(false);

  const limited = new ApiError(400, {
    title: 'refused',
    status: 400,
    errors: { slotId: ['events:errors.bookingRestrictedEvent'] },
  });
  const other = new ApiError(400, {
    title: 'refused',
    status: 400,
    errors: { slotId: ['events:errors.slotJustTaken'] },
  });

  expect(isLimitRefusal(limited)).toBe(true);
  expect(isLimitRefusal(other)).toBe(false);
  expect(isLimitRefusal(new Error('network'))).toBe(false);
});

test('a person whose data was erased has no page to open in a register, and their page is never titled with the pseudonym', () => {
  const t = createTestI18n().t;

  // #333, item 2: their rows of the events that are over stay, under the pseudonym.
  expect(registerHref('/staff/events/pilots', 761380)).toBe('/staff/events/pilots/761380');
  expect(registerHref('/staff/events/controllers', -17)).toBeNull();

  expect(registerTitle(761380, { vid: 761380, name: 'Smoke Pilot' }, t)).toBe('Smoke Pilot (761380)');
  // Before the register answers, or when it has no row of theirs.
  expect(registerTitle(761380, undefined, t)).toBe('761380');
  expect(registerTitle(-17, undefined, t)).toBe(t('people.deleted'));
  expect(registerTitle(-17, { vid: -17, name: null }, t)).toBe(t('people.deleted'));
});
