import { expect, test } from '@playwright/test';

import { stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The editor is three columns: the components on the left, the page in the middle, the properties
 * of whatever is selected on the right (asked for by Carmine on 10 September 2026).
 *
 * A unit test can say the palette renders and that a click calls the right thing. What it cannot
 * say is that the three are **beside** each other in a real window at a real width — which is the
 * whole of the request, and the kind of thing that has been wrong here before while every unit test
 * stayed green.
 */

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

const editor = englishCommon.content.editor;

test('the three panels are side by side, in order, at a desktop width', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/ed/content/1');

  const palette = page.getByRole('heading', { name: editor.components });
  // The middle column opens on the page itself, not on the outline: composing by looking at it was
  // the road chosen on 9 September, and it should not need a button pressed to be reached.
  //
  // ⚠️ Anchored on the width switch above the frame rather than on the frame itself: this row of
  // the fixture has no sections, so the preview renders nothing, and an element of no height is not
  // visible to a browser however correctly it is placed.
  const middle = page.getByRole('button', { name: editor.previewWidths.desktop });
  const properties = page.getByRole('heading', { name: editor.properties });

  await expect(palette).toBeVisible();
  await expect(middle).toBeVisible();
  await expect(properties).toBeVisible();

  const left = await palette.boundingBox();
  const centre = await middle.boundingBox();
  const right = await properties.boundingBox();

  // Measured, not assumed. Three columns that have quietly become three rows still pass every
  // assertion above.
  expect(left!.x).toBeLessThan(centre!.x);
  expect(centre!.x).toBeLessThan(right!.x);

  // And on the same line: a wrapped grid puts the third one underneath, at the same x as the first.
  expect(Math.abs(left!.y - right!.y)).toBeLessThan(80);
});

test('the outline swaps the middle column and leaves the other two where they are', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/ed/content/1');

  const palette = page.getByRole('heading', { name: editor.components });
  const before = await palette.boundingBox();

  await page.getByRole('button', { name: editor.outline }).click();

  await expect(page.getByRole('heading', { name: editor.structure })).toBeVisible();
  await expect(page.getByRole('button', { name: editor.previewWidths.desktop })).toHaveCount(0);

  // The point of the frame: the two side panels do not move when the middle one changes.
  const after = await palette.boundingBox();
  expect(after!.x).toBe(before!.x);

  await expect(page.getByRole('heading', { name: editor.properties })).toBeVisible();
});

test('a component from the palette lands in the section that was selected', async ({ page }) => {
  await page.goto('/staff/ed/content/1');

  // Nothing selected to begin with, and the palette says so rather than doing nothing when clicked.
  await expect(page.getByText(editor.componentsHint)).toBeVisible();

  // A section, made the way a coordinator makes one: the row this fixture carries has none, which
  // is also the state a page starts in.
  await page.getByRole('button', { name: editor.outline }).click();
  await page.getByRole('button', { name: editor.addSection }).first().click();

  // Adding a section selects it, so the palette now says where a click would land.
  await expect(page.getByText(/^Adds to:/)).toBeVisible();

  const heading = page
    .getByLabel(englishCommon.blocks.subgroups.text)
    .getByRole('button', { name: englishCommon.blocks.heading.label });

  await expect(heading).toBeEnabled();
  await heading.click();

  // The block was added *and* selected, so the panel on the right is a block's properties now
  // rather than the page's: that form is the only one with this button on it.
  await expect(page.getByRole('button', { name: editor.applyBlock })).toBeVisible();

  // And it is in the page, not only in the panel: the outline has a row for it.
  await expect(
    page.getByRole('listitem').filter({ hasText: englishCommon.blocks.heading.label }),
  ).toHaveCount(1);
});
