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
