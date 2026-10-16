import { expect, test } from 'vitest';

import { attendanceSchema, decidableAttendances, unflownExcuseSchema } from './schemas';
import { isLimitKey, isLimitRefusal } from './screens/limits';
import { ApiError } from '../../shared/api/problem';

/**
 * The forms of the registers (M4, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 1, 6 and 8): the outcomes a shift is still
 * offered, the form of «Decide» with them, and the refusals of a booking that the record explains. The rules are the server's: the form offers
 * what it would accept.
 */

test('a shift is offered the outcomes still open to it', () => {
  expect(decidableAttendances('Unknown')).toEqual(['NoShow', 'Excused', 'Attended']);
  expect(decidableAttendances('NoShowProposed')).toEqual(['NoShow', 'Excused', 'Attended']);
  expect(decidableAttendances('NoShow')).toEqual(['Excused']);
  expect(decidableAttendances('Attended')).toEqual([]);
  expect(decidableAttendances('Excused')).toEqual([]);
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
