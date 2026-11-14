import type { StartedTourDto } from '../api';
import type { TourListSearch, TourListTab } from '../schemas';

import type { TourCard } from './TourCards';

/**
 * What the list of the tours puts where (design 11 §3, phase 6): the pilot's tours on top, the three tabs, the search and
 * the kind. The server answers which tours the public sees and in what order (`PublicTours.CardsAsync`); these only sort
 * its answer, and they are pure, so a test reads them without a browser.
 */

/** The kinds of a tour, in the order the editor offers them: the order of the filter. */
const KIND_ORDER = ['Sequential', 'SequentialChosenStart', 'Free', 'Hub', 'Distance', 'Open', 'Container'];

/** The states each tab holds. The archive is a read of its own, asked only when its tab opens. */
const TAB_STATES: Readonly<Record<TourListTab, readonly string[]>> = {
  open: ['Open', 'Closing'],
  upcoming: ['Upcoming'],
  archive: ['Closed'],
};

/**
 * «Your tours» (design 11 §3): the first-level tours the pilot started and has not finished, among those still flown. A
 * finished one goes down among the others, with its mark (the card's «Completed»); a subtour is in its container.
 */
export function myCards(
  cards: readonly TourCard[],
  started: readonly StartedTourDto[] | undefined,
): TourCard[] {
  const running = new Set(
    (started ?? [])
      .filter((row) => row.completedAt === null && row.parentTourId === null)
      .map((row) => row.tourId),
  );

  return cards.filter((card) => running.has(card.id) && TAB_STATES.open.includes(card.state));
}

/** The cards of a tab, without those already on top in «Your tours». */
export function tabCards(
  tab: TourListTab,
  cards: readonly TourCard[],
  mine: readonly TourCard[],
): TourCard[] {
  const onTop = new Set(mine.map((card) => card.id));

  return cards.filter((card) => TAB_STATES[tab].includes(card.state) && !onTop.has(card.id));
}

/** A name as a search compares it: no case, no accents. */
function folded(text: string): string {
  return text
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLocaleLowerCase();
}

/** The cards the search and the kind leave: every word of the search in the title, in the language on screen. */
export function narrowCards(
  cards: readonly TourCard[],
  search: Pick<TourListSearch, 'q' | 'kind'>,
): TourCard[] {
  const words = folded(search.q ?? '')
    .split(/\s+/)
    .filter((word) => word !== '');

  return cards.filter(
    (card) =>
      (search.kind === undefined || card.kind === search.kind) &&
      words.every((word) => folded(card.title).includes(word)),
  );
}

/** The kinds the filter offers: those the cards of the tab hold, as `/events` does — a kind nobody has narrows to nothing. */
export function cardKinds(cards: readonly TourCard[]): string[] {
  const held = new Set(cards.map((card) => card.kind));

  return [
    ...KIND_ORDER.filter((kind) => held.has(kind)),
    ...[...held].filter((kind) => !KIND_ORDER.includes(kind)),
  ];
}
