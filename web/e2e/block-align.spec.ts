import { expect, test, type Page } from '@playwright/test';

import { oneTemplate, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * Where a block stands in its column (PR 3.9, design of the editor's phase 3, §5.6), in a browser: the three icons of
 * the panel, the page under them moving at once, and what is saved — `align` on the envelope. And a group of buttons
 * written before, with its alignment in its props, still drawn where it was and moved onto the envelope by the panel.
 *
 * In a browser because what draws it is CSS — the alignment of the text, the justification of a row of buttons — and a
 * unit test reads class names, not where things end up.
 */

const editor = englishCommon.content.editor;
const words = editor.align;

const en = (value: string) => ({ en: value, it: value });

const aPage = {
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
  rowVersion: '2026-11-10T10:00:00',
  body: {
    schemaVersion: 1,
    sections: [
      {
        id: 'sec-intro',
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [
          { id: 'blk-heading', type: 'heading', version: 1, props: { level: 2, text: en('A heading') } },
          {
            id: 'blk-buttons',
            type: 'buttonGroup',
            version: 1,
            // Written before PR 3.9: the alignment in the props.
            props: { align: 'right', buttons: [{ label: en('Go'), href: '/go', variant: 'primary' }] },
          },
          {
            id: 'blk-table',
            type: 'table',
            version: 1,
            props: {
              columns: [{ label: en('Column'), align: 'left' }],
              rows: [{ cells: [{ text: en('A cell') }] }],
            },
          },
        ],
        sections: [],
      },
    ],
  },
};

interface Written {
  body: { sections: { blocks: { id: string; align?: string | null; props: Record<string, unknown> }[] }[] };
}

/** The row as the server holds it: a save is answered with what it wrote. */
async function stubTheRow(page: Page) {
  const writes: Written[] = [];
  let current: Record<string, unknown> = aPage;

  await page.route('**/api/content/7', async (route) => {
    if (route.request().method() === 'PUT') {
      const written = route.request().postDataJSON() as Written;
      writes.push(written);
      current = { ...current, ...written, rowVersion: `2026-11-10T10:00:0${writes.length}` };
    }

    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(current) });
  });

  return writes;
}

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.setViewportSize({ width: 1440, height: 900 });
});

const thePage = (page: Page) => page.getByRole('region', { name: editor.preview });

/** A block is picked by a click on it, on the page: a link inside it picks the block rather than leaving the editor. */
const pick = (thing: ReturnType<Page['getByRole']>) => thing.click();

const savedBlock = (writes: Written[], id: string) =>
  writes
    .at(-1)
    ?.body.sections.flatMap((section) => section.blocks)
    .find((block) => block.id === id);

test('a heading is put in the middle of its column, on the page at once and on the envelope when saved', async ({
  page,
}) => {
  const writes = await stubTheRow(page);
  await page.goto('/staff/content/7');

  const heading = thePage(page).getByRole('heading', { name: 'A heading' });
  await pick(heading);
  await expect(heading).toHaveCSS('text-align', 'start');
  await expect(page.getByRole('button', { name: words.options.left })).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await page.getByRole('button', { name: words.options.center }).click();

  await expect(heading).toHaveCSS('text-align', 'center');
  await expect(page.getByRole('button', { name: words.options.center })).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  // Saved by hand rather than after the pause of the autosave.
  await page.getByRole('button', { name: editor.saveDraft }).click();
  await expect.poll(() => savedBlock(writes, 'blk-heading')?.align).toBe('center');
});

test('a group of buttons written before is drawn where it was, and the panel moves its alignment', async ({
  page,
}) => {
  const writes = await stubTheRow(page);
  await page.goto('/staff/content/7');

  const row = thePage(page)
    .locator('[data-align-row]')
    .filter({ has: page.getByRole('link', { name: 'Go' }) });
  await expect(row).toHaveCSS('justify-content', 'flex-end');

  await pick(thePage(page).getByRole('link', { name: 'Go' }));
  await expect(page.getByRole('button', { name: words.options.right })).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await page.getByRole('button', { name: words.options.center }).click();
  await expect(row).toHaveCSS('justify-content', 'center');

  await page.getByRole('button', { name: editor.saveDraft }).click();
  await expect.poll(() => savedBlock(writes, 'blk-buttons')?.align).toBe('center');
  expect(savedBlock(writes, 'blk-buttons')?.props).not.toHaveProperty('align');

  await page.screenshot({ path: 'test-results/block-align.png', fullPage: true });
});

test('a table takes its whole column, and offers no alignment', async ({ page }) => {
  await stubTheRow(page);
  await page.goto('/staff/content/7');

  // The table picked: no choice. The heading picked after it: the choice is there — so the first answer was not a
  // panel that had not opened yet.
  await pick(thePage(page).getByRole('cell', { name: 'A cell' }));
  await expect(page.getByRole('button', { name: words.options.left })).toHaveCount(0);
  await pick(thePage(page).getByRole('heading', { name: 'A heading' }));
  await expect(page.getByRole('button', { name: words.options.left })).toHaveCount(1);
});
