import { expect, test, type Page } from '@playwright/test';

import { stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The formatting bar (design of the editor, phase 3, §4.6): it writes Markdown around the selection, and the page beside it
 * draws what was written. In a browser because the two popovers hold a selection the box has lost the focus of, and because a
 * list that opened and closed on the same click has happened here before while every unit test stayed green.
 */

const blocks = englishCommon.blocks;
const editor = englishCommon.content.editor;
const words = englishCommon.form.markdown;

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

async function addBlock(page: Page, label: string) {
  await page.goto('/staff/content/1');
  await page.getByRole('button', { name: editor.outline }).click();
  await page.getByRole('button', { name: editor.addSection }).first().click();
  await page.getByRole('searchbox', { name: editor.searchComponents }).fill(label);
  await page.getByRole('button', { name: label, exact: true }).click();
}

/** The box of the block just added, holding `text`, with `selected` selected. */
async function writeAndSelect(page: Page, text: string, selected: string) {
  const box = page.getByRole('toolbar', { name: words.bar }).locator('xpath=following-sibling::textarea');
  await box.fill(text);
  await box.evaluate((element: HTMLTextAreaElement, part) => {
    const from = element.value.indexOf(part);
    element.focus();
    element.setSelectionRange(from, from + part.length);
  }, selected);
  return box;
}

const bar = (page: Page) => page.getByRole('toolbar', { name: words.bar });

test('a button writes the mark, and the page draws it', async ({ page }) => {
  await addBlock(page, blocks.text.label);
  const box = await writeAndSelect(page, 'One word here', 'word');

  await page.getByRole('button', { name: editor.onThePage }).click();
  await box.evaluate((element: HTMLTextAreaElement) => element.setSelectionRange(4, 8));
  await bar(page).getByRole('button', { name: words.bold }).click();

  await expect(box).toHaveValue('One **word** here');
  await expect(page.locator('strong', { hasText: 'word' })).toBeVisible();

  await bar(page).getByRole('button', { name: words.strike }).click();

  await expect(box).toHaveValue('One **~~word~~** here');
  await expect(page.locator('del', { hasText: 'word' })).toBeVisible();
});

test('the shortcut writes the same', async ({ page }) => {
  await addBlock(page, blocks.text.label);
  const box = await writeAndSelect(page, 'One word here', 'word');

  await box.press('Control+b');
  await expect(box).toHaveValue('One **word** here');

  await box.press('Control+i');
  await expect(box).toHaveValue('One ***word*** here');
});

test('a colour keeps the selection through its list, and the page draws it', async ({ page }) => {
  await addBlock(page, blocks.text.label);
  const box = await writeAndSelect(page, 'One word here', 'word');

  await page.getByRole('button', { name: editor.onThePage }).click();
  await box.evaluate((element: HTMLTextAreaElement) => element.setSelectionRange(4, 8));
  await bar(page).getByRole('button', { name: words.colour }).click();
  await page.getByRole('button', { name: words.colours.ocean, exact: true }).click();

  await expect(box).toHaveValue('One :ocean[word] here');
  await expect(page.locator('.text-colour-ocean', { hasText: 'word' })).toBeVisible();
  await expect(box).toBeFocused();
});

test('Escape closes the list of the colours and leaves the block picked, and Delete does not remove it', async ({
  page,
}) => {
  await addBlock(page, blocks.text.label);
  await writeAndSelect(page, 'One word here', 'word');

  const colour = bar(page).getByRole('button', { name: words.colour });
  const blue = page.getByRole('button', { name: words.colours.brand, exact: true });

  await colour.click();
  await blue.focus();
  await page.keyboard.press('Delete');
  await expect(bar(page)).toBeVisible();
  await expect(blue).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(blue).toHaveCount(0);
  await expect(bar(page)).toBeVisible();
});

test('from the list of the colours straight to the link: the link stays open, and writes', async ({
  page,
}) => {
  await addBlock(page, blocks.text.label);
  const box = await writeAndSelect(page, 'One word here', 'word');

  await bar(page).getByRole('button', { name: words.colour }).click();
  await expect(page.getByRole('button', { name: words.colours.brand, exact: true })).toBeVisible();

  await bar(page).getByRole('button', { name: words.link }).click();
  const address = page.getByRole('combobox', { name: words.linkAddress });
  await expect(page.getByRole('button', { name: words.colours.brand, exact: true })).toHaveCount(0);

  await address.fill('https://forum.example/board');
  await page.getByRole('button', { name: words.linkApply }).click();

  await expect(box).toHaveValue('One [word](https://forum.example/board) here');
});

test('a link is chosen among the pages of the site, and an address of elsewhere is written in full', async ({
  page,
}) => {
  await addBlock(page, blocks.text.label);
  const box = await writeAndSelect(page, 'One word here', 'word');

  await bar(page).getByRole('button', { name: words.link }).click();
  const address = page.getByRole('combobox', { name: words.linkAddress });
  await address.click();
  await page.getByRole('option', { name: /Team/ }).click();
  await expect(address).toHaveValue('/about/team');
  await page.getByRole('button', { name: words.linkApply }).click();

  await expect(box).toHaveValue('One [word](/about/team) here');
  await expect(box).toBeFocused();

  // The caret in the link: the same address comes back, and another takes its place.
  await box.evaluate((element: HTMLTextAreaElement) => element.setSelectionRange(6, 6));
  await bar(page).getByRole('button', { name: words.link }).click();
  await expect(address).toHaveValue('/about/team');
  await address.fill('https://forum.example/board');
  await address.press('Enter');

  await expect(box).toHaveValue('One [word](https://forum.example/board) here');
});

test('a callout is prose and has the bar; a heading of a hero is a line of a drawing and has none', async ({
  page,
}) => {
  await addBlock(page, blocks.callout.label);
  await expect(bar(page)).toBeVisible();

  await addBlock(page, blocks.hero.label);
  await expect(bar(page)).toHaveCount(0);
});
