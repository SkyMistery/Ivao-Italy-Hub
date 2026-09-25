import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, readInEnglish, signIn, whileWaitingFor } from './bench';

/**
 * The skeleton of the training (M3, A4), through the real screens registered from the module manifest: the section is
 * offered in the back office, and its settings are changed and read back after a reload. The bench signs in as the web
 * team, which reaches every department and so holds the training's permissions on its base department too; the bench's
 * trainer holds what `division.json` gives a trainer, and the settings are not part of it.
 *
 * The settings are put back as they were in a `finally`: the bench survives between runs.
 */

const training = englishTraining();
const settingsUrl = '/api/modules/training/settings';
const asTheClientDoes = { 'X-Requested-With': 'hub' };

test('the training section is offered, and its settings are saved and read back', async ({
  page,
  context,
}) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const before = await context.request.get(settingsUrl);
  expect(before.status(), await before.text()).toBe(200);
  const saved: unknown = await before.json();

  try {
    // Offered where every back office screen is offered: the palette reads the destinations the sidebar draws.
    await page.goto('/staff/links');
    await expect(page.getByRole('heading', { name: englishCommon.links.title })).toBeVisible();
    await page.keyboard.press('Control+k');

    const palette = page.getByRole('dialog');
    await palette.getByText(`${training.nav.section} — ${training.nav.settings}`).click();
    await expect(page.getByRole('heading', { name: training.settings.title })).toBeVisible();

    const cooldown = page.locator('[id="cooldownDays"]');
    const next = (Number(await cooldown.inputValue()) % 30) + 1;
    await cooldown.fill(String(next));

    await whileWaitingFor(page, 'PUT', settingsUrl, async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    await expect(page.getByText(training.settings.saved)).toBeVisible();

    await page.reload();
    await expect(page.locator('[id="cooldownDays"]')).toHaveValue(String(next));
  } finally {
    const putBack = await context.request.put(settingsUrl, { headers: asTheClientDoes, data: saved });
    expect(putBack.status(), await putBack.text()).toBe(200);
  }
});

test('the trainer of the bench views the trainings and does not manage their settings', async ({
  browser,
}) => {
  const context = await browser.newContext({ baseURL: benchUrl });

  const signedIn = await context.request.post('/e2e/signin?as=trainer');
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  const me = (await (await context.request.get('/api/me')).json()) as {
    permissions: { name: string; department: string | null }[];
  };
  const held = me.permissions
    .filter((permission) => permission.department === 'TD')
    .map((permission) => permission.name);

  expect(held).toEqual(expect.arrayContaining(['Training.View', 'Training.ManageExams']));
  expect(held).not.toContain('Training.ManageSettings');

  expect((await context.request.get(settingsUrl)).status()).toBe(403);

  await context.close();
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishTraining() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/training.json', import.meta.url)), 'utf8'),
  ) as {
    nav: { section: string; settings: string };
    settings: { title: string; saved: string };
  };
}
