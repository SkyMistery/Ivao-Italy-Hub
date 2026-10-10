import { describe, expect, test } from 'vitest';

import {
  bookableIn,
  linkableDepartures,
  privateOneDay,
  privateRequest,
  privateSections,
  type PrivateSlot,
} from './privateList';

/**
 * The private slots of an event as data (E7; design M4 §3.4, §7.1): by airport, direction and hour of the event, the slots a pilot may
 * still book in an hour, the departures an arrival may link, and what the dialog sends. Times on an invented day.
 */

const at = (hour: number, minute = 0) =>
  `2099-11-21T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00.000Z`;

function slot(id: number, airportIcao: string, isArrival: boolean, time: string, taken = false): PrivateSlot {
  return { id, airportIcao, isArrival, timeUtc: time, taken };
}

describe('the private slots of an event as data', () => {
  test('by airport in the order of the event, departures and arrivals apart, hour by hour from the start of the event', () => {
    const sections = privateSections(
      [
        slot(5, 'XXBB', false, at(18, 10)),
        slot(1, 'XXAA', true, at(17, 45)),
        slot(2, 'XXAA', true, at(17, 30)),
        slot(3, 'XXAA', true, at(18, 30), true),
        slot(4, 'XXAA', false, at(17, 30)),
        slot(6, 'XXZZ', true, at(17, 40)),
      ],
      ['XXAA', 'XXBB'],
      at(17, 30),
      at(19),
    );

    // The event's airports first, in its order; one it no longer lists after them.
    expect(sections.map((section) => section.icao)).toEqual(['XXAA', 'XXBB', 'XXZZ']);

    // Hours from the start, 17:30, the last one cut at the end, 19:00; each hour's slots by their time.
    const [first] = sections;
    expect(first?.arrivals).toEqual([
      {
        fromUtc: at(17, 30),
        toUtc: at(18, 30),
        slots: [slot(2, 'XXAA', true, at(17, 30)), slot(1, 'XXAA', true, at(17, 45))],
      },
      { fromUtc: at(18, 30), toUtc: at(19), slots: [slot(3, 'XXAA', true, at(18, 30), true)] },
    ]);
    expect(first?.departures.map((hour) => hour.slots.map((one) => one.id))).toEqual([[4]]);
    expect(sections[1]?.arrivals).toEqual([]);
  });

  test('an hour offers the free slots still to come', () => {
    const hour = {
      fromUtc: at(17),
      toUtc: at(18),
      slots: [
        slot(1, 'XXAA', true, at(17)),
        slot(2, 'XXAA', true, at(17, 20), true),
        slot(3, 'XXAA', true, at(17, 40)),
      ],
    };

    expect(bookableIn(hour, Date.parse(at(16))).map((one) => one.id)).toEqual([1, 3]);
    // The time itself has passed: closed.
    expect(bookableIn(hour, Date.parse(at(17))).map((one) => one.id)).toEqual([3]);
  });

  test('an arrival links a free departure from its own airport, leaving after its hour has begun', () => {
    const slots = [
      slot(1, 'XXAA', false, at(18, 30)),
      slot(2, 'XXAA', false, at(16, 50)),
      slot(3, 'XXAA', false, at(18), true),
      slot(4, 'XXBB', false, at(18)),
      slot(5, 'XXAA', true, at(18)),
      slot(6, 'XXAA', false, at(17, 10)),
    ];

    expect(linkableDepartures(slots, 'XXAA', at(17)).map((one) => one.id)).toEqual([6, 1]);
  });

  test('the day is said once when every private slot falls on it', () => {
    expect(privateOneDay([slot(1, 'XXAA', true, at(17)), slot(2, 'XXAA', false, at(23, 50))])).toBe(true);
    expect(
      privateOneDay([slot(1, 'XXAA', true, at(17)), slot(2, 'XXAA', false, '2099-11-22T00:10:00.000Z')]),
    ).toBe(false);
  });

  test('the dialog sends the slot, the aircraft and the flight in capitals, and the linked departure when there is one', () => {
    expect(
      privateRequest({
        slotId: '41',
        callsign: ' xsm101 ',
        aircraftIcao: 'xa20',
        otherIcao: ' xxbb',
        otherTimeUtc: at(16),
      }),
    ).toEqual({
      slotId: 41,
      aircraftIcao: 'XA20',
      callsign: 'XSM101',
      otherIcao: 'XXBB',
      otherTimeUtc: at(16),
      departure: null,
    });

    // A time not written is none: the server says it is required.
    expect(
      privateRequest({
        slotId: '41',
        callsign: 'XSM101',
        aircraftIcao: 'XA20',
        otherIcao: 'XXBB',
        departure: { slotId: '52', callsign: 'xsm102', otherIcao: 'xxcc' },
      }),
    ).toEqual({
      slotId: 41,
      aircraftIcao: 'XA20',
      callsign: 'XSM101',
      otherIcao: 'XXBB',
      otherTimeUtc: null,
      departure: { slotId: 52, callsign: 'XSM102', otherIcao: 'XXCC', otherTimeUtc: null },
    });
  });
});
