import { expect, test } from '@playwright/test';

import { staffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The free and busy calendar of a date field (note 2026-11-18-il-calendario-libero-e-occupato), in a browser and under the
 * real CSP: the field of the gallery's form opens the picker in its popover, the month shows the busy day with the dots of its
 * kinds and the legend, the hour a blocking kind takes cannot be chosen, and the hour chosen is written in the box in the
 * division's order.
 *
 * In a browser because it is a popover, a grid of days and a layout of dots, which jsdom does not lay out.
 */

const words = englishCommon.form.busy;

const kinds = [
  ...staffBootstrap.calendarKinds,
  { key: 'event', label: { en: 'Event', it: 'Evento' }, colour: 'blue', blocks: true },
];

test('a date is chosen on the free and busy calendar, and a blocked hour cannot be', async ({ page }) => {
  await stubTheApiAsStaff(page, { ...staffBootstrap, calendarKinds: kinds });

  // The fifteenth of whatever month the picker opens on, from 14:00 to 16:00, of a kind that blocks; and a meeting at 09:00.
  let busyDay = '';
  await page.route('**/api/calendar/busy**', (route) => {
    const from = new Date(new URL(route.request().url()).searchParams.get('fromUtc') ?? '');
    const month = new Date(
      Date.UTC(from.getUTCFullYear(), from.getUTCMonth() + (from.getUTCDate() > 1 ? 1 : 0), 15),
    );
    busyDay = month.toISOString().slice(0, 10);

    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          startsAtUtc: `${busyDay}T09:00:00Z`,
          endsAtUtc: `${busyDay}T10:00:00Z`,
          allDay: false,
          kind: 'meeting',
          blocks: false,
          title: { en: 'Staff meeting', it: 'Riunione dello staff' },
        },
        {
          startsAtUtc: `${busyDay}T14:00:00Z`,
          endsAtUtc: `${busyDay}T16:00:00Z`,
          allDay: false,
          kind: 'event',
          blocks: true,
          title: { en: 'Fly-in', it: 'Fly-in' },
        },
      ]),
    });
  });

  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/staff/admin/ui-kit');

  const form = page.locator('[data-ui-kit="SchemaForm"]');
  await form.getByRole('button', { name: new RegExp(words.open) }).click();

  const picker = page.getByTestId('busy-calendar');
  await expect(picker).toBeVisible();

  const legend = picker.getByRole('list', { name: words.legend });
  await expect(legend).toContainText('Event');
  await expect(legend).toContainText(words.blocks);
  await expect(legend).toContainText('Meeting');

  // The busy day says so, its kinds in the vocabulary's order, and carries a dot of each.
  const day = picker.getByRole('button', { name: /busy: Meeting, Event/ });
  await expect(day).toHaveCount(1);
  await expect(day.locator('xpath=..').locator('span.rounded-full')).toHaveCount(2);

  await day.click();

  await expect(picker.getByRole('button', { name: '14:00 UTC, blocked' })).toBeDisabled();
  await expect(picker.getByRole('button', { name: '15:00 UTC, blocked' })).toBeDisabled();
  await expect(picker.getByText('Fly-in')).toBeVisible();

  await page.screenshot({ path: test.info().outputPath('busy-calendar.png') });

  await picker.getByRole('button', { name: '11:00 UTC, Free' }).click();

  // The picker closes, and the box holds the day and the hour in the division's order (dd/mm/yyyy by default).
  await expect(picker).toBeHidden();
  const [year, month, date] = busyDay.split('-');
  await expect(form.getByRole('textbox', { name: /Meets at/ })).toHaveValue(`${date}/${month}/${year} 11:00`);
});
