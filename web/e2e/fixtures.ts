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
