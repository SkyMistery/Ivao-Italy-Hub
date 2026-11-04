import { expect, test } from '@playwright/test';

import { siteStaffBootstrap, staffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * «Settings» on the screen of the content (decisions/2026-11-04-la-pagina-dello-stile.md): the page of the division's
 * style, chosen by whoever cures the templates and reaches every department, from the published pages the whole staff
 * reads. What is sent is checked here; what the server refuses, by `ContentSettingsTests`.
 */

const settings = englishCommon.content.settings;

/** The web coordinator, whose role reaches every department (Carmine, 4 November 2026: «Solo chi ha tutti»). */
const curator = {
  ...siteStaffBootstrap,
  user: { ...siteStaffBootstrap.user, hasAllDepartments: true },
};

/** Three pages: one the whole staff reads, one only its department reads, one never published. */
const pages = {
  items: [
    { id: 9, slug: 'style', path: 'about/style', visibility: 'Staff', publishedAt: '2026-10-01T00:00:00Z' },
    {
      id: 10,
      slug: 'team-only',
      path: 'team-only',
      visibility: 'Department',
      publishedAt: '2026-10-01T00:00:00Z',
    },
    { id: 11, slug: 'draft', path: 'draft', visibility: 'Staff', publishedAt: null },
  ].map((page) => ({
    ...page,
    kind: 'Page',
    parentId: null,
    ownerDepartment: 'WD',
    status: page.publishedAt === null ? 'Draft' : 'Published',
    isTemplate: false,
    title: { en: page.slug, it: page.slug },
    collections: [],
    coverMediaId: null,
    pinned: false,
    sort: 0,
    fileMediaId: null,
    reviewOn: null,
    retiredAt: null,
    supersededById: null,
    updatedAt: '2026-10-01T00:00:00Z',
  })),
  page: 1,
  pageSize: 100,
  total: 3,
};

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('whoever cures the templates chooses the page of the style among the ones the whole staff reads', async ({
  page,
}) => {
  await stubTheApiAsStaff(page, curator);
  await page.route('**/api/content?**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(pages) }),
  );

  const sent: unknown[] = [];
  await page.route('**/api/content/settings', async (route) => {
    if (route.request().method() === 'PUT') {
      sent.push(route.request().postDataJSON());
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ stylePageId: 9, stylePagePath: '/about/style' }),
      });
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ stylePageId: null, stylePagePath: null }),
    });
  });

  await page.goto('/staff/content?kind=Page');
  await page.getByRole('button', { name: settings.open }).click();

  const field = page.getByRole('combobox', { name: settings.fields.stylePageId });
  await expect(field).toBeVisible();
  await field.click();

  // The page for the whole staff is offered; the one of a department and the draft are not.
  await expect(page.getByRole('option', { name: /about\/style/ })).toBeVisible();
  await expect(page.getByRole('option', { name: /team-only/ })).toHaveCount(0);
  await expect(page.getByRole('option', { name: /\/draft/ })).toHaveCount(0);

  await page.getByRole('option', { name: /about\/style/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: englishCommon.common.save }).click();

  await expect.poll(() => sent).toEqual([{ stylePageId: 9 }]);
});

test('whoever does not cure the templates has no settings', async ({ page }) => {
  await stubTheApiAsStaff(page, staffBootstrap);

  await page.goto('/staff/content?kind=Page');

  // The screen's own title, not the division's name in the header.
  await expect(page.locator('h1[aria-current="page"]')).toBeVisible();
  await expect(page.getByRole('button', { name: settings.open })).toHaveCount(0);
});

test('whoever cures the templates of one department only has no settings either', async ({ page }) => {
  // `Content.ManageTemplates` on the web team's department, without the role that reaches all of them.
  await stubTheApiAsStaff(page, siteStaffBootstrap);

  await page.goto('/staff/content?kind=Page');

  await expect(page.locator('h1[aria-current="page"]')).toBeVisible();
  await expect(page.getByRole('button', { name: settings.open })).toHaveCount(0);
});
