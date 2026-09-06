import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { renderWithProviders } from '../../test/harness';

import { CalendarView } from './CalendarView';
import { calendarDays, calendarWindow, type CalendarItem } from './calendar';

/**
 * What the calendar says. What it *looks like* — a grid of seven columns rather than a column of
 * thirty-five — is measured in a browser, because jsdom does no layout (`web/e2e/calendar.spec.ts`).
 *
 * ⚠️ The zone in these tests is **never UTC**, and that is the whole point of one of them: in M0 a
 * fixture with `timezone: "UTC"` made the two lines of every date identical, and a screen showing
 * UTC twice would have passed unnoticed. It was one of the three false alarms of HANDOFF §13, and
 * this is the test that would have caught the real version of it.
 */

/** Two in the afternoon UTC on a fixed day, so no test depends on when it runs. */
const AFTERNOON = '2026-09-15T14:00:00.000Z';

const items: CalendarItem[] = [
  {
    id: 1,
    kind: 'meeting',
    title: { en: 'Staff meeting', it: 'Riunione dello staff' },
    startsAt: AFTERNOON,
    allDay: false,
  },
  {
    id: 2,
    kind: 'deadline',
    title: { en: 'Applications close', it: 'Chiusura delle candidature' },
    startsAt: '2026-09-20T00:00:00.000Z',
    allDay: true,
  },
];

describe('CalendarView', () => {
  it('shows an instant in UTC and in the division time, and says which is which', () => {
    // Asia/Tokyo, which is nine hours ahead: an afternoon in UTC is late at night there, so the
    // two readings cannot accidentally be the same string.
    renderWithProviders(<CalendarView items={items} view="agenda" timezone="Asia/Tokyo" empty="Nothing" />);

    // Both are on screen, and each is labelled: two times side by side with no labels is a screen
    // that makes a reader guess which one is theirs (plan §9.5).
    // Two in the afternoon in UTC is eleven at night in Tokyo, and the nine hours between them are
    // what says the conversion happened at all rather than the same string being printed twice.
    expect(screen.getByText(/2:00\sPM UTC/)).toBeInTheDocument();
    expect(screen.getByText(/11:00\sPM local/)).toBeInTheDocument();
  });

  it('shows no local time for an all-day entry, because there is none to show', () => {
    renderWithProviders(<CalendarView items={items} view="agenda" timezone="Asia/Tokyo" empty="Nothing" />);

    // The deadline is a day and not an instant. A second line for it would be inventing a
    // difference — and in Tokyo it would move the entry to the previous day, which is a lie.
    expect(screen.getAllByText(/local/)).toHaveLength(1);
  });

  it('says so when there is nothing, rather than drawing an empty list', () => {
    renderWithProviders(
      <CalendarView items={[]} view="agenda" timezone="Asia/Tokyo" empty="Nothing at all" />,
    );

    expect(screen.getByText('Nothing at all')).toBeInTheDocument();
  });

  it('puts an entry in the square of its UTC day', () => {
    const anchor = new Date('2026-09-15T00:00:00.000Z');

    renderWithProviders(
      <CalendarView items={items} view="month" anchor={anchor} timezone="Asia/Tokyo" empty="Nothing" />,
    );

    // September 2026 starts on a Tuesday, so the grid opens on Monday the 31st of August and the
    // month has five rows. What is asserted is that the entry is drawn at all and that the square
    // for its day exists; where the square sits on screen is a measurement and lives in the browser.
    expect(screen.getByText('Staff meeting')).toBeInTheDocument();
    expect(screen.getByText('15')).toBeInTheDocument();
  });
});

describe('the days a grid draws, and the window asked for them', () => {
  it('covers a whole month in complete weeks, starting on a Monday', () => {
    // The 28th on purpose: a window worked out from the anchor rather than from the squares would
    // ask for the last week of the month and draw the first three empty. That was a real bug in
    // this phase, found by writing this test.
    const days = calendarDays('month', new Date('2026-09-28T00:00:00.000Z'));

    expect(days).toHaveLength(35);
    expect(days[0]!.toISOString().slice(0, 10)).toBe('2026-08-31');
    expect(days[0]!.getUTCDay()).toBe(1);
    expect(days.at(-1)!.toISOString().slice(0, 10)).toBe('2026-10-04');
  });

  it('asks for exactly the days it draws, whichever day of the month it is opened on', () => {
    for (const on of ['2026-09-01', '2026-09-15', '2026-09-28']) {
      const anchor = new Date(`${on}T00:00:00.000Z`);
      const days = calendarDays('month', anchor);
      const window = calendarWindow('month', anchor);

      // The window starts at the first square and ends the day after the last one, so every square
      // has been asked for and none has been asked for twice.
      expect(window.from).toBe(days[0]!.toISOString());
      expect(new Date(window.to).getTime() - days.at(-1)!.getTime()).toBe(24 * 60 * 60 * 1000);
    }
  });

  it('draws a week as the seven days from the Monday on or before', () => {
    const days = calendarDays('week', new Date('2026-09-17T00:00:00.000Z'));

    expect(days).toHaveLength(7);
    expect(days[0]!.toISOString().slice(0, 10)).toBe('2026-09-14');
  });
});
