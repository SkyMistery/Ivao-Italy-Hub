import { expect, test } from '@playwright/test';

import { stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The back office is reachable: a list, and the form behind its buttons.
 *
 * This exists because of the second failure of the same family as the first. The detail routes were
 * children of the list routes, and no list rendered an `Outlet`, so clicking "new link" changed the
 * address and left the list on the screen — every form in the hub was unreachable in a browser
 * while 76 unit tests and 353 server tests stayed green. Nothing exercised how the routes compose,
 * exactly as nothing had exercised how the providers compose.
 *
 * So the assertions here are deliberately about **arriving somewhere**, not about what a screen
 * looks like: the address changed *and* the thing it promised is on the page. Either half alone is
 * what let this through.
 */

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the links list opens for a coordinator of that department', async ({ page }) => {
  await page.goto('/staff/ed/links');

  await expect(page.getByText('Something went wrong!')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: englishCommon.links.title })).toBeVisible();

  // The row the API answered with: proof the list rendered its data and not just its frame.
  // By cell, because the URL of the same row also contains the word.
  await expect(page.getByRole('cell', { name: 'Discord', exact: true })).toBeVisible();
});

test('new link reaches the form, and not just the address bar', async ({ page }) => {
  await page.goto('/staff/ed/links');
  await page.getByRole('link', { name: englishCommon.links.create }).first().click();

  await expect(page).toHaveURL(/\/staff\/ed\/links\/new/);

  // The half that was missing. The address changed all along; what never happened was the form
  // appearing, because the list route had no outlet to draw its child into.
  await expect(page.getByLabel(englishCommon.links.fields.url)).toBeVisible();

  // And the list is gone rather than sitting above the form. Asserted on the row, not on the word
  // "Links": that still appears in the breadcrumb of the form, which is correct.
  await expect(page.getByRole('cell', { name: 'Discord', exact: true })).toHaveCount(0);
});

test('edit reaches the form of that row', async ({ page }) => {
  await page.goto('/staff/ed/links');
  await page.getByRole('link', { name: englishCommon.common.edit }).first().click();

  await expect(page).toHaveURL(/\/staff\/ed\/links\/7/);
  await expect(page.getByLabel(englishCommon.links.fields.url)).toBeVisible();
});

test('a department the member does not reach is a refusal, not an empty table', async ({ page }) => {
  await page.goto('/staff/fod/links');

  await expect(page).toHaveURL(/\/forbidden/);
});
