import { expect, test } from '@playwright/test';

import { stubTheApi } from './fixtures';
import { englishCommon } from './locales';

/**
 * A page behind a guard, opened by somebody who is not signed in (#329).
 *
 * The guard sends the visitor to `/auth/login`, an endpoint of the server and not a page of the
 * application. On the test installation the browser never got there: the router took the path for one of
 * its own, `_public/$` caught it, and the visitor read "This page does not exist" at the sign-in's address.
 * What is asserted here is the half a unit test cannot see — that the browser **asks the server** for the
 * sign-in, as a document.
 *
 * The suite runs without the API, so the server's answer is stood in for by a page that says where it is.
 */

const SIGN_IN_PAGE = 'The server sign-in';

test.beforeEach(async ({ page }) => {
  await stubTheApi(page);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

for (const address of ['/staff', '/me/tokens']) {
  test(`${address} opened signed out goes to the server's sign-in and comes back to ${address}`, async ({
    page,
  }) => {
    const asked: { url: string; document: boolean }[] = [];
    await page.route('**/auth/login**', (route) => {
      asked.push({ url: route.request().url(), document: route.request().isNavigationRequest() });
      return route.fulfill({ status: 200, contentType: 'text/html', body: `<h1>${SIGN_IN_PAGE}</h1>` });
    });

    await page.goto(address);

    await expect(page.getByRole('heading', { name: SIGN_IN_PAGE })).toBeVisible();
    await expect(page.getByText(englishCommon.notFound.title)).toHaveCount(0);

    const url = new URL(page.url());
    expect(url.pathname).toBe('/auth/login');
    // The path, not the whole address: a list fills in its default search (`?page=1&pageSize=25…`)
    // before the guard runs, and coming back to it with that search is coming back to the same page.
    expect(new URL(url.searchParams.get('returnUrl') ?? '', url.origin).pathname).toBe(address);
    expect(asked).toEqual([{ url: page.url(), document: true }]);
  });
}
