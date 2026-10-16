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
  dateStyle?: 'medium' | 'short' | 'long';
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

  return new Intl.DateTimeFormat(locale, {
    ...(options.date === false ? {} : { dateStyle: options.dateStyle ?? 'medium' }),
    ...(options.time === false ? {} : { timeStyle: 'short', hour12: false }),
    timeZone: options.timeZone ?? 'UTC',
  }).format(moment);
}
