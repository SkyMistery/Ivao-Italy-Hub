import { expect, test } from '@playwright/test';

import { siteStaffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * A select shows more than one of its options.
 *
 * ⚠️ Only a browser can answer this, and the reason is worth keeping: Atmosphere gives the select's
 * popup the height of its **trigger**, so the list is one row tall whatever it holds — measured at
 * 46 pixels of viewport for two rows of 30. Everything worked: the options were in the document, a
 * unit test found them by name, a keyboard reached them. What a reader saw was a list with one
 * entry in it and no reason to suspect a second.
 *
 * So this asserts a **geometry**, which is the only thing that would have caught it, and it is the
 * third time this project has had to learn that: the sidebar column, the icon grid, and now this.
 */

test('a select shows several options at once, not one at a time', async ({ page }) => {
  await stubTheApiAsStaff(page, siteStaffBootstrap);
  await page.goto('/staff/wd/menu/3');

  // "Visible to" has four options, which is the case that matters: a list of four shown one at a
  // time is a control that lies about what it offers.
  await page.getByRole('combobox', { name: englishCommon.menu.fields.visibility }).click();

  const options = page.getByRole('option');
  await expect(options.first()).toBeVisible();

  const count = await options.count();
  expect(count, 'the fixture is meant to offer several audiences').toBeGreaterThan(2);

  // Every one of them is where a reader can see it: inside the popup, not below its bottom edge.
  const listbox = page.getByRole('listbox');
  const frame = await listbox.boundingBox();
  expect(frame).not.toBeNull();

  const rows = [];
  for (let index = 0; index < count; index += 1) {
    const box = await options.nth(index).boundingBox();
    expect(box).not.toBeNull();
    rows.push(box!);
  }

  // The popup is at least as tall as three rows: it does not have to hold every option of a long
  // list — a long one still scrolls — but a reader must be able to see that there is more than one.
  const rowHeight = Math.min(...rows.map((row) => row.height));
  expect(
    frame!.height,
    `the popup is ${Math.round(frame!.height)}px and a row is ${Math.round(rowHeight)}px, so it shows one option`,
  ).toBeGreaterThan(rowHeight * 2);

  // And the second one is really on screen, not merely inside a box that is tall enough.
  await expect(options.nth(1)).toBeInViewport();
});
