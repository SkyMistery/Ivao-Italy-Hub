import { expect, test, type Page } from '@playwright/test';

import { siteStaffBootstrap, staffBootstrap, stubTheApiAsStaff, stubTheBlockData } from './fixtures';
import { englishCommon } from './locales';

/**
 * The two personal dashboards (D3, note 2026-09-13-le-dashboard-a-tutto-schermo): `/staff` is no
 * longer a door to the first department but the seeded row `staff`, and `/me` draws the row `me` on
 * the whole width of the public frame. What is asserted is **arriving**: the tiles the row carries
 * are drawn, with the answer the server gives for whoever is looking.
 */

/** A published dashboard of the site's department, with the tiles of one section. */
function dashboard(slug: string, title: string, blocks: readonly unknown[]) {
  return {
    id: slug === 'staff' ? 42 : 43,
    kind: 'Dashboard',
    slug,
    path: slug,
    ownerDepartment: 'WD',
    title: { en: title, it: title },
    summary: null,
    seo: null,
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 's_tiles',
          key: 'tiles',
          layout: 'stacked',
          background: 'none',
          padding: 'sm',
          width: 'full',
          blocks,
        },
      ],
    },
    schemaVersion: 1,
    collections: [],
    coverMediaId: null,
    fileMediaId: null,
    version: 1,
    publishedAt: '2026-09-13T20:00:00.000Z',
    effectiveOn: null,
    reviewOn: null,
    retiredAt: null,
    supersededBySlug: null,
    supersededByTitle: null,
    showFooter: true,
    publishedByName: null,
    media: {},
  };
}

const staffDashboard = dashboard('staff', 'Staff dashboard', [
  {
    id: 'b_drafts',
    type: 'myWork',
    version: 1,
    props: { what: 'drafts', limit: 5 },
    span: 6,
    renderMode: 'live',
  },
  { id: 'b_departments', type: 'myDepartments', version: 1, props: {}, span: 6 },
]);

const memberDashboard = dashboard('me', 'My dashboard', [
  {
    id: 'b_welcome',
    type: 'welcome',
    version: 1,
    props: { message: { en: 'Nice to see you.', it: 'Ciao.' } },
    span: 12,
  },
]);

async function stubTheDashboard(page: Page, slug: string, answer: unknown): Promise<void> {
  await page.route(`**/api/content/public/Dashboard/${slug}`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(answer) }),
  );
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('/staff is the staff dashboard, answered for whoever is looking', async ({ page }) => {
  await stubTheApiAsStaff(page);
  await stubTheDashboard(page, 'staff', staffDashboard);
  await stubTheBlockData(page, 'myWork', {
    what: 'drafts',
    items: [
      {
        title: { en: 'Summer fly-in', it: 'Raduno estivo' },
        url: '/staff/content/7',
        department: 'ED',
        at: '2026-09-12T18:00:00.000Z',
        status: 'Draft',
        sentBack: true,
      },
    ],
  });

  await page.goto('/staff');

  // Not sent on to a department: the address stays, and the row's own title is the heading.
  await expect(page).toHaveURL(/\/staff$/);
  await expect(page.getByRole('heading', { name: 'Staff dashboard' })).toBeVisible();

  await expect(page.getByText(englishCommon.blocks.myWork.titles.drafts)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Summer fly-in' })).toHaveAttribute('href', '/staff/content/7');
  await expect(page.getByText(englishCommon.blocks.myWork.sentBack)).toBeVisible();

  // The departments of this coordinator, one link each.
  await expect(
    page.getByRole('link', { name: englishCommon.departments.ED, exact: true }).first(),
  ).toHaveAttribute('href', '/staff/ed');

  // The row belongs to the site's department, and this coordinator may not change it.
  await expect(page.getByRole('link', { name: englishCommon.dashboard.edit })).toHaveCount(0);
});

test('whoever may edit the site finds "edit" leading to the row they are reading', async ({ page }) => {
  await stubTheApiAsStaff(page, siteStaffBootstrap);
  await stubTheDashboard(page, 'staff', staffDashboard);
  await stubTheBlockData(page, 'myWork', { what: 'drafts', items: [] });

  await page.goto('/staff');

  await expect(page.getByText(englishCommon.blocks.myWork.empty.drafts)).toBeVisible();
  await expect(page.getByRole('link', { name: englishCommon.dashboard.edit })).toHaveAttribute(
    'href',
    '/staff/wd/dashboard/42',
  );
});

test('/me greets the member on the whole width of the frame', async ({ page }) => {
  await stubTheApiAsStaff(page, staffBootstrap);
  await stubTheDashboard(page, 'me', memberDashboard);
  await page.route('**/api/me/notifications', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
  );

  await page.goto('/me');

  await expect(page.getByRole('heading', { name: 'My dashboard' })).toBeVisible();
  await expect(page.getByText('Hello, Test')).toBeVisible();
  await expect(page.getByText('Nice to see you.')).toBeVisible();

  // A dashboard is not a page: the reading column of the public frame (1152 px) does not hold it.
  const width = await page.locator('main').evaluate((main) => main.getBoundingClientRect().width);
  expect(width).toBeGreaterThan(1152);
});
