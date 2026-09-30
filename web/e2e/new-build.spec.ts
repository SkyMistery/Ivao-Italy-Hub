import { expect, test, type Page } from '@playwright/test';

import { anonymousBootstrap, stubTheApi } from './fixtures';

/**
 * A tab that outlives a delivery (note 2026-09-30-la-pagina-dopo-una-consegna).
 *
 * The page keeps the bootstrap it loaded with for a minute; a navigation after that hands the kept one
 * to the screens and asks the server again behind it, and the navigation after that reads the answer.
 * That is when a tab opened before an upload met the new menu with the old bundle. The clock is moved
 * past the minute rather than waited for.
 */

type Bootstrap = typeof anonymousBootstrap;

const delivered: Bootstrap = { ...anonymousBootstrap, version: '0.0.1-e2e', commit: 'def5678' };

/** Answers `/api/me` with whichever build the test says the server is running now. */
async function serveTheBuild(page: Page, first: Bootstrap): Promise<(next: Bootstrap) => void> {
  let current = first;
  await page.route('**/api/me', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(current) }),
  );
  return (next) => {
    current = next;
  };
}

/** How many times the browser loaded the page itself, rather than drew a screen inside it. */
function countPageLoads(page: Page): () => number {
  let loads = 0;
  page.on('request', (request) => {
    if (request.resourceType() === 'document') {
      loads++;
    }
  });
  return () => loads;
}

const welcome = (page: Page) => page.getByRole('heading', { name: 'Welcome to the division', level: 1 });

/** A minute later, a click in the menu: the kept bootstrap is drawn, and asked for again behind it. */
async function aMinuteLaterFollowTheMenu(page: Page): Promise<void> {
  await page.clock.fastForward('02:00');
  const askedAgain = page.waitForResponse('**/api/me');
  await page.getByRole('link', { name: 'Modules', exact: true }).first().click();
  await askedAgain;
  // Only the address: what that page draws is the stubbed API's business, and this is about the frame.
  await expect(page).toHaveURL(/\/modules-entry$/);
}

async function goHome(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Home', exact: true }).first().click();
  await expect(page).toHaveURL(/\/$/);
  await expect(welcome(page)).toBeVisible();
}

test.beforeEach(async ({ page }) => {
  await stubTheApi(page);
  await page.clock.install();

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the same build on the next navigations draws the screens inside the page', async ({ page }) => {
  await serveTheBuild(page, anonymousBootstrap);
  const loads = countPageLoads(page);

  await page.goto('/');
  await expect(welcome(page)).toBeVisible();
  await aMinuteLaterFollowTheMenu(page);
  await goHome(page);

  expect(loads()).toBe(1);
});

test('a new build on the server loads the page again, at the address it was going to', async ({ page }) => {
  const deliver = await serveTheBuild(page, anonymousBootstrap);
  const loads = countPageLoads(page);

  await page.goto('/');
  await expect(welcome(page)).toBeVisible();

  deliver(delivered);
  await aMinuteLaterFollowTheMenu(page);
  expect(loads()).toBe(1);

  await goHome(page);
  expect(loads()).toBe(2);
  // The footer of the page loaded again speaks for the new build.
  await expect(page.getByText('0.0.1-e2e · def5678')).toBeVisible();
});

test('two releases answering in turn reload the page once, not forever', async ({ page }) => {
  // The old process and the new one side by side, as during an upload: the page meets the new
  // build, reloads, is served by the old one, and meets the new one again.
  const serve = await serveTheBuild(page, anonymousBootstrap);
  const loads = countPageLoads(page);

  await page.goto('/');
  await expect(welcome(page)).toBeVisible();

  serve(delivered);
  await aMinuteLaterFollowTheMenu(page);
  serve(anonymousBootstrap);
  await goHome(page);
  expect(loads()).toBe(2);
  await expect(page.getByText('0.0.0-e2e · abc1234')).toBeVisible();

  serve(delivered);
  await aMinuteLaterFollowTheMenu(page);
  await goHome(page);
  expect(loads()).toBe(2);
});
