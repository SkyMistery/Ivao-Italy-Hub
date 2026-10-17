import { expect, test } from 'vitest';

import { readFields } from '../../shared/forms';

import { bookingOutcome } from './api';
import englishEvents from './locales/en/events.json';
import italianEvents from './locales/it/events.json';
import { statsSchema, statsToFormValues } from './schemas';

/**
 * The tab «Statistics» (E13a): what the list says of a booking after the check, the numbers the form draws — the bookings still to check
 * worked out from the others —, and a word in both languages for every number and every outcome.
 */

const numbers = {
  publicSlots: 4,
  privateSlots: 1,
  booked: 5,
  bookedFlown: 3,
  bookedUnflown: 1,
  flownUnbooked: 5,
  atcAvailabilities: 2,
  atcShifts: 2,
  atcAttended: 1,
  atcNoShows: 1,
  atcExcused: 0,
  registrations: 0,
  activitySeats: 0,
};

test('a booking is flown, looked for and not found, or still to check', () => {
  expect(bookingOutcome({ flownAt: '2001-01-01T17:27:44Z', flownCheckedAt: '2001-01-02T00:00:00Z' })).toBe(
    'flown',
  );
  expect(bookingOutcome({ flownAt: null, flownCheckedAt: '2001-01-02T00:00:00Z' })).toBe('notFlown');
  expect(bookingOutcome({ flownAt: null, flownCheckedAt: null })).toBe('toCheck');
});

test('the bookings still to check are the ones neither flown nor not flown', () => {
  expect(statsToFormValues(numbers).bookedToCheck).toBe(1);
  expect(statsToFormValues({ ...numbers, bookedUnflown: 2 }).bookedToCheck).toBe(0);
});

test('«not counted» is an empty field, never a 0', () => {
  expect(statsToFormValues(numbers).flownUnbooked).toBe(5);
  expect(statsToFormValues({ ...numbers, flownUnbooked: 0 }).flownUnbooked).toBe(0);
  expect('flownUnbooked' in statsToFormValues({ ...numbers, flownUnbooked: null })).toBe(false);
});

test('every number and every outcome has its word in both languages', () => {
  const fields = readFields(statsSchema).map((field) => field.path);

  for (const words of [englishEvents.stats, italianEvents.stats]) {
    expect(Object.keys(words.fields).sort()).toEqual([...fields].sort());
    expect(Object.keys(words.bookings.options.outcome).sort()).toEqual(['flown', 'notFlown', 'toCheck']);
    expect(Object.keys(words.shifts.options.attendance).sort()).toEqual(
      ['Attended', 'Excused', 'NoShow', 'NoShowProposed', 'Unknown'].sort(),
    );
  }
});
