import { defineConfig, devices } from '@playwright/test';

/**
 * The round, against the real API: create from a template, add blocks, publish, read the published
 * page as a visitor. It is the half of the end to end story `playwright.config.ts` deliberately
 * does not tell — that suite stubs `/api/me` and fails every other call on purpose, because what
 * it watches is the front end assembling itself in a browser.
 *
 * This one has a database, a server and a session. It is slower and it is worth it: until M1 the
 * sentence "a member of staff opens the editor, adds a block and publishes" had never been
 * executed in a browser even once (handoff section 10, debt 1).
 *
 * The server is the *published* application, so the SPA arrives through the server's own fallback
 * rather than through a static file server or Vite. See `scripts/e2e-server.mjs`.
 */
const baseURL = process.env.E2E_URL ?? 'http://127.0.0.1:5080';

export default defineConfig({
  testDir: './e2e/full',

  // The round writes rows and reads them back; two workers would publish over each other's page.
  workers: 1,
  fullyParallel: false,

  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',

  use: {
    baseURL,
    trace: 'on-first-retry',
  },

  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  webServer: {
    command: 'node scripts/e2e-server.mjs',
    // `/health` and not the home page: it pings the database, so waiting on it means waiting for
    // the migrations, the seed of the system templates and the reference data as well.
    url: `${baseURL}/health`,
    reuseExistingServer: !process.env.CI,
    // Publishing builds the SPA and the .NET package first.
    timeout: 600_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
