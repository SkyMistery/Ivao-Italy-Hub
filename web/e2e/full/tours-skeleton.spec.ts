import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test } from '@playwright/test';

import { englishCommon } from '../locales';

import { readInEnglish, signIn, whileWaitingFor } from './bench';

/**
 * The skeleton of the tours (M2, T5), through the real screens, registered from the module manifest: the settings
 * are changed and read back after a reload, and the lists and forms of the aircraft data open. Creating a
 * profile is proved by the integration tests: the bench has no aircraft types until the reference data job
 * runs at night, and a closed field has nothing to offer without them.
 *
 * The bench signs in as the web team, which reaches every department and so holds the permissions of the
 * tours on the base department too.
 */

const flightops = englishFlightOps();

test('the tours section opens, and its settings are saved and read back', async ({ page, context }) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the settings
  await page.goto('/staff/tours/settings');
  await expect(page.getByRole('heading', { name: flightops.settings.title })).toBeVisible();

  const fixed = page.locator('[id="durationFixedMinutes"]');
  const next = (Number(await fixed.inputValue()) % 60) + 1;
  await fixed.fill(String(next));

  await whileWaitingFor(page, 'PUT', '/api/modules/flightops/settings', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });

  await page.reload();
  await expect(page.locator('[id="durationFixedMinutes"]')).toHaveValue(String(next));

  // ---------------------------------------------------------------- the aircraft data
  await page.goto('/staff/tours/aircraft-profiles');
  await expect(page.getByRole('heading', { name: flightops.aircraftProfiles.title })).toBeVisible();

  await page.goto('/staff/tours/aircraft-profiles/new');
  await expect(page.getByRole('heading', { name: flightops.aircraftProfiles.create })).toBeVisible();

  await page.goto('/staff/tours/aircraft-groups/new');
  await expect(page.getByRole('heading', { name: flightops.aircraftGroups.create })).toBeVisible();
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishFlightOps() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
  ) as {
    settings: { title: string };
    aircraftProfiles: { title: string; create: string };
    aircraftGroups: { create: string };
  };
}
