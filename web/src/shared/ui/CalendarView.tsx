import { Badge, Button, H4 } from '@ivao/atmosphere-react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { useLocalized } from '../i18n/useLocalized';
import { useMoment } from '../i18n/useMoment';

import type { CalendarKind } from '../api/bootstrap';

import {
  calendarDays,
  calendarKindColour,
  calendarSpan,
  isCalendarGrid,
  type CalendarItem,
  type CalendarViewMode,
} from './calendar';

/**
 * The one calendar of the division, drawn.
 *
 * A custom component of the closed list (docs/UI-GUIDELINES.md §3), and it earns the place by the
 * criterion written there: **two screens mount it** — the public `/calendar` and the `calendar`
 * block inside a page. Atmosphere has a `Calendar`, and it is a date *picker*, which is a different
 * thing: it answers "which day do you mean", not "what is happening".
 *
 * ⚠️ Every time is shown in **UTC and in the division's own zone**, never one instead of the other
 * (plan §9.5). The zone is handed in — it comes from `/api/me` — and is never a constant here: a
 * hub that assumed Europe/Rome would be a hub that only one division can fork.
 *
 * The component draws and decides nothing else: which entries there are, and for which window, is
 * the caller's business. That is what lets the same component be a month grid on a page of its own
 * and an agenda of five lines inside a section.
 */

export function CalendarView({
  items,
  view,
  /** The day the grid is drawn around. Ignored by the agenda, which reads forwards from now. */
  anchor,
  onAnchorChange,
  timezone,
  empty,
  kinds = [],
}: {
  items: readonly CalendarItem[];
  view: CalendarViewMode;
  anchor?: Date | undefined;
  /** Absent on a block: a page inside a section does not navigate months. */
  onAnchorChange?: ((next: Date) => void) | undefined;
  timezone: string;
  empty: string;
  /**
   * The division's vocabulary, as `/api/me` carries it: what to call a kind in the language on
   * screen, and the colour of its chip. Handed in rather than fetched, because this component asks
   * nobody for anything — and an entry whose kind is not in it still draws, in grey, with the word
   * it has.
   */
  kinds?: readonly CalendarKind[];
}) {
  if (view === 'agenda') {
    return <Agenda items={items} timezone={timezone} empty={empty} kinds={kinds} />;
  }

  const around = anchor ?? new Date();

  return (
    <div className="flex flex-col gap-4">
      <Navigation view={view} anchor={around} onAnchorChange={onAnchorChange} />

      {items.length === 0 ? <Nothing empty={empty} /> : null}

      {isCalendarGrid(view) ? (
        <Grid items={items} view={view} anchor={around} timezone={timezone} kinds={kinds} />
      ) : (
        <Days items={items} view={view} anchor={around} timezone={timezone} kinds={kinds} />
      )}
    </div>
  );
}

/**
 * Which stretch of time is on screen, and the way out of it. It belongs to the four anchored views
 * and not to the grid, because a list of the same week navigates exactly the same way — sharing it
 * is what stops "next" meaning a month in one view and a week in the other by accident.
 */
