import { Badge, Button, Calendar } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Lock } from 'lucide-react';
import { createContext, useContext, useState, type HTMLAttributes } from 'react';
import { useTranslation } from 'react-i18next';

import type { CalendarKind } from '../api/bootstrap';
import { formatMoment } from '../i18n/useMoment';
import { useLocalized } from '../i18n/useLocalized';
import { calendarKindColour } from '../ui/calendar';

import {
  DOT_CLASS,
  busyDays,
  busyQuery,
  busyHours,
  dayKeyOf,
  dayKeyOfPicker,
  hourInstant,
  legendOf,
  pickerDay,
  stretchesOn,
  type BusyDay,
  type BusyStretch,
  type DayKey,
  type FormCalendar,
} from './busy';

/** What the cells of the month read while they draw: the busy days and the vocabulary their colours come from. */
const BusyDays = createContext<{ days: Map<DayKey, BusyDay>; kinds: readonly CalendarKind[] }>({
  days: new Map(),
  kinds: [],
});

/**
 * The cell of a day, with a dot of each kind that takes some of it, at most three. The cell and its button stay Atmosphere's: only
 * the dots are added underneath, so the picker keeps the design system's look and its keyboard. Declared once, out here, so the
 * picker does not remount every cell — and lose the focus of the keyboard — each time it draws.
 */
function BusyDayCell(
  props: HTMLAttributes<HTMLTableCellElement> & { day: { date: Date }; modifiers: unknown },
) {
  // The picker's own facts about the day are not attributes of a cell: they stay out of the `td`.
  const { day, modifiers, children, ...cell } = props;
  void modifiers;
  const { days, kinds: vocabulary } = useContext(BusyDays);
  const kinds = days.get(dayKeyOfPicker(day.date))?.kinds ?? [];

  return (
    <td {...cell}>
      {children}
      {kinds.length === 0 ? null : (
        <span
          aria-hidden
          className="pointer-events-none absolute inset-x-0 bottom-0.5 flex justify-center gap-0.5"
        >
          {kinds.slice(0, 3).map((kind) => (
            <span
              key={kind}
              className={`size-1.5 rounded-full ${DOT_CLASS[calendarKindColour(kind, vocabulary)]}`}
            />
          ))}
        </span>
      )}
    </td>
  );
}

/**
 * The picker of a `calendar: true` date field (note 2026-11-18-il-calendario-libero-e-occupato): a month of Atmosphere's
 * `Calendar` with the free days in green and the busy ones marked with a dot of each kind's colour, the legend of what the month
 * shows, and — for a field with a time — the twenty-four hours of the day chosen, the busy ones marked and the blocked ones not
 * to be chosen. Days and hours are UTC (Carmine's answer 3); the field writes the division's time beside the value.
 *
 * It is a piece of the form's instant field, not a component of the closed list: it draws inside the field's popover and
 * nowhere else.
 */
export function BusyCalendar({
  value,
  withTime,
  calendar,
  onPick,
}: {
  /** The instant the field holds, ISO UTC, or empty. */
  value: string;
  withTime: boolean;
  calendar: FormCalendar;
  /** Called with the instant chosen: a day for a date field, a day and an hour for an instant. */
  onPick: (iso: string) => void;
}) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const held = value === '' ? null : new Date(value);
  const heldDay = held === null || Number.isNaN(held.getTime()) ? null : dayKeyOf(held);
  const [day, setDay] = useState<DayKey | null>(heldDay);
  const [month, setMonth] = useState(() => {
    const anchor = pickerDay(heldDay ?? dayKeyOf(new Date()));
    return new Date(anchor.getFullYear(), anchor.getMonth(), 1);
  });

  // The grid's weeks in UTC, asked of the same function the calendar screen asks, so the window covers every square drawn.
  const utcMonth = new Date(Date.UTC(month.getFullYear(), month.getMonth(), 1));
  const busy = useQuery(busyQuery(utcMonth, calendar.except));
  const stretches = busy.data ?? [];
  const days = busyDays(stretches, calendar.kinds);

  const isFree = (date: Date) => !days.has(dayKeyOfPicker(date));
  // An instant cannot be put on a day with no free hour; a day without a time takes the whole day, so a day any blocking entry
  // touches cannot be chosen — the same answer the server gives (`BlockingAsync` with `allDay`).
  const isBlocked = (date: Date) => {
    const found = days.get(dayKeyOfPicker(date));
    return found !== undefined && (withTime ? found.blocked : found.blocking);
  };

  const kindLabel = (kind: string) => read(calendar.kinds.find((word) => word.key === kind)?.label) || kind;
  const longDay = (date: Date) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'full' }).format(date);

  const choose = (key: DayKey) => {
    setDay(key);
    if (!withTime) {
      onPick(`${key}T00:00:00Z`);
    }
  };

  const legend = legendOf(stretches, calendar.kinds);

  return (
    <div className="flex flex-col gap-3" data-testid="busy-calendar">
      <BusyDays.Provider value={{ days, kinds: calendar.kinds }}>
        <Calendar
          mode="single"
          weekStartsOn={1}
          month={month}
          onMonthChange={setMonth}
          selected={day === null ? undefined : pickerDay(day)}
          onSelect={(date: Date | undefined) => {
            if (date !== undefined) {
              choose(dayKeyOfPicker(date));
            }
          }}
          disabled={isBlocked}
          modifiers={{ free: isFree }}
          modifiersClassNames={{ free: 'bg-green-100 dark:bg-green-950 rounded-md' }}
          formatters={{
            formatCaption: (date: Date) =>
              new Intl.DateTimeFormat(i18n.language, { month: 'long', year: 'numeric' }).format(date),
            formatWeekdayName: (date: Date) =>
              new Intl.DateTimeFormat(i18n.language, { weekday: 'narrow' }).format(date),
          }}
          labels={{
            labelDayButton: (date: Date) => {
              const found = days.get(dayKeyOfPicker(date));
              const state =
                found === undefined
                  ? t('form.busy.free')
                  : isBlocked(date)
                    ? t('form.busy.blockedDay')
                    : t('form.busy.busyDay', { kinds: found.kinds.map(kindLabel).join(', ') });
              return `${longDay(date)}, ${state}`;
            },
            labelPrevious: () => t('form.busy.previousMonth'),
            labelNext: () => t('form.busy.nextMonth'),
          }}
          components={{ Day: BusyDayCell }}
        />
      </BusyDays.Provider>

      <ul className="flex flex-wrap items-center gap-2 text-xs" aria-label={t('form.busy.legend')}>
        <li className="flex items-center gap-1">
          <span aria-hidden className="size-3 rounded-sm bg-green-100 dark:bg-green-950" />
          {t('form.busy.free')}
        </li>
        {legend.map((entry) => (
          <li key={entry.kind} className="flex items-center gap-1">
            <span aria-hidden className={`size-2 rounded-full ${DOT_CLASS[entry.colour]}`} />
            {kindLabel(entry.kind)}
            {entry.blocks ? (
              <span className="text-muted-foreground flex items-center gap-0.5">
                <Lock aria-hidden className="size-3" />
                {t('form.busy.blocks')}
              </span>
            ) : null}
          </li>
        ))}
        {busy.isError ? <li className="text-muted-foreground">{t('form.busy.unknown')}</li> : null}
      </ul>

      {withTime && day !== null ? (
        <Hours
          day={day}
          stretches={stretches}
          kinds={calendar.kinds}
          chosen={held !== null && heldDay === day ? held.getUTCHours() : null}
          kindLabel={kindLabel}
          locale={i18n.language}
          onPick={(hour) => onPick(hourInstant(day, hour))}
        />
      ) : null}
    </div>
  );
}

