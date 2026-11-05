import { expect, test, type Page } from '@playwright/test';

import { oneMedia, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The search box of a generated list, on a real route: what is being searched for is in the address, so
 * the box hears of its own search from the router, late, and of the back button the same way. Which of
 * the two it is hearing is what these are about. `/staff/media` is the screen it was seen on, and any
 * other list draws the same box.
 */

const box = (page: Page) => page.getByRole('searchbox', { name: englishCommon.list.search });

/** What the address says is being searched for. */
const term = (page: Page) => expect.poll(() => new URL(page.url()).searchParams.get('q'));

test('somebody who writes slowly goes on writing, and the back button goes back over the searches', async ({
  page,
}) => {
  await stubTheApiAsStaff(page);
  await page.goto('/staff/media');

  await box(page).click();
  await page.keyboard.type('run');
  await term(page).toBe('run');

  await expect(box(page)).toBeFocused();
  await page.keyboard.type('way');
  await expect(box(page)).toHaveValue('runway');
  await term(page).toBe('runway');
  await expect(box(page)).toBeFocused();

  await page.goBack();
  await term(page).toBe('run');
  await expect(box(page)).toHaveValue('run');

  await page.goBack();
  await term(page).toBeNull();
  await expect(box(page)).toHaveValue('');

  await page.goForward();
  await term(page).toBe('run');
  await expect(box(page)).toHaveValue('run');
});

test('a slow answer does not take back what was written while it was on its way', async ({ page }) => {
  await stubTheApiAsStaff(page);

  // Registered last, so it is asked first. The list hears of a term only when the loader has its answer.
  let answer = (): void => undefined;
  const held = new Promise<void>((resolve) => {
    answer = resolve;
  });
  const asked: string[] = [];
  await page.route('**/api/media?**', async (route) => {
    const q = new URL(route.request().url()).searchParams.get('q');
    if (q !== null) {
      asked.push(q);
      await held;
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(oneMedia) });
  });

  await page.goto('/staff/media');
  await box(page).click();
  await page.keyboard.type('tow');
  await expect.poll(() => asked).toContain('tow');

  await page.keyboard.type('e');
  answer();

  await term(page).toBe('towe');
  await expect(box(page)).toHaveValue('towe');
  await expect(box(page)).toBeFocused();
});
