import { queryOptions } from '@tanstack/react-query';

import type { CalendarKind } from '../api/bootstrap';
import { api, unwrap } from '../api/client';
import type { components } from '../api/schema';
import { calendarKindColour, calendarWindow, type CalendarKindColour } from '../ui/calendar';

/**
 * The free and the busy of the one calendar, as the date picker of the form draws them (note
 * 2026-11-18-il-calendario-libero-e-occupato). Pure functions over what `/api/calendar/busy` answers, so a test asks them what a
 * screen shows.
 *
 * ⚠️ **No rule of time lives here.** The server has already widened each entry to the time it takes (`CalendarBusy.Occupies`:
 * an entry with no end takes its hour, one of the whole day its day) and said whether its kind blocks: the browser only asks
 * which UTC days and hours a stretch touches. A second copy of "how long does an entry with no end last" is how the picker would
 * come to switch off one hour and the server refuse another.
 *
 * ⚠️ **UTC days and hours** (Carmine's answer 3), like `CalendarView`: the hub stores UTC and the network runs on it, and the
 * division's time is written beside the value, never instead of it.
 */
export type BusyStretch = components['schemas']['CalendarBusyDto'];

/** A UTC day as `2026-11-18`: the key a day of the picker and a stretch agree on. */
export type DayKey = string;

/** The UTC day an instant falls on. */
export function dayKeyOf(instant: Date): DayKey {
  return instant.toISOString().slice(0, 10);
}

/**
 * The picker draws plain days with no time zone in them, so a UTC day is handed to it as the same date at local midnight, and
 * read back the same way: the calendar of the browser is only a grid of numbers here.
 */
export function pickerDay(key: DayKey): Date {
  const [year, month, day] = key.split('-').map(Number) as [number, number, number];
  return new Date(year, month - 1, day);
}

