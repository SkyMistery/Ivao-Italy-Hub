import { defineConfig, devices } from '@playwright/test';

/**
 * The smoke suite: does the application come up in a real browser at all.
 *
 * It exists because of a failure the 74 unit tests could not see. `DarkModeToggle` wraps itself in
 * a Radix tooltip, `main.tsx` mounted no `TooltipProvider`, and every screen behind a layout died
 * in the root error boundary — while every unit test stayed green, because each mounted one
 * component under a harness of its own and the fault was in the tree. A browser opening `/` finds
 * that in a second, and nothing short of a browser finds it at all.
 *
 * It runs against the **production build** (`vite preview`), not the dev server: a bundle that
 * behaves differently once minified and tree shaken is precisely the sort of thing this is for.
 */
export default defineConfig({
  testDir: './e2e',
  // The round with the real API is its own configuration: it needs a database and a server, and
  // this suite is the one that must stay fast and offline. `playwright.full.config.ts`.
  testIgnore: '**/full/**',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',

  use: {
    baseURL: 'http://127.0.0.1:4173',
    trace: 'on-first-retry',
  },

  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  // The built bundle, served the way the published package serves it. The API is not started: what
  // this suite covers is the front end assembling itself, so the few calls the shell makes are
  // fulfilled from `e2e/fixtures.ts` instead. A full stack run — real API, a page published from a
  // seed — is a bigger piece of machinery and is still owed (handoff §10).
  webServer: {
    command: 'pnpm build && pnpm preview --port 4173 --host 127.0.0.1',
    url: 'http://127.0.0.1:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
  },
});
