import { describe, expect, test } from 'vitest';

import { bookingsOpen, countdown, dayIn, onTheEventsDay, slotOpen } from './bookingTimes';

/**
 * The moments of the bookings of an event (E6b): how long until they open, to the second; when they are open and a slot can still
 * be booked; and the event's day where the division lives, when its page shows who is online at its airports.
 */

const at = (instant: string) => Date.parse(instant);

describe('the countdown to the opening of the bookings', () => {
  test('days, then hours, minutes and seconds', () => {
    expect(countdown(at('2099-11-20T10:00:00Z'), at('2099-11-22T13:04:05Z'))).toEqual({
      days: 2,
      time: '03:04:05',
    });
    expect(countdown(at('2099-11-20T10:00:00Z'), at('2099-11-20T10:00:59.900Z'))).toEqual({
      days: 0,
      time: '00:00:59',
    });
  });

  test('nothing once the moment has come', () => {
    expect(countdown(at('2099-11-20T10:00:00Z'), at('2099-11-20T10:00:00Z'))).toBeNull();
    expect(countdown(at('2099-11-20T10:00:00Z'), at('2099-11-20T09:00:00Z'))).toBeNull();
  });
});

describe('what can be booked', () => {
  const event = { bookingOpensAtUtc: '2099-11-20T18:00:00Z', cancelledAt: null };

  test('from the opening of the bookings, never on a cancelled event nor on one without slots', () => {
    expect(bookingsOpen(event, at('2099-11-20T17:59:59Z'))).toBe(false);
    expect(bookingsOpen(event, at('2099-11-20T18:00:00Z'))).toBe(true);
    expect(bookingsOpen({ ...event, cancelledAt: '2099-11-19T09:00:00Z' }, at('2099-11-21T10:00:00Z'))).toBe(
      false,
    );
    expect(bookingsOpen({ bookingOpensAtUtc: null, cancelledAt: null }, at('2099-11-21T10:00:00Z'))).toBe(
      false,
    );
  });

  test('a slot until its off block, which is already closed', () => {
    const slot = { offBlockUtc: '2099-11-21T18:00:00Z' };
    expect(slotOpen(slot, at('2099-11-21T17:59:59Z'))).toBe(true);
    expect(slotOpen(slot, at('2099-11-21T18:00:00Z'))).toBe(false);
  });
});

describe("the event's day, where the division lives", () => {
  test('the day of an instant in a zone', () => {
    expect(dayIn(new Date('2099-11-21T23:30:00Z'), 'UTC')).toBe('2099-11-21');
    expect(dayIn(new Date('2099-11-21T23:30:00Z'), 'Europe/Rome')).toBe('2099-11-22');
  });

  test('an evening is its day, from midnight to midnight', () => {
    const starts = '2099-11-21T18:00:00Z';
    const ends = '2099-11-21T22:00:00Z';

    expect(onTheEventsDay(starts, ends, new Date('2099-11-20T22:59:00Z'), 'Europe/Rome')).toBe(false);
    expect(onTheEventsDay(starts, ends, new Date('2099-11-20T23:00:00Z'), 'Europe/Rome')).toBe(true);
    expect(onTheEventsDay(starts, ends, new Date('2099-11-21T22:30:00Z'), 'Europe/Rome')).toBe(true);
    expect(onTheEventsDay(starts, ends, new Date('2099-11-21T23:00:00Z'), 'Europe/Rome')).toBe(false);
  });

  test('a night across midnight is two days, and an event that ends at midnight ends on the day before', () => {
    expect(
      onTheEventsDay('2099-11-21T21:00:00Z', '2099-11-22T02:00:00Z', new Date('2099-11-22T12:00:00Z'), 'UTC'),
    ).toBe(true);
    expect(
      onTheEventsDay('2099-11-21T20:00:00Z', '2099-11-22T00:00:00Z', new Date('2099-11-22T00:30:00Z'), 'UTC'),
    ).toBe(false);
  });
});
