import { expect, test } from '@playwright/test';

import { siteStaffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The closed suggestion — a `SchemaForm` field with `suggestionsOnly` — keeps the option that is
 * clicked after part of it was typed (phase A6c of M3, note
 * `decisions/2026-09-26-il-suggerimento-chiuso-tiene-la-scelta.md`).
 *
 * ⚠️ The defect this exists for, found while writing the request page of the training (A6b) and
 * measured in a browser on 26 September 2026: whoever typed part of a value to search the list and
 * then clicked the option found the box holding what it held before — empty, on a new row. Pressing
 * the option moved the focus into the list; leaving the box with something nobody offered puts back
 * what was there, which is the rule of a closed field; the list grew back to every row under the
 * pointer, and the click landed on another row. Clicking the box and then an option, without
 * typing, worked — which is all `back-office.spec.ts` does, and why it stayed green.
 *
 * jsdom cannot see it: it lays nothing out, so a click there reaches the element it was aimed at
 * whatever has moved under the pointer. The address of a menu entry is the closed field of the core
 * on a screen this suite already opens; every other closed field is the same component.
 */

// ⚠️ Scrollbars drawn, which headless Chromium does not do unless asked: the last test presses one.
// The obvious fix — cancelling the press on the list, so the box keeps the focus — is exactly what
// stops a scrollbar from dragging, and only a scrollbar that is there can say so.
test.use({ launchOptions: { ignoreDefaultArgs: ['--hide-scrollbars'] } });

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page, siteStaffBootstrap);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('an option clicked after typing part of it is the one the box keeps', async ({ page }) => {
  await page.goto('/staff/wd/menu/3');

  const address = page.getByLabel(englishCommon.menu.fields.path, { exact: true });
  await expect(address).toHaveValue('/pilots');

  await address.click();
  await address.fill('cal');

  // Narrowed to the one option it matches, and the rest of this test is about that: before the fix
  // the list grew back to every address the moment the option was pressed, and the row under the
  // pointer was no longer the one pressed.
  await expect(page.getByRole('option')).toHaveCount(1);
  await page.getByRole('option').filter({ hasText: '/calendar' }).click();

  await expect(address).toHaveValue('/calendar');
  await expect(page.getByRole('listbox')).toBeHidden();

  // ⚠️ And what comes back when the field is left with something nobody offered is **that choice**,
  // not the address the box held when it was first clicked: after a choice, the choice is what was
  // there.
  await address.fill('somewhere else');
  await expect(page.getByText(englishCommon.form.suggest.emptyClosed)).toBeVisible();
  await page.getByLabel(englishCommon.menu.fields.sort, { exact: true }).click();
  await expect(address).toHaveValue('/calendar');
});

test('a press in the list that chooses nothing keeps the search, and leaving from there still puts back what was there', async ({
  page,
}) => {
  await page.goto('/staff/wd/menu/3');

  const address = page.getByLabel(englishCommon.menu.fields.path, { exact: true });
  await address.click();
  await address.fill('cal');
  await expect(page.getByRole('option')).toHaveCount(1);

  // A heading is part of the field: pressing it takes the focus into the list, and what is typed
  // stays what it was — still a search, still narrowing.
  await page.getByText(englishCommon.menu.screensGroup).click();
  await expect(address).toHaveValue('cal');
  await expect(page.getByRole('option')).toHaveCount(1);

  // ⚠️ Leaving the field **from the list** is still leaving it. The box saw no blur this time — it
  // lost the focus to the list, not to the order — so it is the list closing that applies the rule.
  await page.getByLabel(englishCommon.menu.fields.sort, { exact: true }).click();
  await expect(address).toHaveValue('/pilots');
});

test('back in the box from the list, the search goes on from where it was', async ({ page }) => {
  await page.goto('/staff/wd/menu/3');

  const address = page.getByLabel(englishCommon.menu.fields.path, { exact: true });
  await address.click();
  await address.fill('cal');
  await page.getByText(englishCommon.menu.screensGroup).click();

  // Arriving in the box reads what it holds as the starting point of a search; coming back to it
  // from its own list must not, or "cal" would become what was there — a value nobody offered, which
  // the rule would then put back.
  await address.click();
  await expect(page.getByRole('option')).toHaveCount(1);

  await page.getByLabel(englishCommon.menu.fields.sort, { exact: true }).click();
  await expect(address).toHaveValue('/pilots');
});

test('Escape closes the list and leaves what is typed until the field is left', async ({ page }) => {
  await page.goto('/staff/wd/menu/3');

  const address = page.getByLabel(englishCommon.menu.fields.path, { exact: true });
  await address.click();
  await address.fill('cal');
  await expect(page.getByRole('option')).toHaveCount(1);

  await page.keyboard.press('Escape');
  await expect(page.getByRole('listbox')).toBeHidden();
  await expect(address).toBeFocused();
  await expect(address).toHaveValue('cal');

  await page.getByLabel(englishCommon.menu.fields.sort, { exact: true }).click();
  await expect(address).toHaveValue('/pilots');
});

test('the scrollbar of the list drags, keeps the search, and the option is chosen after it', async ({
  page,
}) => {
  await page.goto('/staff/wd/menu/3');

  const address = page.getByLabel(englishCommon.menu.fields.path, { exact: true });
  await address.click();
  // Matched by most of the addresses and none of them: a search long enough to scroll, which a press
  // that counted as leaving the field would throw away.
  await address.fill('e');

  const list = page.getByRole('listbox');
  await expect(list).toBeVisible();
  await expect(page.getByRole('option').filter({ hasText: 'https://example.org/discord' })).toHaveCount(1);

  const { scrollbar, overflow } = await list.evaluate((element) => ({
    scrollbar: (element as HTMLElement).offsetWidth - element.clientWidth,
    overflow: element.scrollHeight - element.clientHeight,
  }));
  expect(scrollbar, 'the list draws a scrollbar to press').toBeGreaterThan(0);
  expect(overflow, 'the list is longer than its box').toBeGreaterThan(0);

  // The thumb, dragged down with the mouse.
  const frame = (await list.boundingBox())!;
  const x = frame.x + frame.width - scrollbar / 2;
  await page.mouse.move(x, frame.y + 40);
  await page.mouse.down();
  await page.mouse.move(x, frame.y + 140, { steps: 8 });
  await page.mouse.up();

  expect(await list.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
  await expect(address).toHaveValue('e');

  await page.getByRole('option').filter({ hasText: 'https://example.org/discord' }).click();
  await expect(address).toHaveValue('https://example.org/discord');
});
