import { expect, test } from '@playwright/test';

import { siteStaffBootstrap, staffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * «Settings» on the screen of the content (decisions/2026-11-04-la-pagina-dello-stile.md): the page of the division's
 * style and the page of the guide «How to make a page» (design 13 §5.2), chosen by whoever cures the templates and reaches
 * every department, from the published pages the whole staff reads. What is sent is checked here; what the server
 * refuses, by `ContentSettingsTests`. The link to the guide on the form of a row is at the end.
 */

const settings = englishCommon.content.settings;

/** The web coordinator, whose role reaches every department (Carmine, 4 November 2026: «Solo chi ha tutti»). */
const curator = {
  ...siteStaffBootstrap,
  user: { ...siteStaffBootstrap.user, hasAllDepartments: true },
};

/**
 * What `GET /api/content/addresses` answers: what the site shows the curator, published only. A page the whole staff reads,
 * one only its department reads, and a news item — of which only the first may be the page of the style.
 */
const addresses = [
  {
    id: 9,
    kind: 'Page',
    url: '/about/style',
    title: { en: 'Style', it: 'Stile' },
    ownerDepartment: 'WD',
    visibility: 'Staff',
  },
  {
    id: 10,
    kind: 'Page',
    url: '/team-only',
    title: { en: 'Team only', it: 'Solo squadra' },
    ownerDepartment: 'WD',
    visibility: 'Department',
  },
  {
    id: 12,
    kind: 'News',
    url: '/news/launch',
    title: { en: 'Launch', it: 'Lancio' },
    ownerDepartment: 'ED',
    visibility: 'Public',
  },
];

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('whoever cures the templates chooses the page of the style among the ones the whole staff reads', async ({
  page,
}) => {
  await stubTheApiAsStaff(page, curator);
  await page.route('**/api/content/addresses', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(addresses) }),
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

  // The page for the whole staff is offered; the one of a department and a news item are not.
  await expect(page.getByRole('option', { name: /about\/style/ })).toBeVisible();
  await expect(page.getByRole('option', { name: /team-only/ })).toHaveCount(0);
  await expect(page.getByRole('option', { name: /news\/launch/ })).toHaveCount(0);

  await page.getByRole('option', { name: /about\/style/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: englishCommon.common.save }).click();

  // The whole of the settings: the guide, left on none, is said as none.
  await expect.poll(() => sent).toEqual([{ stylePageId: 9, guidePageId: null }]);
});

test('the page of the guide is a select of its own, and choosing it leaves the style as it was', async ({
  page,
}) => {
  await stubTheApiAsStaff(page, curator);
  await page.route('**/api/content/addresses', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(addresses) }),
  );

  const sent: unknown[] = [];
  await page.route('**/api/content/settings', async (route) => {
    if (route.request().method() === 'PUT') {
      sent.push(route.request().postDataJSON());
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          stylePageId: 9,
          stylePagePath: '/about/style',
          guidePageId: 9,
          guidePagePath: '/about/style',
        }),
      });
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        stylePageId: 9,
        stylePagePath: '/about/style',
        guidePageId: null,
        guidePagePath: null,
      }),
    });
  });

  await page.goto('/staff/content?kind=Page');
  await page.getByRole('button', { name: settings.open }).click();

  const field = page.getByRole('combobox', { name: settings.fields.guidePageId });
  await expect(field).toBeVisible();
  await field.click();

  // The same pages as for the style: the one of a department and a news item are not offered.
  await expect(page.getByRole('option', { name: /team-only/ })).toHaveCount(0);
  await expect(page.getByRole('option', { name: /news\/launch/ })).toHaveCount(0);

  await page.getByRole('option', { name: /about\/style/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: englishCommon.common.save }).click();

  await expect.poll(() => sent).toEqual([{ stylePageId: 9, guidePageId: 9 }]);
});

test('the form of a page links to the guide when whoever writes can open it, in a tab of its own', async ({
  page,
}) => {
  await stubTheApiAsStaff(page);
  await page.route('**/api/content/settings', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        stylePageId: null,
        stylePagePath: null,
        guidePageId: 4,
        guidePagePath: '/how-to-make-a-page',
      }),
    }),
  );

  await page.goto('/staff/content/new?kind=Page&department=ED');

  const link = page.getByRole('link', { name: englishCommon.content.guide.link });
  await expect(link).toHaveAttribute('href', '/how-to-make-a-page');
  await expect(link).toHaveAttribute('target', '_blank');
});

test('without a guide that whoever writes can open, the form draws no link', async ({ page }) => {
  await stubTheApiAsStaff(page);
  await page.route('**/api/content/settings', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      // Chosen, but not a page this reader can open: the server leaves the address out.
      body: JSON.stringify({ stylePageId: null, stylePagePath: null, guidePageId: 4, guidePagePath: null }),
    }),
  );

  await page.goto('/staff/content/new?kind=Page&department=ED');

  await expect(page.getByLabel(englishCommon.content.fields.parentId, { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: englishCommon.content.guide.link })).toHaveCount(0);
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
