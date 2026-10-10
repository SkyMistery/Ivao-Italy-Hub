import { describe, expect, test } from 'vitest';

import { bookingsByEvent, splitBookings, type OwnBooking } from './myBookings';

/**
 * A pilot's own bookings as `/events/mine` and the block `events.myEvents` draw them (E6b): still to fly — one in the air too — by
 * their off block, the past ones the latest first, and both under their events.
 */

function booking(id: number, eventId: number, offBlock: string, onBlock: string): OwnBooking {
  return {
    id,
    eventId,
    eventSlug: `evt-test-smoke-${eventId}`,
    eventTitle: { en: `Event ${eventId}` },
    eventState: 'BookingOpen',
    offBlockUtc: offBlock,
    onBlockUtc: onBlock,
  };
}

const now = Date.parse('2099-11-21T12:00:00Z');

describe("a pilot's own bookings", () => {
  test('still to fly while the on block is to come, by off block; flown the latest first', () => {
    const inTheAir = booking(1, 10, '2099-11-21T11:00:00Z', '2099-11-21T13:00:00Z');
    const later = booking(2, 10, '2099-11-21T18:00:00Z', '2099-11-21T19:00:00Z');
    const landed = booking(3, 11, '2099-11-20T18:00:00Z', '2099-11-20T19:00:00Z');
    const longAgo = booking(4, 12, '2099-10-01T18:00:00Z', '2099-10-01T19:00:00Z');

    const { upcoming, past } = splitBookings([later, longAgo, inTheAir, landed], now);

    expect(upcoming.map((row) => row.id)).toEqual([1, 2]);
    expect(past.map((row) => row.id)).toEqual([3, 4]);
  });

  test('under their events, each once, in the order its first booking comes', () => {
    const groups = bookingsByEvent([
      booking(1, 10, '2099-11-21T18:00:00Z', '2099-11-21T19:00:00Z'),
      booking(2, 11, '2099-11-21T19:00:00Z', '2099-11-21T20:00:00Z'),
      booking(3, 10, '2099-11-21T21:00:00Z', '2099-11-21T22:00:00Z'),
    ]);

    expect(groups.map((group) => [group.eventId, group.slug, group.bookings.map((row) => row.id)])).toEqual([
      [10, 'evt-test-smoke-10', [1, 3]],
      [11, 'evt-test-smoke-11', [2]],
    ]);
  });
});
