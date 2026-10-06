import type { LocalizedString } from '../../../shared/api/bootstrap';
import type { CalendarItem, CalendarKindColour } from '../../../shared/ui';

/**
 * The cards of the events (design M4 §7.1, §7.3, E4) as data: what the block `events.eventList` answers, and the questions
 * `/events` asks of it — narrowed to a kind and an airport, the choices of the two filters, the same events as entries of the
 * calendar. Plain TypeScript beside the components, so that a test reads it without drawing anything.
 */

/** The block's type, as `EventListProvider.BlockType` spells it on the server. */
export const EVENT_LIST_BLOCK = 'events.eventList';

/** The page of an event, where a card, a calendar entry and a line of the search all lead (E3b). */
export function eventHref(slug: string): string {
  return `/events/${slug}`;
}

/** One event as the block answers it: what fits on a card, the same for whoever is looking. */
export interface EventCard {
  readonly id: number;
  readonly slug: string;
  /** A key of the calendar's kinds: the bootstrap carries its word and its colour. */
  readonly kind: string;
  readonly title: LocalizedString | null;
  readonly summary: LocalizedString | null;
  readonly bannerMediaId: number | null;
  /** Announced, BookingOpen, InProgress or Cancelled: an event the public sees is in one of the four. */
  readonly state: string;
  readonly startsAtUtc: string;
  readonly endsAtUtc: string;
  /** About every airport of the division, as an online day is: it names none of its own. */
  readonly wholeDivision: boolean;
  /** Its own airports, in their order. */
  readonly airports: readonly string[];
}

/** What `EventListProvider` answers with; undefined while it is on its way. */
export interface EventListData {
  items?: EventCard[];
}

/** The colour of a state, on a card and on the page: what is open stands out, what is cancelled warns. */
export const STATE_COLOURS: Readonly<Record<string, CalendarKindColour>> = {
  Draft: 'gray',
  Scheduled: 'gray',
  Announced: 'gray',
  BookingOpen: 'blue',
  InProgress: 'green',
  Ended: 'gray',
  Cancelled: 'red',
};

/** What `/events` is narrowed to. Left out, everything. */
export interface CardFilters {
  readonly kind?: string | undefined;
  readonly airport?: string | undefined;
}

/**
 * The cards of one kind and at one airport. An event names the airports it is at; one of the whole division names none, so it is
 * among the cards only when no airport is chosen.
 */
export function narrowCards(cards: readonly EventCard[], filters: CardFilters): EventCard[] {
  return cards.filter(
    (card) =>
      (filters.kind === undefined || card.kind === filters.kind) &&
      (filters.airport === undefined || card.airports.includes(filters.airport)),
  );
}

/** The kinds the cards are of, once each, in the order the cards come: the choices of the filter of the kinds. */
export function cardKinds(cards: readonly EventCard[]): string[] {
  return [...new Set(cards.map((card) => card.kind))];
}

/** The airports the cards name, once each, in alphabetical order: the choices of the filter of the airports. */
export function cardAirports(cards: readonly EventCard[]): string[] {
  return [...new Set(cards.flatMap((card) => card.airports))].sort();
}

/**
 * The cards as entries of the calendar under them: the same events, each where its page is. A cancelled one is left out, as the
 * division's calendar leaves it out (E3b): its card says it is cancelled, a square of a grid would only say it is on.
 */
export function calendarItems(cards: readonly EventCard[]): CalendarItem[] {
  return cards
    .filter((card) => card.state !== 'Cancelled')
    .map((card) => ({
      id: card.id,
      kind: card.kind,
      ...(card.title === null ? {} : { title: card.title }),
      description: card.summary,
      startsAt: card.startsAtUtc,
      endsAt: card.endsAtUtc,
      allDay: false,
      url: eventHref(card.slug),
    }));
}

/**
 * When an event runs, as one line: its start with its date, and its end — with its own date only when it falls on another day, so
 * that an evening reads «12 Oct 2026, 18:00 – 22:00». `format` is the hub's one way of writing an instant (`useMoment`), handed in
 * with the zone to read the days in: UTC when none.
 */
export function spanText(
  startsAt: string,
  endsAt: string,
  format: (value: unknown, options?: { timeZone?: string; time?: boolean; date?: boolean }) => string,
  timeZone?: string,
): string {
  const zone = timeZone === undefined ? {} : { timeZone };
  const sameDay = format(startsAt, { ...zone, time: false }) === format(endsAt, { ...zone, time: false });

  return `${format(startsAt, zone)} – ${format(endsAt, sameDay ? { ...zone, date: false } : zone)}`;
}
