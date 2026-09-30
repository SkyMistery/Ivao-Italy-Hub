import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test } from '@playwright/test';

import { englishCommon, englishSeed } from '../locales';

import { benchUrl, readInEnglish, whileWaitingFor } from './bench';

/**
 * The skeleton of the events (M4, E2), through the real screens registered from the module manifest, as the bench's
 * coordinator of the events (`?as=events`, E1): the section is offered in the back office, and a kind preset and a setting
 * are saved and read back after a reload. The coordinator holds only what `division.json` gives the events department,
 * through the grants of the position — the web master of the bench reaches every department and would pass with any.
 *
 * The settings are put back as they were in a `finally`: the bench survives between runs.
 */

const events = englishEvents();
const settingsUrl = '/api/modules/events/settings';
const asTheClientDoes = { 'X-Requested-With': 'hub' };

interface Settings {
  readonly kindPresets: readonly { readonly kind: string; readonly publicSlots: boolean }[];
  readonly bookingGapMinutes: number;
}

test('the coordinator of the events finds the section, and saves a preset and a setting it reads back', async ({
  browser,
}) => {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post('/e2e/signin?as=events');
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  const before = await context.request.get(settingsUrl);
  expect(before.status(), await before.text()).toBe(200);
  const saved = (await before.json()) as Settings;

  // The new preset goes after the ones a bench may already hold.
  const row = saved.kindPresets.length;

  const page = await context.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  try {
    // Offered where every back office screen is offered: the palette reads the destinations the sidebar draws.
    await page.goto('/staff/links');
    await expect(page.getByRole('heading', { name: englishCommon.links.title })).toBeVisible();
    await page.keyboard.press('Control+k');

    const palette = page.getByRole('dialog');
    await palette.getByText(`${events.nav.section} — ${events.nav.settings}`).click();
    await expect(page.getByRole('heading', { name: events.settings.title })).toBeVisible();

    // A kind of the division's calendar, chosen by its word: the form offers the calendar's kinds, never a typed key.
    await page.getByRole('button', { name: englishCommon.form.addEntry }).click();
    await page.locator(`[id="kindPresets.${row}.kind"]`).click();
    await page.getByRole('option', { name: englishSeed.seed.calendarKinds.rfe, exact: true }).click();
    await page.locator(`[id="kindPresets.${row}.publicSlots"]`).click();

    const gap = page.locator('[id="bookingGapMinutes"]');
    const next = (Number(await gap.inputValue()) % 60) + 1;
    await gap.fill(String(next));

    await whileWaitingFor(page, 'PUT', settingsUrl, async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    await expect(page.getByText(events.settings.saved)).toBeVisible();

    await page.reload();
    await expect(page.locator('[id="bookingGapMinutes"]')).toHaveValue(String(next));
    await expect(page.locator(`[id="kindPresets.${row}.kind"]`)).toContainText(
      englishSeed.seed.calendarKinds.rfe,
    );
    await expect(page.locator(`[id="kindPresets.${row}.publicSlots"]`)).toBeChecked();

    // And the server keeps the key of the kind, not its word.
    const after = (await (await context.request.get(settingsUrl)).json()) as Settings;
    expect(after.kindPresets[row]).toMatchObject({ kind: 'rfe', publicSlots: true });
  } finally {
    const putBack = await context.request.put(settingsUrl, { headers: asTheClientDoes, data: saved });
    expect(putBack.status(), await putBack.text()).toBe(200);
    await context.close();
  }
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    nav: { section: string; settings: string };
    settings: { title: string; saved: string };
  };
}
