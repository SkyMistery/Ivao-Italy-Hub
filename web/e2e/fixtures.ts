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
    // Not UTC, deliberately. A hub shows every time in UTC *and* where the division lives, and a
    // fixture whose division sits in UTC makes the two lines identical — which is exactly how a
    // screen showing UTC twice would pass unnoticed (HANDOFF §13, third false alarm).
    timezone: 'Europe/Rome',
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
    { name: 'Media.View', department: 'ED' },
    { name: 'Media.Edit', department: 'ED' },
    // The gallery is behind `Admin.Access`, and the gallery is where every kind of field the form
    // generator draws is mounted at once — which is the only screen that can be looked at whole.
    { name: 'Admin.Access', department: null },
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

/** One page of the media library, the shape `MapCrud` answers a list with. */
export const oneMedia = {
  items: [
    {
      id: 9,
      ownerDepartment: 'ED',
      visibility: 'Staff',
      fileName: 'banner.png',
      contentType: 'image/png',
      byteSize: 2048,
      width: 640,
      height: 360,
      alt: { en: 'A runway at dawn', it: 'Una pista all alba' },
      category: null,
      url: '/media/9/banner.png',
      createdAt: '2026-09-05T09:00:00Z',
      updatedAt: '2026-09-05T09:00:00Z',
    },
  ],
  page: 1,
  pageSize: 25,
  total: 1,
};

/** The same file as its metadata form loads it. */
export const oneMediaDetail = {
  ...oneMedia.items[0],
  title: null,
  hasFile: true,
  deletedAt: null,
  createdBy: 111111,
  updatedBy: 111111,
  rowVersion: '2026-09-05T09:00:00Z',
};

/** An empty page, for the "which contents use this file" filter of the content list. */
export const noContent = { items: [], page: 1, pageSize: 25, total: 0 };

/**
 * A real picture, 8 by 8 and red, so that a test can measure the box a browser gives it. A stub
 * answering a broken image would draw the alternative text instead, and the two look nothing alike
 * on screen but exactly alike to an assertion about text.
 */
const RED_8X8_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAFElEQVR42mP8z8BQz0AEYBxVSF+FANqkA/8ZBEwuAAAAAElFTkSuQmCC',
  'base64',
);

/**
 * A published page, for a visitor who is nobody. The body is handed in, because what these tests
 * are about is what a body of blocks *looks like* once a browser has laid it out — which is the one
 * thing neither a unit test nor a screenshot-free assertion can see (implementation plan M1 §A.9).
 */
export async function stubThePublishedPage(page: Page, slug: string, body: unknown): Promise<void> {
  await stubTheApi(page);

  await page.route(`**/api/content/public/Page/${slug}`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        kind: 'Page',
        slug,
        title: { en: 'A page of blocks', it: 'Una pagina di blocchi' },
        summary: null,
        seo: null,
        body,
        schemaVersion: 1,
        version: 1,
        publishedAt: '2026-09-06T10:00:00Z',
      }),
    }),
  );

  // The pictures a block asks for. Served the way Kestrel serves them, at `/media/{id}/{name}`.
  await page.route('**/media/*/**', (route) =>
    route.fulfill({ status: 200, contentType: 'image/png', body: RED_8X8_PNG }),
  );
}

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

  await page.route('**/api/media/*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(oneMediaDetail),
    }),
  );

  await page.route('**/api/media?**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(oneMedia) }),
  );

  await page.route('**/api/content**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(noContent) }),
  );

  // Not under /api, so the guard below never sees it: the file route is served by Kestrel.
  await page.route('**/media/9/**', (route) =>
    route.fulfill({ status: 200, contentType: 'image/png', body: RED_8X8_PNG }),
  );

  await page.route('**/api/**', (route) => {
    const url = route.request().url();
    if (
      url.includes('/api/me') ||
      url.includes('/api/links') ||
      url.includes('/api/media') ||
      url.includes('/api/content')
    ) {
      return route.fallback();
    }

    return route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ title: `Unexpected call in the smoke suite: ${url}` }),
    });
  });
}
