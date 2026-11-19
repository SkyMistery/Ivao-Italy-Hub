import { describe, expect, test } from 'vitest';

import type { CalendarKind } from '../api/bootstrap';

import {
  busyDays,
  busyHours,
  dayKeyOfPicker,
  legendOf,
  pickerDay,
  stretchesOn,
  type BusyStretch,
} from './busy';

/**
 * What the date picker draws out of what `/api/calendar/busy` answers (note 2026-11-18-il-calendario-libero-e-occupato): the
 * UTC days and hours a stretch touches, and which of them cannot be chosen. The time each entry takes is the server's answer;
 * these only place it.
 */

const vocabulary: CalendarKind[] = [
  { key: 'event', label: { en: 'Event' }, colour: 'blue', blocks: true },
  { key: 'training', label: { en: 'Training' }, colour: 'green', blocks: false },
];

function stretch(
  startsAtUtc: string,
  endsAtUtc: string,
  kind: string,
  blocks: boolean,
  allDay = false,
): BusyStretch {
  return { startsAtUtc, endsAtUtc, allDay, kind, blocks, title: { en: kind } };
}

describe('busyDays', () => {
  test('marks every UTC day a stretch touches, and leaves the others free', () => {
    const days = busyDays(
      [stretch('2026-11-20T22:00:00Z', '2026-11-21T02:00:00Z', 'event', true)],
      vocabulary,
    );

    expect([...days.keys()]).toEqual(['2026-11-20', '2026-11-21']);
    expect(days.has('2026-11-22')).toBe(false);
  });

  test('a stretch ending at midnight does not touch the next day', () => {
    const days = busyDays(
      [stretch('2026-11-20T00:00:00Z', '2026-11-21T00:00:00Z', 'event', true, true)],
      vocabulary,
    );

    expect([...days.keys()]).toEqual(['2026-11-20']);
  });

  test('blocks a day only when blocking stretches take all of it', () => {
    const days = busyDays(
      [
        stretch('2026-11-20T00:00:00Z', '2026-11-20T12:00:00Z', 'event', true),
        stretch('2026-11-20T12:00:00Z', '2026-11-21T00:00:00Z', 'event', true),
        stretch('2026-11-21T00:00:00Z', '2026-11-22T00:00:00Z', 'training', false, true),
        stretch('2026-11-22T10:00:00Z', '2026-11-22T11:00:00Z', 'event', true),
      ],
      vocabulary,
    );

    expect(days.get('2026-11-20')?.blocked).toBe(true);
    // Taken all day by a kind that does not block: busy, and still to be chosen.
    expect(days.get('2026-11-21')?.blocked).toBe(false);
    expect(days.get('2026-11-22')?.blocked).toBe(false);
    // A blocking stretch on part of a day: no hour of it is lost to an instant, but a date without a time takes the day.
    expect(days.get('2026-11-22')?.blocking).toBe(true);
    expect(days.get('2026-11-21')?.blocking).toBe(false);
  });

  test("lists a day's kinds once each, in the vocabulary's order, the unknown ones last", () => {
    const days = busyDays(
      [
        stretch('2026-11-20T15:00:00Z', '2026-11-20T16:00:00Z', 'meeting', false),
        stretch('2026-11-20T10:00:00Z', '2026-11-20T11:00:00Z', 'training', false),
        stretch('2026-11-20T08:00:00Z', '2026-11-20T09:00:00Z', 'event', true),
        stretch('2026-11-20T18:00:00Z', '2026-11-20T19:00:00Z', 'training', false),
      ],
      vocabulary,
    );

    expect(days.get('2026-11-20')?.kinds).toEqual(['event', 'training', 'meeting']);
  });
});

describe('busyHours', () => {
  test('marks the hours a stretch touches, and switches off the ones a blocking kind takes', () => {
    const hours = busyHours(
      [
        stretch('2026-11-20T14:30:00Z', '2026-11-20T16:00:00Z', 'event', true),
        stretch('2026-11-20T09:00:00Z', '2026-11-20T10:00:00Z', 'training', false),
      ],
      '2026-11-20',
    );

    expect(hours).toHaveLength(24);
    expect(hours.filter((hour) => hour.blocked).map((hour) => hour.hour)).toEqual([14, 15]);
    expect(hours[9]?.stretches.map((taken) => taken.kind)).toEqual(['training']);
    expect(hours[9]?.blocked).toBe(false);
    expect(hours[16]?.stretches).toEqual([]);
  });

  test('reads a stretch that started the day before', () => {
    const hours = busyHours(
      [stretch('2026-11-19T22:00:00Z', '2026-11-20T02:00:00Z', 'event', true)],
      '2026-11-20',
    );

    expect(hours.filter((hour) => hour.blocked).map((hour) => hour.hour)).toEqual([0, 1]);
  });
});

describe('the rest of the picker', () => {
  test('the lines of a day are its stretches by their start', () => {
    const lines = stretchesOn(
      [
        stretch('2026-11-20T15:00:00Z', '2026-11-20T16:00:00Z', 'training', false),
        stretch('2026-11-20T08:00:00Z', '2026-11-20T09:00:00Z', 'event', true),
        stretch('2026-11-21T08:00:00Z', '2026-11-21T09:00:00Z', 'event', true),
      ],
      '2026-11-20',
    );

    expect(lines.map((line) => line.startsAtUtc)).toEqual(['2026-11-20T08:00:00Z', '2026-11-20T15:00:00Z']);
  });

  test('the legend names each kind of the month once, its colour and whether it blocks', () => {
    expect(
      legendOf(
        [
          stretch('2026-11-20T15:00:00Z', '2026-11-20T16:00:00Z', 'training', false),
          stretch('2026-11-20T08:00:00Z', '2026-11-20T09:00:00Z', 'event', true),
          stretch('2026-11-21T08:00:00Z', '2026-11-21T09:00:00Z', 'meeting', false),
        ],
        vocabulary,
      ),
    ).toEqual([
      { kind: 'event', colour: 'blue', blocks: true },
      { kind: 'training', colour: 'green', blocks: false },
      { kind: 'meeting', colour: 'gray', blocks: false },
    ]);
  });

  test('a UTC day goes to the picker and comes back the same, whatever the browser zone', () => {
    expect(dayKeyOfPicker(pickerDay('2026-03-29'))).toBe('2026-03-29');
    expect(dayKeyOfPicker(pickerDay('2026-10-25'))).toBe('2026-10-25');
  });
});
