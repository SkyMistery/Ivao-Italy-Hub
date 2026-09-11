import { expect, test } from '@playwright/test';

import { stubTheApi, stubTheApiAsStaff, stubTheSearch } from './fixtures';
import { englishCommon } from './locales';

/**
 * The two screens of the search (design M1 §7): the public one at `/search`, and the ⌘K palette of
 * the back office.
 *
 * What a unit test cannot see is here and only here: that a keyboard shortcut opens a dialog, that
 * the marked words come out marked in the DOM rather than as text with angle brackets in it, and —
 * the one that would otherwise be found in production — that the results coming back from the server
 * are **not** thrown away again by the palette's own filtering.
 */

const hits = {
  results: {
    items: [
      {
        sourceModule: 'core',
        sourceId: 'content:1',
        kind: 'page',
        url: '/start',
        ownerDepartment: 'WD',
        title: 'Getting started',
        // ⚠️ The word searched for is in the snippet and **not** in the title. That is the case the
        // palette used to lose: `cmdk` filters what it is given against what was typed, and this
        // row would have been dropped on the way in.
        snippet: '… and then the word aerodrome in the middle of the text …',
      },
    ],
    page: 1,
    pageSize: 20,
    total: 1,
  },
  notice: null,
};

test.describe('the public search', () => {
  test.beforeEach(async ({ page }) => {
    await stubTheApi(page);

    page.on('pageerror', (error) => {
      throw new Error(`The page threw: ${error.message}`);
    });
  });

  test('the query is the address, and the words searched for come back marked', async ({ page }) => {
    await stubTheSearch(page, hits);
    await page.goto('/search?q=aerodrome');

    await expect(page.getByRole('heading', { name: englishCommon.search.title })).toBeVisible();
    await expect(page.getByRole('link', { name: /Getting started/ })).toBeVisible();

    // `<mark>` and not a colour: the marking is a meaning, so it is in the DOM where a screen
    // reader can find it. This is also what says the server sent text and the client cut it.
    const marks = page.locator('mark');
    await expect(marks.first()).toHaveText('aerodrome');

    // The box carries what the address says, so a result worth sending somebody is a link.
    await expect(page.getByLabel(englishCommon.search.label)).toHaveValue('aerodrome');
  });

  test('a query of words too short to index says why, instead of showing nothing', async ({ page }) => {
    // The one of the three the code cannot fix, seen from the reader's side (design M1 §7).
    await stubTheSearch(page, {
      results: { items: [], page: 1, pageSize: 20, total: 0 },
      notice: 'search.termsTooShort',
    });

    await page.goto('/search?q=an%20il');

    await expect(page.getByText(englishCommon.search.termsTooShort)).toBeVisible();
  });

  test('the header carries the way in', async ({ page }) => {
    // A screen nobody can reach is half a screen. It is in the frame and not in the menu, because
    // the menu is what the staff writes and this is a tool of the site itself.
    await page.goto('/');

    await page.getByRole('link', { name: englishCommon.search.open }).click();

    await expect(page).toHaveURL(/\/search/);
    await expect(page.getByRole('heading', { name: englishCommon.search.title })).toBeVisible();
  });

  test('an empty box asks nothing and says so', async ({ page }) => {
    // No stub for the search here on purpose: the catch-all of the fixtures answers 500, so if the
    // screen asked anyway this test would fail rather than pass quietly.
    await page.goto('/search');

    await expect(page.getByText(englishCommon.search.empty)).toBeVisible();
  });
});

test.describe('the palette of the back office', () => {
  test.beforeEach(async ({ page }) => {
    await stubTheApiAsStaff(page);
    await stubTheSearch(page, hits);

    page.on('pageerror', (error) => {
      throw new Error(`The page threw: ${error.message}`);
    });
  });

  test('control and K opens it, and it offers the screens of the back office', async ({ page }) => {
    await page.goto('/staff/ed/links');

    // ⚠️ Waited for on purpose. A key pressed before React has attached its listener is a key
    // nobody hears, and the test then waits five seconds for a dialog that was never asked for —
    // which looks exactly like a broken shortcut and is not one. A person cannot press a key
    // before the page is there either.
    await expect(page.getByRole('heading', { name: englishCommon.links.title })).toBeVisible();
    await expect(page.getByRole('dialog')).toHaveCount(0);

    await page.keyboard.press('Control+k');

    const palette = page.getByRole('dialog');
    await expect(palette).toBeVisible();

    // The screens this member may reach, which are the same ones the sidebar draws: one list, read
    // twice. `ED` is the only department of this fixture, written by its name since 11 September
    // 2026, as the sidebar writes it.
    await expect(
      palette.getByText(`${englishCommon.departments.ED} — ${englishCommon.links.title}`),
    ).toBeVisible();
  });

  test('a hit matched on its body is offered, and is not filtered out again', async ({ page }) => {
    // ⚠️ The assertion the palette exists to get right. `cmdk` filters the items it is handed
    // against what has been typed, and this row says "aerodrome" only in its snippet — so with the
    // filtering left on it would vanish, and the search would look broken for exactly the results a
    // FULLTEXT index is for.
    await page.goto('/staff/ed/links');
    await expect(page.getByRole('heading', { name: englishCommon.links.title })).toBeVisible();
    await page.keyboard.press('Control+k');

    const palette = page.getByRole('dialog');
    await palette.getByRole('combobox').fill('aerodrome');

    await expect(palette.getByText('Getting started')).toBeVisible();
  });
});
