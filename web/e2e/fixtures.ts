import type { Page } from '@playwright/test';

/**
 * What `GET /api/me` answers for an anonymous visitor of a division that speaks two languages.
 *
 * Written out rather than fetched from a running API on purpose: this suite is about the front end
 * assembling itself in a browser, and an API in the loop would make it slower, flakier and no
 * better at the one job it has. The shape is the generated contract's — if the server changes it,
 * `pnpm gen:api` moves `schema.d.ts`, the typed client stops compiling, and that is the check.
 */
export const anonymousBootstrap = {
  user: null,
  permissions: [],
  division: {
    code: 'XX',
    name: { en: 'IVAO Example', it: 'IVAO Esempio' },
    locales: ['en', 'it'],
    defaultLocale: 'en',
    timezone: 'UTC',
    firStaffScope: 'all',
  },
  modules: [],
  navigation: { public: [{ key: 'nav.home', path: '/' }], staff: [] },
  registries: { blocks: [], widgets: [], permissions: [] },
  version: '0.0.0-e2e',
};

/**
 * Answers the calls the shell makes on its way up, and fails loudly on any other `/api` request:
 * a smoke that silently swallowed an unexpected call would hide the very thing it is watching for.
 */
export async function stubTheApi(page: Page): Promise<void> {
  await page.route('**/api/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(anonymousBootstrap),
    }),
  );

  await page.route('**/api/**', (route) => {
    const url = route.request().url();
    if (url.includes('/api/me')) {
      return route.fallback();
    }

    return route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ title: `Unexpected call in the smoke suite: ${url}` }),
    });
  });
}

/**
 * A staff member who may work on the links of ED: enough to open `/staff/ed/links` and the form
 * behind its "new" button, and nothing more.
 *
 * `hasAllDepartments` is false and `departments` holds one entry on purpose: that is the identity
 * the department guard on the route actually examines, so a smoke run under a superadmin would not
 * be exercising it.
 */
export const staffBootstrap = {
  ...anonymousBootstrap,
  user: {
    vid: 111111,
    firstName: 'Test',
    lastName: 'Coordinator',
    positions: ['XX-EC'],
    isStaff: true,
    isSuperadmin: false,
    hasAllDepartments: false,
    locale: 'en',
    departments: ['ED'],
    firs: [],
  },
  permissions: [
    { name: 'Links.View', department: 'ED' },
    { name: 'Links.Edit', department: 'ED' },
  ],
  navigation: {
    public: [{ key: 'nav.home', path: '/' }],
    staff: [{ key: 'nav.links', path: '/staff/links' }],
  },
};

/** One page of links, the shape `MapCrud` answers a list with. */
export const oneLink = {
  items: [
    {
      id: 7,
      ownerDepartment: 'ED',
      visibility: 'Public',
      title: { en: 'Discord', it: 'Discord' },
      url: 'https://example.org/discord',
      category: null,
      sort: 0,
      isActive: true,
      updatedAt: '2026-09-04T12:00:00Z',
    },
  ],
  page: 1,
  pageSize: 25,
  total: 1,
};

/**
 * The same stubbing, for a signed in member of the staff: `/api/me` answers with a coordinator and
 * `/api/links` with one page. Anything else under `/api` still fails the test rather than being
 * quietly answered, so a screen that started calling something new says so.
 */
export async function stubTheApiAsStaff(page: Page): Promise<void> {
  await page.route('**/api/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(staffBootstrap),
    }),
  );

  await page.route('**/api/links**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(oneLink) }),
  );

  await page.route('**/api/**', (route) => {
    const url = route.request().url();
    if (url.includes('/api/me') || url.includes('/api/links')) {
      return route.fallback();
    }

    return route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ title: `Unexpected call in the smoke suite: ${url}` }),
    });
  });
}
