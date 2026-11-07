import { expect, test, type Page } from '@playwright/test';

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
  // sections stand exactly where they would with the labels and the places to insert gone from the flow.
  const boxes = () =>
    section.evaluateAll((all) => all.map((one) => JSON.stringify(one.getBoundingClientRect())));
  const drawn = await boxes();
  await frame.locator('[data-section-label], [data-insert-gap]').evaluateAll((chrome) => {
    chrome.forEach((one) => ((one as HTMLElement).style.display = 'none'));
  });
  expect(await boxes()).toEqual(drawn);
  await frame.locator('[data-section-label], [data-insert-gap]').evaluateAll((chrome) => {
    chrome.forEach((one) => ((one as HTMLElement).style.display = ''));
  });

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
  // From the pointer, the cursor stays out of the panel: only the keyboard, below, is taken there.
  await expect(page.locator('#content-properties input:focus')).toHaveCount(0);

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

  // A row inside the second, from the place at the end of what it holds, and the cursor goes on to the
  // new row's name: the button it was on now stands somewhere else.
  await section.nth(1).getByRole('button', { name: editor.insertRow, exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(labels).toHaveText([
    '1 · First · pale blue background',
    '2 · Section without a name',
    'Row 2.1',
    '3 · Second',
  ]);
  await expect(page.locator('#content-properties input:focus')).toBeVisible();

  // The row is what is picked; its label takes the click, not the section around it.
  const row = section.nth(2);
  await expect(row).toHaveAttribute('data-picked', '');
  await labels.nth(1).click();
  await expect(section.nth(1)).toHaveAttribute('data-picked', '');
  await labels.nth(2).click();
  await expect(row).toHaveAttribute('data-picked', '');

  // A row inside the row, from its own bar: the number says where each one stands.
  await row.getByRole('button', { name: editor.addRow, exact: true }).click();
  await expect(labels).toHaveText([
    '1 · First · pale blue background',
    '2 · Section without a name',
    'Row 2.1',
    'Row 2.1.1',
    '3 · Second',
  ]);
});

