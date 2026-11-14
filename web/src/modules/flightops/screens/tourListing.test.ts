import { describe, expect, it } from 'vitest';

import type { StartedTourDto } from '../api';

import type { TourCard } from './TourCards';
import { cardKinds, myCards, narrowCards, tabCards } from './tourListing';

/**
 * The list of the tours as design 11 §3 sorts it: the pilot's tours on top, the three tabs, the search by name and the
 * filter by kind — sorted answers of the server, which decides what the public sees and in what order.
 */

const card = (id: number, patch: Partial<TourCard> = {}): TourCard => ({
  id,
  slug: `tour-${id}`,
  kind: 'Sequential',
  title: `Tour ${id}`,
  summary: '',
  coverMediaId: null,
  state: 'Open',
  releaseAt: '2026-09-01T00:00:00Z',
  closeAt: '2026-12-31T23:59:00Z',
  legs: 3,
  totalNm: 900,
  minPilotRatingName: null,
  award: null,
  subtours: 0,
  ...patch,
});

const started = (tourId: number, patch: Partial<StartedTourDto> = {}): StartedTourDto => ({
  tourId,
  slug: `tour-${tourId}`,
  title: { en: `Tour ${tourId}` },
  parentTourId: null,
  startedAt: '2026-09-02T00:00:00Z',
  completedAt: null,
  done: 1,
  target: 3,
  unit: 'Legs',
  next: null,
  ...patch,
});

describe('«Your tours» on top of the list', () => {
  it('holds the tours the pilot started and has not finished, among those still flown', () => {
    const cards = [card(1), card(2), card(3, { state: 'Closing' }), card(4, { state: 'Upcoming' })];
    const mine = myCards(cards, [
      started(1),
      started(2, { completedAt: '2026-10-01T00:00:00Z' }),
      started(3),
      started(9, { parentTourId: 1 }),
    ]);

    expect(mine.map((entry) => entry.id)).toEqual([1, 3]);
  });

  it('is empty for a visitor', () => {
    expect(myCards([card(1)], undefined)).toEqual([]);
  });
});

describe('the tabs of the list', () => {
  const cards = [
    card(1),
    card(2, { state: 'Closing' }),
    card(3, { state: 'Upcoming' }),
    card(4, { state: 'Closed' }),
  ];

  it('hold the open and the closing tours, those to come, and the closed ones', () => {
    expect(tabCards('open', cards, []).map((entry) => entry.id)).toEqual([1, 2]);
    expect(tabCards('upcoming', cards, []).map((entry) => entry.id)).toEqual([3]);
    expect(tabCards('archive', cards, []).map((entry) => entry.id)).toEqual([4]);
  });

  it('leave out what is already on top, and keep a finished tour among the others', () => {
    expect(tabCards('open', cards, [card(1)]).map((entry) => entry.id)).toEqual([2]);
  });
});

describe('the search and the kind', () => {
  const cards = [
    card(1, { title: 'Giro delle Città d’Italia' }),
    card(2, { title: 'Across the Alps', kind: 'Hub' }),
    card(3, { title: 'Alpine loop', kind: 'Container' }),
  ];

  it('find every word of the search in the title, without case or accents', () => {
    expect(narrowCards(cards, { q: 'citta GIRO' }).map((entry) => entry.id)).toEqual([1]);
    expect(narrowCards(cards, { q: 'alp' }).map((entry) => entry.id)).toEqual([2, 3]);
    expect(narrowCards(cards, { q: '   ' }).map((entry) => entry.id)).toEqual([1, 2, 3]);
  });

  it('narrow to one kind, together with the search', () => {
    expect(narrowCards(cards, { kind: 'Hub' }).map((entry) => entry.id)).toEqual([2]);
    expect(narrowCards(cards, { q: 'alp', kind: 'Container' }).map((entry) => entry.id)).toEqual([3]);
  });

  it('offer the kinds the tab holds, in the order of the editor', () => {
    expect(cardKinds(cards)).toEqual(['Sequential', 'Hub', 'Container']);
  });
});
