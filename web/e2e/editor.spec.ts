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
  await page.goto('/staff/content/1');

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
  await page.goto('/staff/content/1');

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
  await page.goto('/staff/content/1');

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
  // rather than the page's: a heading's level is a field no other form on this screen has.
  await expect(page.getByText(englishCommon.blocks.heading.fields.level, { exact: true })).toBeVisible();

  // And it is in the page, not only in the panel: the outline has a row for it.
  await expect(
    page.getByRole('listitem').filter({ hasText: englishCommon.blocks.heading.label }),
  ).toHaveCount(1);

  // Back on the page, the block just added is the picked one, and it carries its own commands
  // (Carmine, 11 September 2026: removing a block should not need the outline). Pressing "remove"
  // there takes it out of the page and, when the outline is asked again, out of the outline.
  await page.getByRole('button', { name: editor.onThePage, exact: true }).click();
  const frame = page.getByRole('region', { name: editor.preview });
  await frame.getByRole('button', { name: englishCommon.content.editor.remove, exact: true }).click();

  await page.getByRole('button', { name: editor.outline, exact: true }).click();
  await expect(
    page.getByRole('listitem').filter({ hasText: englishCommon.blocks.heading.label }),
  ).toHaveCount(0);

  // And a section is offered at the end of the page itself: pressing it adds a second one.
  await page.getByRole('button', { name: editor.onThePage, exact: true }).click();
  await frame.getByRole('button', { name: editor.addSection, exact: true }).click();
  await page.getByRole('button', { name: editor.outline, exact: true }).click();
  await expect(page.getByRole('button', { name: editor.removeSection })).toHaveCount(2);
});

test('a section is inserted between two others, by the pointer or the keyboard, and a row among rows', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/content/1');

  const frame = page.getByRole('region', { name: editor.preview });
  const labels = frame.locator('[data-section-label]');
  const section = frame.locator('[data-pickable="section"]');

  // Two sections from the end of the page, with their names, the first one on a ground.
  const named = async (name: string) => {
    await page.getByRole('button', { name: editor.addSection, exact: true }).click();
    await page.locator('#content-properties input:visible').first().fill(name);
    // Applied a moment after the typing stops, and on the section's label as soon as it is.
    await expect(labels.filter({ hasText: name })).toBeVisible();
  };
  await named('First');
  await page
    .getByRole('button', { name: englishCommon.content.section.options.background.accent, exact: true })
    .click();
  await named('Second');
  await expect(labels).toHaveText(['1 · First · pale blue background', '2 · Second']);

  // ⚠️ Nothing on the page moves for any of this, which is the rule of composing on the page: the
  // label and the gap stand out of the flow.
  const before = await section.nth(1).boundingBox();
  const insert = section.nth(1).getByRole('button', { name: editor.insertSection, exact: true });

  // Out of sight until the pointer reaches the place between the two.
  await expect(insert).toHaveCSS('opacity', '0');
  await section.nth(1).locator('[data-insert-gap]').first().hover();
  await expect(insert).toHaveCSS('opacity', '1');
  expect(await section.nth(1).boundingBox()).toEqual(before);

  await insert.click();
  await expect(labels).toHaveText([
    '1 · First · pale blue background',
    '2 · Section without a name',
    '3 · Second',
  ]);

  // The one just inserted is picked, and the panel says what its name is for.
  await expect(section.nth(1)).toHaveAttribute('data-picked', '');
  await expect(page.getByText(englishCommon.content.section.fields.title, { exact: true })).toBeVisible();
  await expect(page.getByText(englishCommon.content.section.howToDivide)).toBeVisible();

  // The keyboard reaches the same button, and the focus shows it: Tab, because a focus that comes from
  // a mouse or a script is not one the browser shows.
  const reached = section.nth(2).getByRole('button', { name: editor.insertSection, exact: true });
  await reached.focus();
  await page.keyboard.press('Shift+Tab');
  await page.keyboard.press('Tab');
  await expect(reached).toBeFocused();
  await expect(reached).toHaveCSS('opacity', '1');

  // A row inside the second, then one inside that: the number says where each one stands.
  await section.nth(1).getByRole('button', { name: editor.insertRow, exact: true }).first().focus();
  await page.keyboard.press('Enter');
  await expect(labels).toHaveText([
    '1 · First · pale blue background',
    '2 · Section without a name',
    'Row 2.1',
    '3 · Second',
  ]);

  // The row is what is picked: its own air takes the click, not the section around it.
  const row = section.nth(2);
  await expect(row).toHaveAttribute('data-picked', '');
  await labels.nth(1).click();
  await expect(section.nth(1)).toHaveAttribute('data-picked', '');
  await labels.nth(2).click();
  await expect(row).toHaveAttribute('data-picked', '');

  // The second of the row's two: the first stands before it, among the rows of its section.
  await row.getByRole('button', { name: editor.insertRow, exact: true }).nth(1).focus();
  await page.keyboard.press('Enter');
  await expect(labels).toHaveText([
    '1 · First · pale blue background',
    '2 · Section without a name',
    'Row 2.1',
    'Row 2.1.1',
    '3 · Second',
  ]);
});

test("a calendar block's kinds are chosen from the calendar's own, not typed (issue #360)", async ({
  page,
}) => {
  await page.goto('/staff/content/1');

  await page.getByRole('button', { name: editor.outline }).click();
  await page.getByRole('button', { name: editor.addSection }).first().click();
  await page
    .getByRole('searchbox', { name: editor.searchComponents })
    .fill(englishCommon.blocks.calendar.label);
  await page.getByRole('button', { name: englishCommon.blocks.calendar.label, exact: true }).click();

  await page.getByRole('button', { name: englishCommon.form.addEntry, exact: true }).click();

  // A select, with the words of the division (the fixture's two kinds) and nothing to type into.
  const kind = page.getByRole('combobox', { name: englishCommon.blocks.calendar.fields['kinds.kind'] });
  await kind.click();
  await expect(page.getByRole('option', { name: 'Meeting' })).toBeVisible();
  await expect(page.getByRole('option', { name: 'Deadline' })).toBeVisible();
  await page.getByRole('option', { name: 'Deadline' }).click();
  await expect(kind).toHaveText('Deadline');
});
