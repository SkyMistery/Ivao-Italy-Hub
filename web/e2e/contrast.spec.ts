import { type Page, expect, test } from '@playwright/test';

import { measureContrast, type Measured } from './contrast';
import { siteStaffBootstrap, stubTheApi, stubTheApiAsStaff } from './fixtures';

/**
 * The secondary text is readable in the dark theme, measured rather than looked at.
 *
 * ⚠️ Why this exists: Atmosphere flips every foreground for the dark theme — `--foreground` from
 * fuselage-800 to fuselage-100, `--card-foreground` to fuselage-200 — except `--muted-foreground`,
 * which stays fuselage-500 in both. A grey that is dark on white is the same grey on near black,
 * and the staff position codes on `/about` came out at **3.14 : 1** where WCAG AA asks 4.5 : 1 for
 * text that size. The hub overrides that one token in `src/styles/index.css`, and this is what says
 * the override is still there and still winning: it is the only token this project overrides, so it
 * is exactly the sort of line a later change removes by accident.
 *
 * Only a browser can answer it. The value comes from a variable, defined in a stylesheet loaded
 * after Tailwind's utilities, resolved against a background that is often half transparent and
 * painted over something else. The number that matters is the one the browser computed.
 */

/**
 * Where the secondary text of a screen is. Two selectors and not one since 10 September 2026: the
 * footer stopped taking its colours from the theme when it was given a ground of its own -- a token
 * meant for dark-on-light says nothing on a blue band -- so it says what it is with an attribute
 * instead, and this check follows it there rather than losing sight of it.
 */
const SECONDARY_TEXT = '.text-muted-foreground, [data-secondary]';

async function secondaryTextOf(page: Page): Promise<Measured[]> {
  // ⚠️ Asserted and not assumed: without the class this would measure the light theme and pass
  // while proving nothing, which is the failure mode of every test that checks a colour.
  await expect(page.locator('html')).toHaveClass(/dark/);

  // And waited for, for the same reason: `goto` returns when the document loaded, and React draws
  // after that. Measuring an empty screen is a test that says nothing and says it in green — the
  // footer alone carries four of these, so on any screen of this application there is something.
  await page.locator(SECONDARY_TEXT).first().waitFor({ state: 'visible' });

  // The measure itself is shared with every other spec that asks how readable something is: see
  // `./contrast` for why it paints colours instead of reading them.
  const rows = await measureContrast(page, SECONDARY_TEXT);

  expect(rows.length, 'no secondary text on this screen to measure').toBeGreaterThan(0);

  return rows;
}

function readable(where: string, rows: Measured[]) {
  const failing = rows.filter((row) => row.measured < row.needs);
  const said = failing
    .map((row) => `${where}: "${row.text}" at ${row.size}px is ${row.measured}:1, needs ${row.needs}:1`)
    .join('\n');

  expect(failing, said).toEqual([]);
}

test.describe('the dark theme', () => {
  // The browser asks for dark and the application follows: nothing is stored, so the media query
  // decides, exactly as it does for somebody arriving for the first time.
  test.use({ colorScheme: 'dark' });

  test('the secondary text of the public site meets AA', async ({ page }) => {
    await stubTheApi(page);

    for (const path of ['/', '/about', '/news', '/documents', '/calendar']) {
      await page.goto(path);
      readable(path, await secondaryTextOf(page));
    }
  });

  test('the secondary text of the back office meets AA', async ({ page }) => {
    // The half that has the most of it: every list has a description under a title, and every form
    // a hint under a field.
    await stubTheApiAsStaff(page, siteStaffBootstrap);

    for (const path of ['/staff/wd', '/staff/wd/menu', '/staff/wd/menu/3', '/staff/wd/links']) {
      await page.goto(path);
      readable(path, await secondaryTextOf(page));
    }
  });
});
