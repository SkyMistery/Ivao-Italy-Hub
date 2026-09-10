import { expect, test } from '@playwright/test';

import { stubTheApi } from './fixtures';
import { englishAtc, englishCommon } from './locales';

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

  // Since M1 G8 the front page is a published row, so what is asserted is the block the fixture
  // published and not a sentence of the application's own: the day this page draws words the code
  // put there, this fails.
  await expect(page.getByRole('heading', { name: 'Welcome to the division', level: 1 })).toBeVisible();

  // The frame around it: the division name from the bootstrap, and the footer built from the
  // language files. Their presence is what says the layout mounted rather than just the route.
  await expect(page.getByText('IVAO Example').first()).toBeVisible();
  await expect(
    page.getByText(
      englishCommon.footer.rights
        .replace('{{year}}', String(new Date().getFullYear()))
        .replace('{{division}}', 'IVAO Example')
        .replace('{{version}}', '0.0.0-e2e'),
    ),
  ).toBeVisible();
});

test('the menu is what the bootstrap says, one level deep, and the footer carries its own', async ({
  page,
}) => {
  await page.goto('/');

  // Both kinds of entry, drawn side by side: an editorial row shows its words, a module's shows
  // what its key translates to. Neither is written in the client (design M1 §8.1).
  // Named, because the header holds two: the bar with the division's own name, and the menu. The
  // first one on the page is the bar, which is how this assertion first went looking in the wrong
  // half of the header.
  const navigation = page.getByRole('navigation', { name: 'Main' });
  await expect(navigation.getByText('Home', { exact: true })).toBeVisible();
  await expect(navigation.getByText(englishAtc.nav.atc, { exact: true })).toBeVisible();

  // A parent with children is a drop down, and it holds itself first so its own address stays
  // reachable: a heading that leads nowhere is what the alternative would be.
  await navigation.getByText('About', { exact: true }).click();
  await expect(page.getByRole('link', { name: 'Team', exact: true })).toBeVisible();

  // The footer draws the entries of its own scope, which the top menu must not show.
  await expect(page.getByRole('link', { name: 'Legal', exact: true })).toBeVisible();
  await expect(navigation.getByText('Legal', { exact: true })).toHaveCount(0);
});

test('the reading column of the home is a column and not a strip', async ({ page }) => {
  // Geometry, because the text was right and in the wrong place twice in this repository already
  // (handoff §13): a page drawn by the renderer has to be a readable column, not the full width of
  // a desktop window and not a 255 pixel strip.
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/');

  const heading = page.getByRole('heading', { name: 'Welcome to the division', level: 1 });
  await expect(heading).toBeVisible();

  const column = await heading.boundingBox();
  const frame = await page.locator('main').boundingBox();
  expect(column, 'the heading has no box, so nothing was laid out').not.toBeNull();
  expect(frame, 'the shell has no box, so the page was drawn outside it').not.toBeNull();

  // Wide enough to be a column and not the 255 pixel strip of handoff §13.
  expect(column!.width).toBeGreaterThan(500);

  // And narrower than the frame it sits in, by enough to see. This is the assertion that actually
  // distinguishes the two states, and it was written by measuring both rather than by guessing: a
  // section at `width: default` is 992 wide inside a 1152 frame, one at `full` is 1088 — so a bound
  // of "under 1100" would have passed for both, which is what the first version of this test did.
  expect(frame!.width - column!.width).toBeGreaterThan(100);
  expect(column!.x).toBeGreaterThan(frame!.x);
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

  // ⚠️ The code and not the name of the language, since 10 September 2026: the switcher shows
  // "EN" / "IT" because spelled out it took more room in the bar than the search, the theme and
  // the account together. The full name is still its accessible label, not its text.
  await page.getByRole('option', { name: 'IT', exact: true }).click();

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
