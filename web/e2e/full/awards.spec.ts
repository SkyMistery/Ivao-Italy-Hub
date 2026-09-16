import { expect, test } from '@playwright/test';

import { englishCommon } from '../locales';

import { choose, department, readInEnglish, signIn, whileWaitingFor, writeInBothLanguages } from './bench';

/**
 * The awards of the core (M2, T4b), through the real screens: an award written into the catalogue of a
 * department, assigned by hand in the register, found there by its name, revoked, and deleted once
 * nobody holds it. The queue is proved by the integration tests, because only a row of a module fills
 * it and this build has none yet.
 *
 * The bench signs in as the web team, which reaches every department and holds the global
 * `Awards.Assign`: the two halves — the department that writes and whoever assigns — in one identity.
 */

const awards = englishCommon.awards;
const register = englishCommon.awardAssignments;

/** A name of this run: the bench database is not thrown away between runs. */
const stamp = Date.now().toString(36);
const name = { en: `Bench award ${stamp}`, it: `Award del banco ${stamp}` };

test('an award is written, assigned by hand, found in the register, revoked and deleted', async ({
  page,
  context,
}) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the catalogue
  await page.goto(`/staff/awards/new?department=${department.toUpperCase()}`);
  await expect(page.getByRole('heading', { name: awards.create })).toBeVisible();

  await writeInBothLanguages(page.locator('form'), awards.fields.name, 'name', name);

  await whileWaitingFor(page, 'POST', '/api/awards', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });

  await expect(page).toHaveURL(/\/staff\/awards/);
  await expect(page.getByText(name.en, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- the register
  await page.goto('/staff/awards/assignments/new');
  await expect(page.getByRole('heading', { name: register.create })).toBeVisible();

  await choose(page, register.fields.awardId, name.en);
  await page.locator('[id="vid"]').fill('780049');
  await page.locator('[id="reason"]').fill(`Bench reason ${stamp}`);

  await whileWaitingFor(page, 'POST', '/api/award-assignments', async () => {
    await page.getByRole('button', { name: register.assign }).click();
  });

  await expect(page).toHaveURL(/\/staff\/awards\/assignments(\?|$)/);
  const row = page.getByRole('row').filter({ hasText: `Bench reason ${stamp}` });
  await expect(row.getByText(name.en, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- revoked, then deleted
  await row.getByRole('link', { name: englishCommon.common.edit }).click();
  await page.getByRole('button', { name: register.revoke }).click();

  await whileWaitingFor(page, 'DELETE', '/api/award-assignments/', async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: register.revoke }).click();
  });

  await expect(page).toHaveURL(/\/staff\/awards\/assignments(\?|$)/);
  await expect(page.getByText(`Bench reason ${stamp}`)).toHaveCount(0);

  // Nobody holds it any more, so it may go — which is also the bench taking its row back.
  await page.goto(`/staff/awards?department=${department.toUpperCase()}`);
  await page
    .getByRole('row')
    .filter({ hasText: name.en })
    .getByRole('link', { name: englishCommon.common.edit })
    .click();
  await page.getByRole('button', { name: englishCommon.common.delete }).click();

  await whileWaitingFor(page, 'DELETE', '/api/awards/', async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: englishCommon.common.delete }).click();
  });

  await expect(page.getByText(name.en, { exact: true })).toHaveCount(0);
});
