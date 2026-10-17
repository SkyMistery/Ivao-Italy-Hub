import { expect, test } from '@playwright/test';

import { staffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The connection check of the super administrator (note `2026-10-16-la-prova-dei-collegamenti`), in a browser: the entry of
 * the administration's menu, the button, and the two answers in words. The server is stubbed — what it answers is
 * `ConnectionCheckTests`' business, on a real MariaDB — and a member of staff who is not a super administrator does not
 * reach the page.
 */

const words = englishCommon.connections;

const superadmin = {
  ...staffBootstrap,
  user: { ...staffBootstrap.user, isSuperadmin: true },
};

test('a super administrator tests the connections and reads the answer in words', async ({ page }) => {
  await stubTheApiAsStaff(page, superadmin);

  let asked = 0;
  await page.route('**/api/admin/diagnostics/connections', (route) => {
    asked += 1;
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        database: {
          outcome: 'Ok',
          error: null,
          milliseconds: 7,
          serverVersion: '11.4.10-MariaDB',
          migrations: [{ context: 'HubDbContext', latest: '20261001_Latest', pending: [] }],
        },
        archive: { outcome: 'Failed', error: 'AccessDenied', milliseconds: 3, latestSessionStart: null },
      }),
    });
  });

  await page.goto('/staff/admin/connections');
  await expect(page.getByRole('heading', { name: words.title, level: 1 })).toBeVisible();
  await expect(page.getByRole('link', { name: words.title }).first()).toBeVisible();
  expect(asked, 'nothing is asked before the button').toBe(0);

  await page.getByRole('button', { name: words.run }).click();

  const database = page.getByTestId('connection-database');
  await expect(database.getByText(words.outcomes.Ok)).toBeVisible();
  await expect(database.getByText('Server: 11.4.10-MariaDB')).toBeVisible();
  await expect(database.getByText(words.migrations.upToDate)).toBeVisible();

  const archive = page.getByTestId('connection-archive');
  await expect(archive.getByText(words.outcomes.Failed)).toBeVisible();
  await expect(archive.getByText(words.errors.AccessDenied)).toBeVisible();
  expect(asked).toBe(1);
});

test('a member of staff who is not a super administrator does not reach the page', async ({ page }) => {
  await stubTheApiAsStaff(page);
  await page.goto('/staff/admin/connections');

  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByRole('link', { name: words.title })).toHaveCount(0);
});
