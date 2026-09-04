import { expect, test } from '@playwright/test';

import { stubTheApi } from './fixtures';
import { englishCommon } from './locales';

/**
 * The application comes up in a browser.
 *
 * That is the whole ambition, and it is not a low one: the failure that made this suite exist —
 * a missing `TooltipProvider` — took down every screen behind a layout and was invisible to 74
 * green unit tests. Anything a browser can tell us that jsdom cannot belongs here; anything a unit
 * test already covers does not.
 */

test.beforeEach(async ({ page }) => {
  await stubTheApi(page);

  // A page that throws is caught by the root error boundary and rendered as an apology, so the
  // assertions below would still find *something*. Failing on the console instead means the test
  // says which error, not merely that a heading is missing.
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the home page renders inside its shell', async ({ page }) => {
  await page.goto('/');

  // The error boundary's own words, which must not be on the page. Checked first and by name:
  // "the heading is missing" and "the application crashed" are very different bug reports.
  await expect(page.getByText('Something went wrong!')).toHaveCount(0);

  await expect(page.getByRole('heading', { name: englishCommon.home.heading })).toBeVisible();

  // The frame around it: the division name from the bootstrap, and the footer built from the
  // language files. Their presence is what says the layout mounted rather than just the route.
  await expect(page.getByText('IVAO Example').first()).toBeVisible();
  await expect(
    page.getByText(englishCommon.footer.version.replace('{{version}}', '0.0.0-e2e')),
  ).toBeVisible();
});

test('the header carries the controls every layout shares', async ({ page }) => {
  await page.goto('/');

  // The theme toggle is the component that broke: it wraps itself in a tooltip, and a tooltip
  // without its provider throws. Hovering it is what actually opens the tooltip, so this asserts
  // the thing that was broken rather than merely that a button exists.
  const toggle = page.getByRole('button', { name: englishCommon.theme.toggle });
  await expect(toggle).toBeVisible();
  await toggle.hover();
  await expect(page.getByRole('tooltip')).toContainText(englishCommon.theme.toggle);

  await expect(page.getByRole('link', { name: englishCommon.auth.login })).toBeVisible();
});

test('an address that belongs to nobody is a not found, not a crash', async ({ page }) => {
  await page.goto('/there-is-no-such-page');

  await expect(page.getByText('Something went wrong!')).toHaveCount(0);
});

test('the language switcher actually switches', async ({ page }) => {
  await page.goto('/');

  const heading = page.getByRole('heading').first();
  await expect(heading).toHaveText('IVAO Example');

  await page.getByRole('combobox').first().click();
  await page.getByRole('option', { name: /italian|italiano/i }).click();

  // The name of the division is a `Localized<T>` resolved by the client, so it changing is proof
  // that the language really changed and not merely that a select closed.
  await expect(heading).toHaveText('IVAO Esempio');
});

/**
 * Note for whoever sees `Unknown event handler property onValueChange` in the console: it is
 * Atmosphere's, not ours. Its `Select` spreads its rest props twice — once onto Radix's
 * `Select.Root`, which is what handles the change, and once onto the viewport `div`, where React
 * ignores it and complains. The test above is what says the handler still runs. Do not "fix" it by
 * removing `onValueChange`.
 */
