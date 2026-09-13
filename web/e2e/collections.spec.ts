import { expect, test } from '@playwright/test';

import { oneMediaDetail, stubTheAddressOfAPage, stubTheApiAsStaff, twoDocuments } from './fixtures';
import { englishCommon } from './locales';

/**
 * Collections and the library after G20 (note 2026-09-13-contenuti-centralizzati §3.3–3.4), in a
 * browser: the editor of a document says which published pages list it, and a file that is archived
 * says so and goes back into the library with one press. What the server decides — the index, the
 * refused delete, the new address — is proven by `CollectionsAndMediaTests`.
 */

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the editor of a document says where it appears', async ({ page }) => {
  await stubTheApiAsStaff(page);

  const document = {
    ...twoDocuments.items[0],
    summary: null,
    seo: null,
    templateId: null,
    body: { schemaVersion: 1, sections: [] },
    schemaVersion: 1,
    reviewNote: null,
    effectiveOn: null,
    reviewOn: null,
    retiredAt: null,
    supersededById: null,
    showFooter: true,
    publishedVersionId: 4,
    createdAt: '2026-09-04T12:00:00Z',
    createdBy: 111111,
    updatedBy: 111111,
    rowVersion: '2026-09-04T12:00:00',
  };

  await page.route('**/api/content/21', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(document) }),
  );
  await page.route('**/api/content/21/appears-in', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: 1, path: 'training/guides', ownerDepartment: 'TD', title: { en: 'Guides', it: 'Guide' } },
      ]),
    }),
  );
  await stubTheAddressOfAPage(page);

  await page.goto('/staff/content/21');

  await expect(page.getByText('Appears in: Guides (/training/guides)')).toBeVisible();
});

test('an archived file says so, and goes back into the library', async ({ page }) => {
  await stubTheApiAsStaff(page);

  let restored = false;
  await page.route('**/api/media/9', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ ...oneMediaDetail, archivedAt: restored ? null : '2026-09-13T10:00:00Z' }),
    }),
  );
  await page.route('**/api/media/9/restore', (route) => {
    restored = true;
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ ...oneMediaDetail, archivedAt: null }),
    });
  });

  await page.goto('/staff/media/9');

  await expect(page.getByText(englishCommon.media.archive.notice)).toBeVisible();
  await expect(page.getByRole('button', { name: englishCommon.media.replace.action })).toBeVisible();

  await page.getByRole('button', { name: englishCommon.media.archive.restore }).click();

  await expect(page.getByText(englishCommon.media.archive.notice)).toHaveCount(0);
  expect(restored).toBe(true);
});
