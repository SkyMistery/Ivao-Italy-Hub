import { formatMoment } from './useMoment';

/**
 * What somebody typed in a date field, read back in the division's order (note 2026-11-18-il-calendario-libero-e-occupato, the
 * question note 2026-10-31-il-formato-della-data §3.2 left to this phase): `31/10/2026 14:05` in a division that writes
 * `dd/mm/yyyy`, `10/31/2026 14:05` in one that writes `mm/dd/yyyy`. The answer is the ISO instant in UTC the form holds, or
 * `null` for a text that is not a date — a day that does not exist, a month thirteen, an hour twenty-five, a field with a time
 * that was written without one. An empty text is `undefined`: nothing written.
 *
 * The order is **asked of `formatMoment`**, by writing a day whose three numbers differ and seeing where each lands, rather
 * than read from a second copy of the division's setting: the date is read back in the order it is written, by construction.
 *
 * Slashes, dashes, dots and spaces all separate, because a keyboard and a habit choose them, not the division; the year has
 * four digits, because `03/04/05` is a riddle in any order.
 */
export function parseMoment(text: string, withTime: boolean): string | null | undefined {
  const written = text.trim();
  if (written === '') {
    return undefined;
  }

  const match = /^(\d{1,4})[/.\-\s](\d{1,2})[/.\-\s](\d{1,4})(?:[\sT,]+(\d{1,2})[:.](\d{2}))?$/.exec(written);
  if (match === null) {
    return null;
  }

  const numbers = [match[1], match[2], match[3]] as [string, string, string];
  const order = dateOrder();
  const part = (which: 'day' | 'month' | 'year') => numbers[order.indexOf(which)]!;

  if (part('year').length !== 4) {
    return null;
  }

  const year = Number(part('year'));
  const month = Number(part('month'));
  const day = Number(part('day'));
  const hasTime = match[4] !== undefined;

  if (withTime !== hasTime) {
    return null;
  }

  const hour = hasTime ? Number(match[4]) : 0;
  const minute = hasTime ? Number(match[5]) : 0;

  const moment = new Date(Date.UTC(year, month - 1, day, hour, minute));
  if (
    month < 1 ||
    month > 12 ||
    hour > 23 ||
    minute > 59 ||
    // A 31 November rolls over to 1 December in `Date`: the day it became is not the day written.
    moment.getUTCFullYear() !== year ||
    moment.getUTCMonth() !== month - 1 ||
    moment.getUTCDate() !== day
  ) {
    return null;
  }

  return `${moment.toISOString().slice(0, 16)}:00Z`;
}

/** Where day, month and year stand in what `formatMoment` writes now: 3 February 2001 has three numbers that cannot be confused. */
function dateOrder(): readonly ('day' | 'month' | 'year')[] {
  const sample = formatMoment('en', '2001-02-03T00:00:00Z', { time: false });
  const found = sample
    .split('/')
    .map((piece) => (piece === '2001' ? 'year' : piece === '02' ? 'month' : 'day'));
  return found.length === 3 ? found : ['day', 'month', 'year'];
}

/** The instant as the field writes it back for a person to read and correct: the division's order, UTC, 24 hours. */
export function writeMoment(iso: string, withTime: boolean): string {
  return formatMoment('en', iso, { dateStyle: 'short', time: withTime });
}
