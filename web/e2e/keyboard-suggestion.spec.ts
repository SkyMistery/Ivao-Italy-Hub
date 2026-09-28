import { expect, test, type Page, type Request } from '@playwright/test';

import { siteStaffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * A suggested field — a `SchemaForm` field with `suggestions` — chosen from the keyboard alone:
 * type part of a value, move with the arrows, Enter (note
 * `decisions/2026-09-28-il-suggerimento-dalla-tastiera.md`).
 *
 * ⚠️ The defect this exists for, found on 27 September 2026 in the review of A6c (#145) and of the
 * request of a training (#144): the box sits outside `cmdk`'s root, so the arrows did not move
 * through the options and Enter did not choose one; and Enter, being nobody's, was the browser's —
 * it sent the form, in the middle of a search.
 *
 * In a browser because the half that matters is the browser's: the form it sends on Enter, and the
 * row it scrolls into sight. The address of a menu entry is the closed field of the core on a screen
 * this suite already opens, and every other suggested field is the same component.
 */

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page, siteStaffBootstrap);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

/** Every save of the entry the page sends, in order. */
function saves(page: Page): Request[] {
  const sent: Request[] = [];
  page.on('request', (request) => {
    if (request.method() === 'PUT' && request.url().includes('/api/menu/3')) {
      sent.push(request);
    }
  });
  return sent;
}

const isTheSave = (request: Request) => request.method() === 'PUT' && request.url().includes('/api/menu/3');

test('part of an address typed, then Enter, chooses the option it matches and saves nothing', async ({
  page,
}) => {
  const sent = saves(page);
  await page.goto('/staff/wd/menu/3');

  const address = page.getByRole('combobox', { name: englishCommon.menu.fields.path, exact: true });
  await address.click();
  await address.fill('cal');

  // The search lights the one option it matches, and the box points at it: a screen reader hears it
  // while the focus stays where the typing is.
  const lit = page.getByRole('option', { selected: true });
  await expect(lit).toHaveAttribute('data-value', '/calendar');
  await expect(address).toHaveAttribute('aria-expanded', 'true');
  await expect(address).toHaveAttribute('aria-activedescendant', (await lit.getAttribute('id'))!);
  await expect(address).toHaveAttribute(
    'aria-controls',
    (await page.getByRole('listbox').getAttribute('id'))!,
  );

  await address.press('Enter');

  await expect(address).toHaveValue('/calendar');
  await expect(page.getByRole('listbox')).toBeHidden();
  await expect(address).toBeFocused();

  // ⚠️ And the Enter was a choice and nothing else. The next one is the form's: it saves, and it is
  // the first save of the page — so the one before sent nothing.
  const saved = page.waitForRequest(isTheSave);
  await address.press('Enter');
  expect(JSON.parse((await saved).postData() ?? '{}')).toMatchObject({ path: '/calendar' });
  expect(sent).toHaveLength(1);
});

test('the arrows walk a long list from the box, with the lit row in sight, and Enter takes it', async ({
  page,
}) => {
  await page.goto('/staff/wd/menu/3');

  const address = page.getByRole('combobox', { name: englishCommon.menu.fields.path, exact: true });
  await address.click();
  // Matched by most of the addresses: a list longer than its box.
  await address.fill('e');

  const list = page.getByRole('listbox');
  expect(await list.evaluate((element) => element.scrollHeight > element.clientHeight)).toBe(true);

  const options = page.getByRole('option');
  const lit = page.getByRole('option', { selected: true });
  const count = await options.count();

  // The first is lit by the search; the arrows go down to the last, which is out of sight until the
  // list follows.
  await expect(lit).toHaveAttribute('data-value', (await options.first().getAttribute('data-value'))!);
  for (let step = 1; step < count; step += 1) {
    await address.press('ArrowDown');
  }

  const last = (await options.last().getAttribute('data-value'))!;
  await expect(lit).toHaveAttribute('data-value', last);
  await expect(lit).toBeInViewport();
  await expect(address).toHaveAttribute('aria-activedescendant', (await lit.getAttribute('id'))!);
  await expect(address).toBeFocused();
  // Never the caret: an arrow in a one line box would only have jumped it to an end.
  expect(await address.evaluate((box: HTMLInputElement) => box.selectionStart)).toBe(1);

  // Past the last there is nowhere to go, and up goes back one.
  await address.press('ArrowDown');
  await expect(lit).toHaveAttribute('data-value', last);
  await address.press('ArrowUp');
  const oneBefore = (await options.nth(count - 2).getAttribute('data-value'))!;
  await expect(lit).toHaveAttribute('data-value', oneBefore);

  await address.press('Enter');
  await expect(address).toHaveValue(oneBefore);
});

test('Enter on an address nobody searched saves the entry, as in any line of the form', async ({ page }) => {
  await page.goto('/staff/wd/menu/3');

  const address = page.getByRole('combobox', { name: englishCommon.menu.fields.path, exact: true });
  await address.click();

  // ⚠️ The list opens on focus, and nothing in it is lit: somebody walking through the form with
  // Enter has chosen nothing, and saves what the field holds.
  await expect(page.getByRole('listbox')).toBeVisible();
  await expect(page.getByRole('option', { selected: true })).toHaveCount(0);
  await expect(address).not.toHaveAttribute('aria-activedescendant');

  const saved = page.waitForRequest(isTheSave);
  await address.press('Enter');
  expect(JSON.parse((await saved).postData() ?? '{}')).toMatchObject({ path: '/pilots' });
});

test('a search nobody finished is never saved: Enter opens the list again, and the next one chooses', async ({
  page,
}) => {
  const sent = saves(page);
  await page.goto('/staff/wd/menu/3');

  const address = page.getByRole('combobox', { name: englishCommon.menu.fields.path, exact: true });
  await address.click();
  await address.fill('cal');
  await address.press('Escape');
  await expect(page.getByRole('listbox')).toBeHidden();

  // The closed list, with a search in the box that is not an address: Enter is not the form's.
  await address.press('Enter');
  await expect(page.getByRole('listbox')).toBeVisible();
  await expect(address).toHaveValue('cal');
  await expect(page.getByRole('option', { selected: true })).toHaveAttribute('data-value', '/calendar');

  await address.press('Enter');
  await expect(address).toHaveValue('/calendar');

  const saved = page.waitForRequest(isTheSave);
  await address.press('Enter');
  await saved;
  expect(sent).toHaveLength(1);
});

test('an arrow opens the list that Escape closed, on its first option', async ({ page }) => {
  await page.goto('/staff/wd/menu/3');

  const address = page.getByRole('combobox', { name: englishCommon.menu.fields.path, exact: true });
  await address.click();
  await address.press('Escape');
  await expect(page.getByRole('listbox')).toBeHidden();
  await expect(address).toHaveAttribute('aria-expanded', 'false');

  await address.press('ArrowDown');
  await expect(page.getByRole('listbox')).toBeVisible();
  await expect(page.getByRole('option', { selected: true })).toHaveAttribute(
    'data-value',
    (await page.getByRole('option').first().getAttribute('data-value'))!,
  );
});
