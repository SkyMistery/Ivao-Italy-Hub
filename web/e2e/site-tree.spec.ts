import { expect, test } from '@playwright/test';

import { siteStaffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The tree of the site (design `13-design-struttura-del-sito.md` §3.2): the pages of `/staff/content` shown where they
 * are, with the pages written in code among them, marked and with nothing to click; «New page here» opens the form of a
 * page already under the row it was clicked on. The stub of `/api/content/pages` (fixtures) has `/about` and
 * `/about/team`; the bootstrap carries the core's screens.
 */

const content = englishCommon.content;

test('the pages are the tree of the site, with the pages of the application among them', async ({ page }) => {
  await stubTheApiAsStaff(page, siteStaffBootstrap);

  await page.goto('/staff/content?kind=Page');

  const rows = page.getByRole('row');
  // In the order of the address: the front page, then `/about` and the page under it, then the calendar.
  await expect(rows.filter({ hasText: '/about/team' })).toBeVisible();
  const paths = await rows.locator('td:nth-child(2)').allInnerTexts();
  expect(paths.slice(0, 4)).toEqual(['/', '/about', '/about/team', '/calendar']);

  // A page of the application says so, and offers nothing.
  const calendar = rows.filter({ hasText: '/calendar' });
  await expect(calendar.getByText(content.options.source.Screen)).toBeVisible();
  await expect(calendar.getByRole('link')).toHaveCount(0);

  // The list is one click away, and has the search box the tree has not.
  await page.getByLabel(content.view.label, { exact: true }).click();
  await page.getByRole('option', { name: content.view.list }).click();
  await expect(page).toHaveURL(/view=list/);
});

test('«New page here» opens the form of a page already under that row', async ({ page }) => {
  await stubTheApiAsStaff(page, siteStaffBootstrap);

  await page.goto('/staff/content?kind=Page');

  await page
    .getByRole('row')
    .filter({ hasText: '/about/team' })
    .getByRole('link', { name: content.tree.newHere })
    .click();

  await expect(page).toHaveURL(/\/staff\/content\/new\?.*parent=2/);
  await expect(page.getByLabel(content.fields.parentId, { exact: true })).toContainText('/about/team');
});