/** The UTC day a day of the picker stands for (`pickerDay` read back). */
export function dayKeyOfPicker(day: Date): DayKey {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${day.getFullYear()}-${pad(day.getMonth() + 1)}-${pad(day.getDate())}`;
}

/**
 * What one day of the picker shows: the kinds that take some of it, in the vocabulary's order; whether blocking stretches take
 * all of it — no hour of it can be chosen —; and whether one takes some of it — a day without a time, which takes the whole day
 * (`CalendarBusy.BlockingAsync` with `allDay`), cannot be chosen.
 */
export interface BusyDay {
  kinds: string[];
  blocked: boolean;
  blocking: boolean;
}

/** What one hour of the picker shows: the stretches that take some of it, and whether one of them blocks it. */
export interface BusyHour {
  hour: number;
  stretches: BusyStretch[];
  blocked: boolean;
}

const HOUR = 3_600_000;
const DAY = 24 * HOUR;

/** Whether a stretch takes some of the time from `from` to `to` (milliseconds, `to` excluded). */
function touches(stretch: BusyStretch, from: number, to: number): boolean {
  return Date.parse(stretch.startsAtUtc) < to && Date.parse(stretch.endsAtUtc) > from;
}

/** Whether the blocking stretches together take all the time from `from` to `to`. */
function covered(stretches: readonly BusyStretch[], from: number, to: number): boolean {
  let reached = from;
  const blocking = stretches
    .filter((stretch) => stretch.blocks)
    .map((stretch) => [Date.parse(stretch.startsAtUtc), Date.parse(stretch.endsAtUtc)] as const)
    .sort((a, b) => a[0] - b[0]);

  for (const [start, end] of blocking) {
    if (start > reached) {
      break;
    }

    reached = Math.max(reached, end);
    if (reached >= to) {
      return true;
    }
  }

  return reached >= to;
}

/** The kinds in the order the vocabulary sorts them, the ones it does not know after, by their key. */
function inVocabularyOrder(kinds: Iterable<string>, vocabulary: readonly CalendarKind[]): string[] {
  const rank = (kind: string) => {
    const at = vocabulary.findIndex((word) => word.key === kind);
    return at === -1 ? Number.MAX_SAFE_INTEGER : at;
  };

  return [...new Set(kinds)].sort((a, b) => rank(a) - rank(b) || a.localeCompare(b));
}

/**
 * Every UTC day a stretch touches, with its kinds; a day nobody touches is not in the map, and is free. A day the blocking
 * stretches take from midnight to midnight is blocked: nothing on it can be chosen.
 */
export function busyDays(
  stretches: readonly BusyStretch[],
  vocabulary: readonly CalendarKind[] = [],
): Map<DayKey, BusyDay> {
  const kinds = new Map<DayKey, string[]>();
  const touching = new Map<DayKey, BusyStretch[]>();

  for (const stretch of stretches) {
    const start = Date.parse(stretch.startsAtUtc);
    const end = Date.parse(stretch.endsAtUtc);

    for (let day = Math.floor(start / DAY) * DAY; day < end; day += DAY) {
      const key = dayKeyOf(new Date(day));
      kinds.set(key, [...(kinds.get(key) ?? []), stretch.kind]);
      touching.set(key, [...(touching.get(key) ?? []), stretch]);
    }
  }

  return new Map(
    [...kinds].map(([key, found]) => {
      const from = Date.parse(`${key}T00:00:00Z`);
      return [
        key,
        {
          kinds: inVocabularyOrder(found, vocabulary),
          blocked: covered(touching.get(key) ?? [], from, from + DAY),
          blocking: (touching.get(key) ?? []).some((stretch) => stretch.blocks),
        },
      ];
    }),
  );
}

/** The twenty-four UTC hours of a day: what takes each, and whether it can be chosen. */
export function busyHours(stretches: readonly BusyStretch[], day: DayKey): BusyHour[] {
  const midnight = Date.parse(`${day}T00:00:00Z`);

  return Array.from({ length: 24 }, (_, hour) => {
    const from = midnight + hour * HOUR;
    const taking = stretches.filter((stretch) => touches(stretch, from, from + HOUR));
    return { hour, stretches: taking, blocked: taking.some((stretch) => stretch.blocks) };
  });
}

/** The stretches that take some of a day, by their start: the lines under the hours. */
export function stretchesOn(stretches: readonly BusyStretch[], day: DayKey): BusyStretch[] {
  const midnight = Date.parse(`${day}T00:00:00Z`);
  return stretches
    .filter((stretch) => touches(stretch, midnight, midnight + DAY))
    .sort((a, b) => Date.parse(a.startsAtUtc) - Date.parse(b.startsAtUtc));
}

/** The kinds a month shows, for its legend, with their colour and whether they block. */
export function legendOf(
  stretches: readonly BusyStretch[],
  vocabulary: readonly CalendarKind[],
): { kind: string; colour: CalendarKindColour; blocks: boolean }[] {
  return inVocabularyOrder(
    stretches.map((stretch) => stretch.kind),
    vocabulary,
  ).map((kind) => ({
    kind,
    colour: calendarKindColour(kind, vocabulary),
    blocks: stretches.some((stretch) => stretch.kind === kind && stretch.blocks),
  }));
}

/**
 * The colour of a dot, as a class Tailwind can find in this file. A closed set, the palette of the badge
 * (`CALENDAR_KIND_COLOURS`): a class built from the word would be one Tailwind never generated.
 */
export const DOT_CLASS: Record<CalendarKindColour, string> = {
  blue: 'bg-blue-500',
  green: 'bg-green-600',
  orange: 'bg-orange-500',
  purple: 'bg-purple-500',
  indigo: 'bg-indigo-500',
  pink: 'bg-pink-500',
  red: 'bg-red-500',
  yellow: 'bg-yellow-400',
  gray: 'bg-gray-400',
};

/** The UTC instant of an hour of a day, as the form holds it. */
export function hourInstant(day: DayKey, hour: number): string {
  return `${day}T${String(hour).padStart(2, '0')}:00:00Z`;
}

/** The row being written, so that its own entry does not occupy its own date: the module and the entry's `source_id`. */
export interface CalendarSource {
  module: string;
  id: string;
}

/** What a form hands a `calendar: true` field: the division's vocabulary, and the row being written when there is one. */
export interface FormCalendar {
  kinds: readonly CalendarKind[];
  except?: CalendarSource | undefined;
}

/** The free and the busy of the weeks a month grid draws, as the reader may see them. */
export function busyQuery(month: Date, except: CalendarSource | undefined) {
  const { from, to } = calendarWindow('month', month);

  return queryOptions({
    queryKey: ['calendar', 'busy', from, to, except?.module ?? null, except?.id ?? null] as const,
    queryFn: async (): Promise<BusyStretch[]> =>
      unwrap(
        await api.GET('/api/calendar/busy', {
          params: {
            query: {
              fromUtc: from,
              toUtc: to,
              ...(except === undefined ? {} : { exceptModule: except.module, exceptId: except.id }),
            },
          },
        }),
      ),
  });
}
