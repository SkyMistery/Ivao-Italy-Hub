import { useTranslation } from 'react-i18next';

/**
 * An instant as a reader reads one. UTC unless a zone is named, because UTC is what the network
 * runs on and what the hub stores; a local time is shown next to it and never instead of it
 * (plan §9.5). An empty string means "there was no usable instant", so a caller can leave it out.
 *
 * It lives here rather than beside the blocks because two very different things format an instant
 * the same way — a data block and `CalendarView` — and a second copy is how one of them quietly
 * starts showing a local time as if it were UTC.
 *
 * ⚠️ **Twenty four hours, always**, whatever the language would have chosen. This is a hub for a
 * flight simulation network: a briefing at 14:05 is written 14:05, and "2:05 PM" is a form nobody
 * in the cockpit uses. Asked for by Carmine running the demo, and it is one line here rather than a
 * choice each caller makes — which is the whole reason this function exists.
 *
 * ⚠️ **The day in the division's order**, whatever the language would have chosen: `31/10/2026`,
 * or `10/31/2026` or `2026/10/31` for a division that says so in `division.json → dateFormat`
 * (note 2026-10-31-il-formato-della-data). Only a date written with its month in words —
 * `dateStyle: 'long'`, "31 October 2026" — keeps the language's own order: there it cannot be
 * misread.
 */
export function useMoment(): (value: unknown, options?: MomentOptions) => string {
  const { i18n } = useTranslation();

  return (value, options) => formatMoment(i18n.language, value, options);
}

export interface MomentOptions {
  timeZone?: string;
  time?: boolean;
  /** False leaves the date out entirely, for a screen that has already said which day it is. */
  date?: boolean;
  /** `long` writes the month in words, in the language; the other two write the day in numbers, in the division's order. */
  dateStyle?: 'medium' | 'short' | 'long';
}

/** The orders a division may write a date in (`division.json → dateFormat`), the server's `HubDates.Formats`. */
export const DATE_FORMATS = ['dd/mm/yyyy', 'mm/dd/yyyy', 'yyyy/mm/dd'] as const;

export type DateFormat = (typeof DATE_FORMATS)[number];

/** The order of a division that says nothing. */
export const DEFAULT_DATE_FORMAT: DateFormat = 'dd/mm/yyyy';

/**
 * The division's order, set once by the root route when the bootstrap arrives (`setDateFormat`) and
 * read by every `formatMoment`, as i18next holds the language: a cell of the generic list is handed
 * the language and not the division, and threading a second argument through every one of a
 * hundred and fifty callers is how one of them would come to forget it.
 */
let divisionFormat: DateFormat = DEFAULT_DATE_FORMAT;

/** Takes the division's `dateFormat` from the bootstrap; anything the client does not know is the default. */
export function setDateFormat(format: string | undefined): void {
  divisionFormat = DATE_FORMATS.find((known) => known === format) ?? DEFAULT_DATE_FORMAT;
}

/**
 * `useMoment` for a caller that is not a component — a cell of the generic list, the line under an
 * instant field — and is handed the language rather than reading it. The one place that says how
 * the hub writes an instant, the twenty four hours included (note 2026-10-13-le-ore-in-24): a copy
 * of the options elsewhere is how three formatters came to write "7:00 PM" in English.
 */
export function formatMoment(locale: string, value: unknown, options: MomentOptions = {}): string {
  if (typeof value !== 'string' || value === '') {
    return '';
  }

  const moment = new Date(value);
  if (Number.isNaN(moment.getTime())) {
    return '';
  }

  const timeZone = options.timeZone ?? 'UTC';
  const time =
    options.time === false
      ? ''
      : new Intl.DateTimeFormat(locale, { timeStyle: 'short', hour12: false, timeZone }).format(moment);

  if (options.date === false) {
    return time;
  }

  if (options.dateStyle === 'long') {
    return new Intl.DateTimeFormat(locale, {
      dateStyle: 'long',
      ...(options.time === false ? {} : { timeStyle: 'short', hour12: false }),
      timeZone,
    }).format(moment);
  }

  const day = numericDay(moment, timeZone);
  return time === '' ? day : `${day} ${time}`;
}

/**
 * `useMoment`'s formatter as the helpers below take it, handed in so that what they say stays a function of their data and a test
 * reads them without drawing anything. Any formatter that knows the zone and the two halves will do.
 */
export type MomentFormat = (
  value: string,
  options?: Pick<MomentOptions, 'timeZone' | 'time' | 'date'>,
) => string;

/** Whether two instants fall on the same day in a zone — UTC when none —, as the formatter writes the day. */
export function onSameDay(one: string, other: string, moment: MomentFormat, timeZone?: string): boolean {
  const zone = timeZone === undefined ? {} : { timeZone };

  return moment(one, { ...zone, time: false }) === moment(other, { ...zone, time: false });
}

/**
 * The end of an interval as a line writes it after its start (note 2026-11-18-la-fine-di-un-intervallo-nel-nucleo): the time alone
 * when it ends on the day it starts, in the zone asked — UTC when none —, and the date with it when it does not, so that an hour
 * which ends at 02:45 the next day never reads as a bare 02:45. The one copy of what a flight's end (#334, item 4), the end of an
 * event's hours (#333, item 6) and the end of an event's or a training's span used to write each on their own; what stands before
 * it — the start, a dash, a sentence — is the caller's.
 */
export function intervalEnd(
  startsAt: string,
  endsAt: string,
  moment: MomentFormat,
  timeZone?: string,
): string {
  const zone = timeZone === undefined ? {} : { timeZone };

  return onSameDay(startsAt, endsAt, moment, timeZone)
    ? moment(endsAt, { ...zone, date: false })
    : moment(endsAt, zone);
}

/**
 * An interval as one line: its start whole, a spaced dash, and its end as `intervalEnd` writes it — «21/11/2026 17:00 – 22:00»,
 * «21/11/2026 23:00 – 22/11/2026 01:00» —, the days read in the zone asked, UTC when none. The dash is spaced everywhere, on one day
 * as on two, because the part before it has spaces of its own (Carmine, 19 Nov 2026; note
 * 2026-11-18-la-fine-di-un-intervallo-nel-nucleo). Without an end, its start. The one copy of what the events' and the training's
 * `spanText` wrote each on their own; the zone is the sentence's to name.
 */
export function intervalText(
  startsAt: string,
  endsAt: string | null,
  moment: MomentFormat,
  timeZone?: string,
): string {
  const zone = timeZone === undefined ? {} : { timeZone };
  const start = moment(startsAt, zone);

  return endsAt === null ? start : `${start} – ${intervalEnd(startsAt, endsAt, moment, timeZone)}`;
}

/** The day of an instant in a zone, in numbers and in the division's order. */
function numericDay(moment: Date, timeZone: string): string {
  // The digits, not the words, so any language that writes Latin digits will do: the order is the division's.
  const parts = new Intl.DateTimeFormat('en', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(moment);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((entry) => entry.type === type)?.value ?? '';
  const [day, month, year] = [part('day'), part('month'), part('year')];

  switch (divisionFormat) {
    case 'mm/dd/yyyy':
      return `${month}/${day}/${year}`;
    case 'yyyy/mm/dd':
      return `${year}/${month}/${day}`;
    default:
      return `${day}/${month}/${year}`;
  }
}
