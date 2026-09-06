import { Badge, Button, H4 } from '@ivao/atmosphere-react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { useLocalized } from '../i18n/useLocalized';
import { useMoment } from '../i18n/useMoment';

import { calendarDays, type CalendarItem, type CalendarViewMode } from './calendar';

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
}: {
  items: readonly CalendarItem[];
  view: CalendarViewMode;
  anchor?: Date | undefined;
  /** Absent on a block: a page inside a section does not navigate months. */
  onAnchorChange?: ((next: Date) => void) | undefined;
  timezone: string;
  empty: string;
}) {
  if (view === 'agenda') {
    return <Agenda items={items} timezone={timezone} empty={empty} />;
  }

  return (
    <Grid
      items={items}
      view={view}
      anchor={anchor ?? new Date()}
      onAnchorChange={onAnchorChange}
      timezone={timezone}
      empty={empty}
    />
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
  compact = false,
}: {
  item: CalendarItem;
  timezone: string;
  compact?: boolean;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();

  const allDay = item.allDay === true;
  const utc = moment(item.startsAt, { time: !allDay });

  // An all-day entry has no time to convert, so showing a second line for it would be inventing a
  // difference; a timed one always shows both, even when the two read the same, because a reader
  // has to be able to tell which is which.
  const local = allDay ? '' : moment(item.startsAt, { timeZone: timezone });
  const title = read(item.title);
  const summary = read(item.description);

  return (
    <div className="flex flex-col gap-1">
      <div className="text-muted-foreground flex flex-wrap items-baseline gap-2 text-xs">
        <time className="tabular-nums" dateTime={item.startsAt ?? undefined}>
          {t('calendar.utc', { at: utc })}
        </time>
        {local === '' ? null : <span className="tabular-nums">{t('calendar.local', { at: local })}</span>}
        {item.kind === undefined || item.kind === '' ? null : (
          <Badge variant="flat" color="gray" text={item.kind} />
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
}: {
  items: readonly CalendarItem[];
  timezone: string;
  empty: string;
}) {
  if (items.length === 0) {
    return <Nothing empty={empty} />;
  }

  return (
    <ul className="flex flex-col divide-y">
      {items.map((item) => (
        <li key={item.id} className="py-3 first:pt-0 last:pb-0">
          <Entry item={item} timezone={timezone} />
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
  onAnchorChange,
  timezone,
  empty,
}: {
  items: readonly CalendarItem[];
  view: Exclude<CalendarViewMode, 'agenda'>;
  anchor: Date;
  onAnchorChange?: ((next: Date) => void) | undefined;
  timezone: string;
  empty: string;
}) {
  const { t, i18n } = useTranslation();

  const squares = calendarDays(view, anchor);
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

  const heading = new Intl.DateTimeFormat(i18n.language, {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(anchor);

  const step = (direction: -1 | 1) => {
    const next = new Date(anchor);
    if (view === 'month') {
      next.setUTCMonth(next.getUTCMonth() + direction);
    } else {
      next.setUTCDate(next.getUTCDate() + direction * 7);
    }
    onAnchorChange?.(next);
  };

  return (
    <div className="flex flex-col gap-4">
      {onAnchorChange === undefined ? null : (
        <div className="flex items-center justify-between gap-4">
          <Button variant="ghost" size="sm" onClick={() => step(-1)} aria-label={t('calendar.previous')}>
            <ChevronLeft aria-hidden className="size-4" />
          </Button>
          <span className="font-medium">{heading}</span>
          <Button variant="ghost" size="sm" onClick={() => step(1)} aria-label={t('calendar.next')}>
            <ChevronRight aria-hidden className="size-4" />
          </Button>
        </div>
      )}

      {items.length === 0 ? <Nothing empty={empty} /> : null}

      {/* Seven columns from `sm` up and two on a phone, where seven squares of forty pixels are a
          grid nobody can read. The day's own number stays visible in both. */}
      <div className="grid grid-cols-2 gap-px sm:grid-cols-7" role="grid">
        {squares.map((day) => {
          const key = dayKey(day);
          const inMonth = view === 'week' || day.getUTCMonth() === anchor.getUTCMonth();
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
                <Entry key={item.id} item={item} timezone={timezone} compact />
              ))}
            </div>
          );
        })}
      </div>
    </div>
  );
}
