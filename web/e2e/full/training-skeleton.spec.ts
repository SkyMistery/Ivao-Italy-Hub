import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, readInEnglish, signIn, whileWaitingFor, test } from './bench';

/**
 * The skeleton of the training (M3, A4), through the real screens registered from the module manifest: the section is
 * offered in the back office, and its settings are changed and read back after a reload. The bench signs in as the web
 * team, which reaches every department and so holds the training's permissions on its base department too; the bench's
 * trainer holds what `division.json` gives a trainer — viewing the trainings, and conducting the ones assigned to them (A7b)
 * —, and the settings are not part of it. A grant on one training alone, which the assignments of A7 wrote before A7b, is not
 * a power over the department, and a bench that survived from then may still have one: it is left out.
 *
 * The settings are put back as they were after the test (`afterwards`): the bench survives between runs.
 */

const training = englishTraining();
const settingsUrl = '/api/modules/training/settings';
const asTheClientDoes = { 'X-Requested-With': 'hub' };

test('the training section is offered, and its settings are saved and read back', async ({
  page,
  context,
  afterwards,
}) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const before = await context.request.get(settingsUrl);
  expect(before.status(), await before.text()).toBe(200);
  const saved: unknown = await before.json();

  afterwards(async () => {
    const putBack = await context.request.put(settingsUrl, { headers: asTheClientDoes, data: saved });
    expect(putBack.status(), await putBack.text()).toBe(200);
  });

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
});

test('the trainer of the bench views and conducts the trainings, and neither holds exams nor manages the settings', async ({
  browser,
}) => {
  const context = await browser.newContext({ baseURL: benchUrl });

  const signedIn = await context.request.post('/e2e/signin?as=trainer');
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  const me = (await (await context.request.get('/api/me')).json()) as {
    permissions: { name: string; department: string | null; resourceScope: string | null }[];
  };
  const held = me.permissions
    .filter((permission) => permission.department === 'TD' && permission.resourceScope === null)
    .map((permission) => permission.name);

  // Conducting reaches only the trainings assigned to them (A7b); an exam is assigned only to an examiner, never to a trainer
  // (the training department, 26 September 2026).
  expect([...new Set(held.filter((name) => name.startsWith('Training.')))]).toEqual([
    'Training.Conduct',
    'Training.View',
  ]);

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