/** The window of the hour: the twenty-four UTC hours of the day chosen, and what takes them. */
function Hours({
  day,
  stretches,
  kinds,
  chosen,
  kindLabel,
  locale,
  onPick,
}: {
  day: DayKey;
  stretches: readonly BusyStretch[];
  kinds: readonly CalendarKind[];
  chosen: number | null;
  kindLabel: (kind: string) => string;
  locale: string;
  onPick: (hour: number) => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const hours = busyHours(stretches, day);
  const lines = stretchesOn(stretches, day);
  const clock = (iso: string) => formatMoment(locale, iso, { date: false });

  return (
    <div className="flex flex-col gap-2">
      <span className="text-sm font-medium">{t('form.busy.hours')}</span>
      <div className="grid grid-cols-6 gap-1" role="group" aria-label={t('form.busy.hours')}>
        {hours.map((hour) => {
          const at = clock(hourInstant(day, hour.hour));
          const state = hour.blocked
            ? t('form.busy.blockedHour')
            : hour.stretches.length > 0
              ? t('form.busy.busyHour', {
                  kinds: hour.stretches.map((stretch) => kindLabel(stretch.kind)).join(', '),
                })
              : t('form.busy.free');

          return (
            <Button
              key={hour.hour}
              type="button"
              size="sm"
              variant={chosen === hour.hour ? 'primary' : 'outline'}
              disabled={hour.blocked}
              aria-label={`${at} UTC, ${state}`}
              aria-pressed={chosen === hour.hour}
              className={
                hour.blocked
                  ? 'line-through'
                  : hour.stretches.length === 0 && chosen !== hour.hour
                    ? 'bg-green-100 dark:bg-green-950'
                    : ''
              }
              onClick={() => onPick(hour.hour)}
            >
              <span className="tabular-nums">{at}</span>
              {hour.stretches.length > 0 && !hour.blocked ? (
                <span aria-hidden className="ml-1 size-1.5 rounded-full bg-current opacity-60" />
              ) : null}
            </Button>
          );
        })}
      </div>

      {lines.length === 0 ? null : (
        <ul className="flex flex-col gap-1 text-xs">
          {lines.map((line, index) => (
            <li
              key={`${line.startsAtUtc}-${line.kind}-${index}`}
              className="flex flex-wrap items-center gap-2"
            >
              <span className="tabular-nums">
                {line.allDay
                  ? t('form.busy.allDay')
                  : `${clock(line.startsAtUtc)}–${clock(line.endsAtUtc)} UTC`}
              </span>
              <Badge
                variant="flat"
                color={calendarKindColour(line.kind, kinds)}
                text={kindLabel(line.kind)}
              />
              <span>{read(line.title)}</span>
              {line.blocks ? (
                <span className="text-muted-foreground flex items-center gap-0.5">
                  <Lock aria-hidden className="size-3" />
                  {t('form.busy.blocks')}
                </span>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
