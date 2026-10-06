import { describe, expect, test } from 'vitest';

import {
  calendarItems,
  cardAirports,
  cardKinds,
  eventHref,
  narrowCards,
  spanText,
  type EventCard,
} from './cards';

/**
 * What `/events` asks of the cards the block answers (E4): narrowed to a kind and an airport — an event of the whole division names
 * none —, the choices of the two filters out of what the cards hold, the same events as entries of the calendar without the
 * cancelled ones, and when an event runs as one line.
 */

function card(id: number, overrides: Partial<EventCard> = {}): EventCard {
  return {
    id,
    slug: `evt-test-card-${id}`,
    kind: 'rfo',
    title: { en: `Event ${id}`, it: `Evento ${id}` },
    summary: { en: 'A summary', it: 'Un riassunto' },
    bannerMediaId: null,
    state: 'Announced',
    startsAtUtc: '2026-11-21T17:00:00.000Z',
    endsAtUtc: '2026-11-21T22:00:00.000Z',
    wholeDivision: false,
    airports: ['XEA1'],
    ...overrides,
  };
}

const rome = card(1, { airports: ['XEA1', 'XEA2'] });
const milan = card(2, { kind: 'rfe', airports: ['XEA3'] });
const everywhere = card(3, { kind: 'online-day', wholeDivision: true, airports: [] });
const cancelled = card(4, { state: 'Cancelled', airports: ['XEA2'] });

describe('the cards of /events', () => {
  test('narrowed to a kind, to an airport, to both, and to nothing chosen', () => {
    const cards = [rome, milan, everywhere, cancelled];

    expect(narrowCards(cards, {})).toEqual(cards);
    expect(narrowCards(cards, { kind: 'rfo' })).toEqual([rome, cancelled]);
    expect(narrowCards(cards, { airport: 'XEA2' })).toEqual([rome, cancelled]);
    expect(narrowCards(cards, { kind: 'rfe', airport: 'XEA2' })).toEqual([]);
  });

  test('an event of the whole division names no airport, so it is among the cards only when none is chosen', () => {
    expect(narrowCards([everywhere], { airport: 'XEA1' })).toEqual([]);
    expect(narrowCards([everywhere], { kind: 'online-day' })).toEqual([everywhere]);
  });

  test('the filters offer what the cards hold: the kinds in their order, the airports once each and sorted', () => {
    const cards = [milan, rome, everywhere, cancelled];

    expect(cardKinds(cards)).toEqual(['rfe', 'rfo', 'online-day']);
    expect(cardAirports(cards)).toEqual(['XEA1', 'XEA2', 'XEA3']);
  });

  test('in the calendar the same events lead to their pages, and a cancelled one is left out', () => {
    const items = calendarItems([rome, cancelled]);

    expect(items).toEqual([
      {
        id: 1,
        kind: 'rfo',
        title: rome.title,
        description: rome.summary,
        startsAt: rome.startsAtUtc,
        endsAt: rome.endsAtUtc,
        allDay: false,
        url: eventHref(rome.slug),
      },
    ]);
    expect(eventHref('evt-test-card-1')).toBe('/events/evt-test-card-1');
  });
});

describe('when an event runs', () => {
  // A stand-in for `useMoment`: the date and the time apart, so that what is left out can be seen.
  const format = (value: unknown, options: { timeZone?: string; time?: boolean; date?: boolean } = {}) => {
    const moment = new Date(value as string);
    const shifted = options.timeZone === 'Europe/Rome' ? new Date(moment.getTime() + 3600_000) : moment;
    const iso = shifted.toISOString();

    return [options.date === false ? '' : iso.slice(0, 10), options.time === false ? '' : iso.slice(11, 16)]
      .filter((part) => part !== '')
      .join(' ');
  };

  test('an evening is one date and two times', () => {
    expect(spanText('2026-11-21T17:00:00Z', '2026-11-21T22:00:00Z', format)).toBe('2026-11-21 17:00 – 22:00');
  });

  test('an event over midnight writes the date of its end too, and the days are read in the zone asked', () => {
    expect(spanText('2026-11-21T20:00:00Z', '2026-11-22T02:00:00Z', format)).toBe(
      '2026-11-21 20:00 – 2026-11-22 02:00',
    );
    expect(spanText('2026-11-21T21:00:00Z', '2026-11-21T22:30:00Z', format, 'Europe/Rome')).toBe(
      '2026-11-21 22:00 – 23:30',
    );
    expect(spanText('2026-11-21T22:30:00Z', '2026-11-21T23:30:00Z', format, 'Europe/Rome')).toBe(
      '2026-11-21 23:30 – 2026-11-22 00:30',
    );
  });
});
