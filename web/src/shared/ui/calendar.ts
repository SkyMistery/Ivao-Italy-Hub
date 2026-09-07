import type { LocalizedString } from '../api/bootstrap';

/**
 * What `CalendarView` is handed, and the three ways of looking at it.
 *
 * They live beside the component rather than inside it because a file that exports both a component
 * and a constant loses its fast refresh (HANDOFF §3) — and because three other places name these:
 * the block's zod schema, the public route's search parameters, and the gallery.
 */

/** One entry, in the shape the `calendar` data block answers with. */
export interface CalendarItem {
  id?: number;
  kind?: string;
  title?: LocalizedString;
  description?: LocalizedString | null;
  startsAt?: string;
  endsAt?: string | null;
  allDay?: boolean;
  department?: string | null;
  url?: string | null;
}

/**
 * Agenda reads forwards from now; the other four are drawn around a day somebody chose — a week or
 * a month, as a grid of squares or as a list of days.
 *
 * The two lists were asked for by Carmine after running the demo of M1: a grid answers "what does
 * this month look like" and a list answers "what is on, in order", and a month with four entries in
 * it reads as four lines far better than as thirty-five squares of which thirty-one are empty.
 *
 * ⚠️ `agenda` is not offered by the public screen. It is what a `calendar` **block** inside a page
 * shows — "what is coming up", read forwards from now, with nothing to navigate — and the switcher
 * of the screen lists `CALENDAR_SCREEN_VIEWS` instead.
 */
export const CALENDAR_VIEWS = ['agenda', 'week', 'weekList', 'month', 'monthList'] as const;

export type CalendarViewMode = (typeof CALENDAR_VIEWS)[number];

/** The four a visitor chooses between: two stretches of time, two ways of drawing each. */
export const CALENDAR_SCREEN_VIEWS = ['week', 'weekList', 'month', 'monthList'] as const;

/** How long a view is. The agenda has no anchor, so it has no span either. */
export function calendarSpan(view: CalendarViewMode): 'week' | 'month' | null {
  switch (view) {
    case 'week':
    case 'weekList':
      return 'week';
    case 'month':
    case 'monthList':
      return 'month';
    default:
      return null;
  }
}

/** Squares or lines. The two say the same thing about the same days, and share their navigation. */
export function isCalendarGrid(view: CalendarViewMode): boolean {
  return view === 'week' || view === 'month';
}

/** The colours a chip may take. Atmosphere's own badge palette, minus the grey kept for "none". */
export type CalendarKindColour = 'blue' | 'green' | 'orange' | 'purple' | 'indigo' | 'pink' | 'gray';

/**
 * The colour a kind of entry is chipped with, so that a reader tells a training from a tour before
 * reading either word.
 *
 * ⚠️ Not a vocabulary, and deliberately not one yet. The kind of an entry is free text today —
 * `event`, `training`, `tour`, `meeting`, `deadline`, and whatever a module projects with its rows
 * — and turning it into a list the division decides is a separate request (11 of the demo), which
 * wants a permission of its own and has not been proposed yet.
 *
 * So: the five the entity's own documentation names get a colour each, chosen rather than drawn
 * out of a hat, because those are the ones a reader sees every day; anything else is derived from
 * the word, which gives a stable colour without anybody declaring anything. When the vocabulary
 * arrives the colour belongs on its rows, and both halves of this go away together.
 */
export function calendarKindColour(kind: string): CalendarKindColour {
  if (kind === '') {
    return 'gray';
  }

  const known = KNOWN_KINDS[kind.toLowerCase()];
  if (known !== undefined) {
    return known;
  }

  let hash = 0;
  for (const character of kind) {
    hash = (hash * 31 + character.codePointAt(0)!) % 1_000_003;
  }

  return DERIVED_COLOURS[hash % DERIVED_COLOURS.length]!;
}

const KNOWN_KINDS: Readonly<Record<string, CalendarKindColour>> = {
  event: 'blue',
  training: 'green',
  tour: 'purple',
  meeting: 'indigo',
  deadline: 'orange',
};

const DERIVED_COLOURS = ['blue', 'green', 'orange', 'purple', 'indigo', 'pink'] as const;

/**
 * The days a grid draws, in UTC.
 *
 * A week is the seven days from the Monday on or before the day it is drawn around. A month is the
 * whole month padded out to full weeks, so the grid is a rectangle and the days of the neighbouring
 * months are there and visibly not part of it.
 *
 * ⚠️ **UTC days, deliberately**: the hub stores UTC and the network runs on it, and a grid whose day
 * boundaries moved with the reader's browser would put the same entry in two different squares for
 * two people looking at the same page.
 *
 * ⚠️ It lives here, next to the window below, because **the two have to agree**. A screen asks the
 * provider for a stretch of time and then draws squares; if the stretch were worked out separately
 * from the squares, a month grid opened on the 28th would ask for the wrong three weeks and draw
 * empty days that are not empty. One function decides, and the other reads it.
 */
export function calendarDays(view: CalendarViewMode, anchor: Date): Date[] {
  const week = calendarSpan(view) === 'week';

  const start = new Date(
    Date.UTC(anchor.getUTCFullYear(), anchor.getUTCMonth(), week ? anchor.getUTCDate() : 1),
  );

  // Monday first: a division that flies at weekends reads a week better ending on Sunday.
  const weekday = (start.getUTCDay() + 6) % 7;
  start.setUTCDate(start.getUTCDate() - weekday);

  const daysInMonth = new Date(Date.UTC(anchor.getUTCFullYear(), anchor.getUTCMonth() + 1, 0)).getUTCDate();

  const count = week ? 7 : Math.ceil((weekday + daysInMonth) / 7) * 7;

  return Array.from({ length: count }, (_, index) => {
    const day = new Date(start);
    day.setUTCDate(start.getUTCDate() + index);
    return day;
  });
}

/**
 * The stretch of time to ask a provider for, so that every square a grid draws is filled.
 *
 * The agenda has no squares: it reads forwards from now and takes what it is given, which is what
 * "coming up" means.
 */
export function calendarWindow(view: CalendarViewMode, anchor: Date): { from: string; to: string } {
  if (view === 'agenda') {
    const now = new Date();
    const until = new Date(now);
    until.setUTCDate(until.getUTCDate() + AGENDA_DAYS);

    return { from: now.toISOString(), to: until.toISOString() };
  }

  const days = calendarDays(view, anchor);
  const first = days[0]!;
  const last = new Date(days[days.length - 1]!);

  // The day after the last square, because the provider's window ends before `to`.
  last.setUTCDate(last.getUTCDate() + 1);

  return { from: first.toISOString(), to: last.toISOString() };
}

/** How far ahead the agenda looks. A month of "what is next" is what a visitor reads in one go. */
const AGENDA_DAYS = 31;
