import { expect, test, type Page } from '@playwright/test';

import { oneTemplate, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The alternative text of an Image block written before its file is chosen (#377, note
 * 2026-11-15-il-file-ancora-da-scegliere): it stays when the block is left and picked again, and the draft saves it,
 * with the file still to choose. Before, the live form waited for the whole schema and the text went with the panel.
 */

const editor = englishCommon.content.editor;
const blocks = englishCommon.blocks;

interface Written {
  body: { sections: { blocks: { id: string; props: Record<string, unknown> }[] }[] };
}

const row = {
  ...oneTemplate,
  id: 7,
  kind: 'Page',
  slug: 'welcome',
  path: 'welcome',
  ownerDepartment: 'ED',
  visibility: 'Public',
  isTemplate: false,
  publishedAt: null,
  draftFromFile: false,
  title: { en: 'Welcome', it: 'Benvenuti' },
  rowVersion: '2026-11-15T10:00:00',
  body: {
    schemaVersion: 1,
    sections: [
      {
        id: 's_main',
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [
          // Added from the palette, and no file chosen yet: what the issue saved.
          { id: 'b_pic', type: 'image', version: 1, props: { width: 'full', rounded: true } },
          {
            id: 'b_head',
            type: 'heading',
            version: 1,
            props: { text: { en: 'Hangars', it: 'Hangar' }, level: 2 },
          },
        ],
        sections: [],
      },
    ],
  },
};

/** The row as the server holds it: a save is answered with what it wrote. */
async function stubTheRow(page: Page) {
  const writes: Written[] = [];
  let current: Record<string, unknown> = row;

  await page.route('**/api/content/7', async (route) => {
    if (route.request().method() === 'PUT') {
      const written = route.request().postDataJSON() as Written;
      writes.push(written);
      current = { ...current, ...written, rowVersion: `2026-11-15T10:00:0${writes.length}` };
    }

    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(current) });
  });

  return writes;
}

const pick = (page: Page, label: string) =>
  page.locator('li').filter({ hasText: label }).getByRole('button', { name: label, exact: true }).click();

test('the alternative text written before the file is kept, and saved with the file still to choose', async ({
  page,
}) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await stubTheApiAsStaff(page);
  await page.setViewportSize({ width: 1440, height: 900 });
  const writes = await stubTheRow(page);

  await page.goto('/staff/content/7');
  await page.getByRole('button', { name: editor.outline, exact: true }).click();
  await pick(page, blocks.image.label);

  // In the language its tab opens on, whichever that is.
  const alt = page.locator('input[id^="alt."]:visible').first();
  await alt.fill('An open hangar');

  await pick(page, blocks.heading.label);
  await pick(page, blocks.image.label);
  await expect(page.locator('input[id^="alt."]:visible').first()).toHaveValue('An open hangar');

  await page.getByRole('button', { name: editor.saveDraft }).click();
  await expect
    .poll(() => writes.at(-1)?.body.sections[0]?.blocks.find((block) => block.id === 'b_pic')?.props)
    .toHaveProperty('alt');
  const saved = writes.at(-1)?.body.sections[0]?.blocks.find((block) => block.id === 'b_pic')?.props;
  expect(Object.values(saved?.alt as Record<string, string>)).toContain('An open hangar');
  expect(saved).not.toHaveProperty('mediaId');
});