function Navigation({
  view,
  anchor,
  onAnchorChange,
}: {
  view: CalendarViewMode;
  anchor: Date;
  onAnchorChange?: ((next: Date) => void) | undefined;
}) {
  const { t, i18n } = useTranslation();

  if (onAnchorChange === undefined) {
    return null;
  }

  const heading = new Intl.DateTimeFormat(i18n.language, {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(anchor);

  const step = (direction: -1 | 1) => {
    const next = new Date(anchor);

    if (calendarSpan(view) === 'month') {
      next.setUTCMonth(next.getUTCMonth() + direction);
    } else {
      next.setUTCDate(next.getUTCDate() + direction * 7);
    }

    onAnchorChange(next);
  };

  return (
    <div className="flex items-center justify-between gap-4">
      <Button variant="ghost" size="sm" onClick={() => step(-1)} aria-label={t('calendar.previous')}>
        <ChevronLeft aria-hidden className="size-4" />
      </Button>
      <span className="font-medium">{heading}</span>
      <Button variant="ghost" size="sm" onClick={() => step(1)} aria-label={t('calendar.next')}>
        <ChevronRight aria-hidden className="size-4" />
      </Button>
    </div>
  );
}

/** What a list of entries looks like when there is nothing in it. */
function Nothing({ empty }: { empty: string }) {
  return <p className="text-muted-foreground py-6 text-sm">{empty}</p>;
}

/**
 * One entry as a line: when it is, in both zones, what it is called and what it is about. It is the
 * whole of the agenda and it is also what a day of the grid holds, so the two can never drift into
 * showing an entry differently.
 */
function Entry({
  item,
  timezone,
  kinds,
  compact = false,
  withDate = false,
}: {
  item: CalendarItem;
  timezone: string;
  kinds: readonly CalendarKind[];
  compact?: boolean;
  /**
   * Whether to write the day beside the time. Only the agenda does: it is a flat list running
   * forward, so nothing else on the screen says which day an entry is on. A square of the grid and
   * a heading of the day list have already said it, and repeating it there is noise on the line a
   * reader actually reads (asked for by Carmine running the demo).
   */
  withDate?: boolean;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();

  const allDay = item.allDay === true;

  // An all-day entry has nothing but its day, so it keeps the date wherever it is drawn: a line
  // that said only "Z" would be saying nothing at all.
  const withDay = withDate || allDay;
  const utc = moment(item.startsAt, { time: !allDay, date: withDay });

  // An all-day entry has no time to convert, so showing a second line for it would be inventing a
  // difference; a timed one always shows both, even when the two read the same, because a reader
  // has to be able to tell which is which.
  const local = allDay ? '' : moment(item.startsAt, { timeZone: timezone, date: withDay });
  const title = read(item.title);
  const summary = read(item.description);

  return (
    <div className="flex flex-col gap-1">
      <div className="text-muted-foreground flex flex-wrap items-baseline gap-2 text-xs">
        <time className="tabular-nums" dateTime={item.startsAt ?? undefined}>
          {/* ⚠️ `Z` only on an instant. An all-day entry is a **day**, and a day is not a time in
              UTC: "Sep 20, 2026Z" is a letter glued to something that has no clock in it. Caught by
              the test that asked for the date to go away everywhere else. */}
          {allDay ? utc : t('calendar.utc', { at: utc })}
        </time>
        {local === '' ? null : <span className="tabular-nums">{t('calendar.local', { at: local })}</span>}
        {item.kind === undefined || item.kind === '' ? null : (
          // The word and the colour of the division's vocabulary, so a reader tells a training from
          // a tour before reading either — and reads it in their own language. A kind nobody
          // declared keeps the key it has: an entry a module projected is not this chip's business.
          <Badge
            variant="flat"
            color={calendarKindColour(item.kind, kinds)}
            text={read(kinds.find((word) => word.key === item.kind)?.label) || item.kind}
          />
        )}
      </div>

      {compact ? (
        <span className="truncate text-sm font-medium">
          {item.url == null || item.url === '' ? title : <a href={item.url}>{title}</a>}
        </span>
      ) : (
        <H4>{item.url == null || item.url === '' ? title : <a href={item.url}>{title}</a>}</H4>
      )}

      {compact || summary === '' ? null : <p className="text-muted-foreground text-sm">{summary}</p>}
    </div>
  );
}

/** Forwards from now, one line each. What a section inside a page shows. */
function Agenda({
  items,
  timezone,
  empty,
  kinds,
}: {
  items: readonly CalendarItem[];
  timezone: string;
  empty: string;
  kinds: readonly CalendarKind[];
}) {
  if (items.length === 0) {
    return <Nothing empty={empty} />;
  }

  return (
    <ul className="flex flex-col divide-y">
      {items.map((item) => (
        <li key={item.id} className="py-3 first:pt-0 last:pb-0">
          <Entry item={item} timezone={timezone} kinds={kinds} withDate />
        </li>
      ))}
    </ul>
  );
}

/** The UTC midnight of a day, which is the key a grid puts its entries under. */
function dayKey(value: Date): string {
  return value.toISOString().slice(0, 10);
}

/**
 * A grid of days: seven of them for a week, the whole month padded to full weeks for a month.
 *
 * ⚠️ The days are **UTC days**, and deliberately: the hub stores UTC, the network runs on it, and a
 * grid whose day boundaries moved with the reader's browser would put the same entry in two
 * different squares for two people looking at the same page.
 */
function Grid({
  items,
  view,
  anchor,
  timezone,
  kinds,
}: {
  items: readonly CalendarItem[];
  view: CalendarViewMode;
  anchor: Date;
  timezone: string;
  kinds: readonly CalendarKind[];
}) {
  const squares = calendarDays(view, anchor);
  const byDay = entriesByDay(items);

  return (
    // Seven columns from `sm` up and two on a phone, where seven squares of forty pixels are a
    // grid nobody can read. The day's own number stays visible in both.
    <div className="grid grid-cols-2 gap-px sm:grid-cols-7" role="grid">
      {squares.map((day) => {
        const key = dayKey(day);
        const inMonth = calendarSpan(view) === 'week' || day.getUTCMonth() === anchor.getUTCMonth();
        const entries = byDay.get(key) ?? [];

        return (
          <div
            key={key}
            role="gridcell"
            className={`border-border flex min-h-24 flex-col gap-2 border p-2 ${
              inMonth ? '' : 'text-muted-foreground bg-muted/40'
            }`}
          >
            <span className="text-xs tabular-nums">{day.getUTCDate()}</span>

            {entries.map((item) => (
              <Entry key={item.id} item={item} timezone={timezone} kinds={kinds} compact />
            ))}
          </div>
        );
      })}
    </div>
  );
}

/**
 * The same days as the grid, read down the page instead of across it: one heading per day that has
 * anything on it, and the entries of that day under it in full.
 *
 * Asked for by Carmine after the demo, and it is not a second calendar: it draws the days
 * `calendarDays` gives for the very same window, and each entry with the same `Entry` the grid and
 * the agenda use. A month with four things in it reads as four lines rather than as thirty-five
 * squares of which thirty-one are empty.
 *
 * ⚠️ Empty days are left out, which is the whole difference from the grid: a list that printed every
 * day of the month would be the grid again, only taller.
 */
function Days({
  items,
  view,
  anchor,
  timezone,
  kinds,
}: {
  items: readonly CalendarItem[];
  view: CalendarViewMode;
  anchor: Date;
  timezone: string;
  kinds: readonly CalendarKind[];
}) {
  const { i18n } = useTranslation();

  const byDay = entriesByDay(items);

  const heading = new Intl.DateTimeFormat(i18n.language, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    timeZone: 'UTC',
  });

  const days = calendarDays(view, anchor)
    .map((day) => ({ day, entries: byDay.get(dayKey(day)) ?? [] }))
    .filter(({ entries }) => entries.length > 0);

  return (
    <ul className="flex flex-col gap-6">
      {days.map(({ day, entries }) => (
        <li key={dayKey(day)} className="flex flex-col gap-3">
          {/* UTC, like the squares of the grid and for the same reason: a heading that moved with
              the reader's browser would file an entry under a different day for two people
              reading the same page. */}
          <h3 className="border-border border-b pb-1 text-sm font-semibold">{heading.format(day)}</h3>

          <ul className="flex flex-col divide-y">
            {entries.map((item) => (
              <li key={item.id} className="py-3 first:pt-0 last:pb-0">
                <Entry item={item} timezone={timezone} kinds={kinds} />
              </li>
            ))}
          </ul>
        </li>
      ))}
    </ul>
  );
}

/** The entries of a window, filed under the UTC day they start on. Read by the grid and the list. */
function entriesByDay(items: readonly CalendarItem[]): Map<string, CalendarItem[]> {
  const byDay = new Map<string, CalendarItem[]>();

  for (const item of items) {
    if (typeof item.startsAt !== 'string') {
      continue;
    }

    const when = new Date(item.startsAt);
    if (Number.isNaN(when.getTime())) {
      continue;
    }

    const key = dayKey(when);
    byDay.set(key, [...(byDay.get(key) ?? []), item]);
  }

  return byDay;
}
