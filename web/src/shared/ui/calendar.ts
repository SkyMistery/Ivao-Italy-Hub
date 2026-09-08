import type { CalendarKind, LocalizedString } from '../api/bootstrap';

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

/** The colours a chip may take. Atmosphere's own badge palette, and the server holds the same set. */
export type CalendarKindColour =
  'blue' | 'green' | 'orange' | 'purple' | 'indigo' | 'pink' | 'red' | 'yellow' | 'gray';

/**
 * ⚠️ The other half of `CalendarKindWriteDtoValidator.Colours`. The two agree by hand — a colour is
 * a value inside a design system, which the OpenAPI contract cannot carry — and the integration
 * test that posts a colour the server does not know is what keeps them agreeing, exactly as it does
 * for the backgrounds of a section.
 */
export const CALENDAR_KIND_COLOURS: readonly CalendarKindColour[] = [
  'blue',
  'green',
  'orange',
  'purple',
  'indigo',
  'pink',
  'red',
  'yellow',
  'gray',
];

/**
 * The colour and the word of a kind, out of the division's vocabulary.
 *
 * ⚠️ Until G13 the colour was **derived from the word**, because the kinds were free text and there
 * was nothing to look one up in. Now there is a table the division decides
 * (`decisions/2026-09-08-tipi-di-evento-di-divisione.md`), it travels with the bootstrap, and a
 * colour somebody chose can group two kinds that belong together — which a hash never could.
 *
 * A kind that is not in the vocabulary still draws: an entry projected by a module carries whatever
 * word that module wrote, and it is not this component's business to refuse it. It is grey and it
 * says the word it has.
 */
export function calendarKindColour(
  kind: string,
  vocabulary: readonly CalendarKind[] = [],
): CalendarKindColour {
  const known = vocabulary.find((word) => word.key === kind)?.colour;
  return CALENDAR_KIND_COLOURS.includes(known as CalendarKindColour) ? (known as CalendarKindColour) : 'gray';
}

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
