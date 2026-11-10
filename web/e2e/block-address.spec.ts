import { expect, test, type Page } from '@playwright/test';

import { oneTemplate, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The address of a button, a card or a logo is chosen from the site, or written in full (design of the editor,
 * phase 3, §4.5; answer 5 of Carmine, 31 October 2026).
 *
 * In a browser and on every block that has one, because the field is at a different depth in each: at the top of
 * the retired button, in an optional object of a hero, in a list everywhere else. And a list that opened and closed
 * on the same click has happened here before while every unit test stayed green.
 */

const editor = englishCommon.content.editor;
const blocks = englishCommon.blocks;

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

/**
 * The whole of the request, on one field: the list says what exists, grouped, whichever department wrote it; a page
 * chosen writes its address; and an address of somewhere else, which the list does not offer, stays.
 */
async function chooseAndWrite(page: Page, address: ReturnType<Page['getByRole']>) {
  await address.click();

  await expect(page.getByText(englishCommon.departments.WD, { exact: true })).toBeVisible();
  await expect(page.getByText(englishCommon.menu.screensGroup)).toBeVisible();
  await expect(page.getByText(englishCommon.menu.linksGroup)).toBeVisible();

  await page.getByRole('option', { name: /Team/ }).click();
  await expect(address).toHaveValue('/about/team');

  await address.fill('https://forum.example/board');
  await address.press('Tab');
  await expect(address).toHaveValue('https://forum.example/board');
}

test('a button of a group', async ({ page }) => {
  await addBlock(page, blocks.buttonGroup.label);
  await page.getByRole('button', { name: englishCommon.form.addEntry, exact: true }).click();

  await chooseAndWrite(page, page.getByRole('combobox', { name: blocks.buttonGroup.fields['buttons.href'] }));
});

test('a card', async ({ page }) => {
  await addBlock(page, blocks.cardGrid.label);
  await page.getByRole('button', { name: englishCommon.form.addEntry, exact: true }).click();

  await chooseAndWrite(page, page.getByRole('combobox', { name: blocks.cardGrid.fields['cards.href'] }));
});

test('a logo', async ({ page }) => {
  await addBlock(page, blocks.logoGrid.label);
  await page.getByRole('button', { name: englishCommon.form.addEntry, exact: true }).click();

  await chooseAndWrite(page, page.getByRole('combobox', { name: blocks.logoGrid.fields['items.href'] }));
});

test('the two buttons of a hero, each on its own', async ({ page }) => {
  await addBlock(page, blocks.hero.label);

  const [main, second] = [
    page.locator('[id="primary.href"]'),
    page.locator('[id="secondary.href"]'),
  ] as const;

  await chooseAndWrite(page, main);
  await chooseAndWrite(page, second);

  // The second one did not write over the first.
  await expect(main).toHaveValue('https://forum.example/board');
});

test('the retired button of a page that still has one', async ({ page }) => {
  // No longer in the palette, so it comes with the row.
  await page.route('**/api/content/1', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...oneTemplate,
        body: {
          schemaVersion: 1,
          sections: [
            {
              id: 'sec-old',
              layout: 'stacked',
              background: 'none',
              padding: 'md',
              width: 'default',
              blocks: [
                {
                  id: 'blk-cta',
                  type: 'cta',
                  version: 1,
                  props: { label: { en: 'Old', it: 'Vecchio' }, href: '/forum' },
                },
              ],
              sections: [],
            },
          ],
        },
      }),
    }),
  );

  await page.goto('/staff/content/1');
  await page.getByRole('button', { name: editor.outline }).click();
  // Only the outline has it: a retired block is not in the palette.
  await page.getByRole('button', { name: blocks.cta.label, exact: true }).click();

  const address = page.getByRole('combobox', { name: blocks.cta.fields.href });

  // What it held is still there, though nothing offers it.
  await expect(address).toHaveValue('/forum');
  await chooseAndWrite(page, address);
});

test('what the block holds is what was written, on the page as well as in the panel', async ({ page }) => {
  await addBlock(page, blocks.buttonGroup.label);
  await page.getByRole('button', { name: englishCommon.form.addEntry, exact: true }).click();

  const address = page.getByRole('combobox', { name: blocks.buttonGroup.fields['buttons.href'] });
  await address.click();
  await page.getByRole('option', { name: /Team/ }).click();

  await page
    .getByRole('group', { name: blocks.buttonGroup.fields['buttons.label'] })
    .getByRole('textbox')
    .fill('Meet the team');
  await page.getByRole('button', { name: editor.onThePage, exact: true }).click();

  await expect(
    page.getByRole('region', { name: editor.preview }).getByRole('link', { name: 'Meet the team' }),
  ).toHaveAttribute('href', '/about/team');
});