test('a block in a row is picked up to its edges, where a place to insert would otherwise take the click', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/content/1');

  const frame = page.getByRole('region', { name: editor.preview });
  const section = frame.locator('[data-pickable="section"]');

  await page.getByRole('button', { name: editor.addSection, exact: true }).click();
  await section.first().getByRole('button', { name: editor.insertRow, exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(section).toHaveCount(2);

  // A row is born with no padding: a heading in it has no air above it, centred across the whole row.
  await page
    .getByLabel(englishCommon.blocks.subgroups.text)
    .getByRole('button', { name: englishCommon.blocks.heading.label })
    .click();
  await page.locator('#content-properties input:visible').first().fill('Heading in a row');
  const heading = frame.getByRole('heading', { name: 'Heading in a row' });
  await expect(heading).toBeVisible();
  const block = frame.locator('[data-pickable="block"]');

  // Something else picked, so that picking the block is something a click has to do. `click` also
  // refuses when something else would take the pointer at that point.
  const box = (await heading.boundingBox())!;
  for (const position of [
    { x: box.width / 2, y: 1 },
    { x: box.width / 2, y: box.height / 2 },
    { x: box.width / 2, y: box.height - 1 },
    { x: 1, y: 1 },
  ]) {
    await frame.locator('[data-section-label]').first().click();
    await expect(block).not.toHaveAttribute('data-picked', '');

    await heading.click({ position });
    await expect(block).toHaveAttribute('data-picked', '');
    // And nothing was inserted on the way.
    await expect(section).toHaveCount(2);
  }

  // The place before the row is there all the same, in the gap above it, and answers the pointer.
  await frame.locator('[data-section-label]').first().click();
  const gap = section.nth(1).locator('[data-insert-gap]');
  const insert = gap.getByRole('button', { name: editor.insertRow, exact: true });
  await gap.hover();
  await expect(insert).toHaveCSS('opacity', '1');
  await insert.click();
  await expect(section).toHaveCount(3);

  // The keyboard goes down the page: past the row, the place at the end of the section's rows comes last.
  const places = frame.locator('[data-insert-gap] button');
  expect(
    await places.evaluateAll((all) => all.map((one) => (one as HTMLElement).title.split(' ').at(-1))),
  ).toEqual(['1', '1.1', '1.2', '1.3']);
});

test('between two sections with no padding that touch, the way in is the bar, by pointer and by keyboard', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/content/1');

  const frame = page.getByRole('region', { name: editor.preview });
  const labels = frame.locator('[data-section-label]');
  const section = frame.locator('[data-pickable="section"]');

  const bare = async (name: string) => {
    await page.getByRole('button', { name: editor.addSection, exact: true }).click();
    await page.locator('#content-properties input:visible').first().fill(name);
    await expect(labels.filter({ hasText: name })).toBeVisible();
    await page.getByRole('combobox', { name: englishCommon.content.section.fields.padding }).click();
    await page
      .getByRole('option', { name: englishCommon.content.section.options.padding.none, exact: true })
      .click();
    await expect(section.last()).not.toHaveClass(/py-8/);
    await page
      .getByLabel(englishCommon.blocks.subgroups.text)
      .getByRole('button', { name: englishCommon.blocks.heading.label })
      .click();
    await page.locator('#content-properties input:visible').first().fill(`Heading of ${name}`);
    await expect(frame.getByRole('heading', { name: `Heading of ${name}` })).toBeVisible();
  };
  await bare('One');
  await bare('Two');

  // They touch, and where they do the pointer finds what they hold, and no place to insert.
  const one = (await section.nth(0).boundingBox())!;
  const two = (await section.nth(1).boundingBox())!;
  expect(two.y).toBe(one.y + one.height);
  const atTheEdge = await page.evaluate(
    ([x, y]) => document.elementFromPoint(x!, y!)?.closest('[data-insert-gap]') === null,
    [two.x + two.width / 2, two.y],
  );
  expect(atTheEdge).toBe(true);

  // Between the two, from the bar of the one above.
  await labels.nth(0).click();
  await section.nth(0).getByRole('button', { name: editor.sectionBelow, exact: true }).click();
  await expect(labels).toHaveText(['1 · One', '2 · Section without a name', '3 · Two']);
  await expect(section.nth(1)).toHaveAttribute('data-picked', '');
  await expect(page.locator('#content-properties input:focus')).toHaveCount(0);

  // And from the keyboard, above the last, with the cursor going on to the new section's name.
  await labels.nth(2).click();
  await section.nth(2).getByRole('button', { name: editor.sectionAbove, exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(labels).toHaveCount(4);
  await expect(labels.nth(3)).toHaveText('4 · Two');
  await expect(section.nth(2)).toHaveAttribute('data-picked', '');
  await expect(page.locator('#content-properties input:focus')).toBeVisible();
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

const panelWidth = editor.panelWidth;
const REM = 16;

/** Where a line is, and how wide the columns around it really are: what no unit test can measure. */
async function dragLine(page: Page, name: string, by: number) {
  const box = (await page.getByRole('separator', { name }).boundingBox())!;
  const x = box.x + box.width / 2;
  const y = box.y + 100;

  await page.mouse.move(x, y);
  await page.mouse.down();
  // In steps, so the line follows a pointer that travels rather than one that jumps.
  await page.mouse.move(x + by, y, { steps: 5 });
  await page.mouse.up();
}

test('the sidebar shuts on opening the editor, and comes back when you leave it', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/links');

  const expand = page.getByRole('button', { name: englishCommon.nav.sidebar.expand });
  const collapse = page.getByRole('button', { name: englishCommon.nav.sidebar.collapse });
  await expect(collapse).toBeVisible();

  // Inside the application and not by loading the page again, which would start the sidebar over.
  await page.evaluate(() => {
    window.history.pushState({}, '', '/staff/content/1');
  });
  await expect(page.getByRole('heading', { name: editor.components })).toBeVisible();
  await expect(expand).toBeVisible();

  await page.goBack();
  await expect(page.getByRole('heading', { name: englishCommon.links.title })).toBeVisible();
  await expect(collapse).toBeVisible();
});

test('a line is dragged, stops at the limit, is remembered, and a double click puts it back', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/content/1');

  const line = page.getByRole('separator', { name: panelWidth.palette });
  await expect(line).toHaveAttribute('aria-valuenow', '13');

  // 3.5 rem to the right, and the panel on the left is as wide as the line says.
  await dragLine(page, panelWidth.palette, 3.5 * REM);
  await expect(line).toHaveAttribute('aria-valuenow', '16.5');
  const heading = (await page.getByRole('heading', { name: editor.components }).boundingBox())!;
  const moved = (await line.boundingBox())!;
  expect(moved.x - heading.x).toBeCloseTo(16.5 * REM, -1);

  // Dragged far beyond the limit, it stops there.
  await dragLine(page, panelWidth.palette, 40 * REM);
  await expect(line).toHaveAttribute('aria-valuenow', '20');

  // The second line grows its panel to the left.
  const second = page.getByRole('separator', { name: panelWidth.properties });
  await dragLine(page, panelWidth.properties, -2 * REM);
  await expect(second).toHaveAttribute('aria-valuenow', '21');

  await page.reload();
  await expect(line).toHaveAttribute('aria-valuenow', '20');
  await expect(second).toHaveAttribute('aria-valuenow', '21');

  await line.dblclick();
  await expect(line).toHaveAttribute('aria-valuenow', '13');
  await expect(second).toHaveAttribute('aria-valuenow', '21');
});

test('the page never goes under its minimum, by dragging or by narrowing the window', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/staff/content/1');

  const pageWidth = async () => {
    const left = (await page.getByRole('separator', { name: panelWidth.palette }).boundingBox())!;
    const right = (await page.getByRole('separator', { name: panelWidth.properties }).boundingBox())!;
    return (right.x - (left.x + left.width)) / REM;
  };

  // Every panel as wide as it goes, in a window that has no room for both.
  await dragLine(page, panelWidth.palette, 40 * REM);
  await dragLine(page, panelWidth.properties, -60 * REM);
  expect(await pageWidth()).toBeGreaterThanOrEqual(23.9);

  // And the room taken away from under it: the sidebar opened by hand, with the window as it is.
  await page.getByRole('button', { name: englishCommon.nav.sidebar.expand }).click();
  await expect(page.getByRole('button', { name: englishCommon.nav.sidebar.collapse })).toBeVisible();
  expect(await pageWidth()).toBeGreaterThanOrEqual(23.9);

  // The whole page still fits the window: the columns gave way, nothing was pushed off the side.
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test('below the width of three columns there is no line to move', async ({ page }) => {
  await page.setViewportSize({ width: 1100, height: 900 });
  await page.goto('/staff/content/1');

  await expect(page.getByRole('heading', { name: editor.components })).toBeVisible();
  await expect(page.getByRole('separator', { name: panelWidth.palette })).toBeHidden();
  await expect(page.getByRole('separator', { name: panelWidth.properties })).toBeHidden();
});
