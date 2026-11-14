import { expect, test, type Page } from '@playwright/test';

import { oneTemplate, stubTheApiAsStaff, stubThePublishedPage } from './fixtures';
import { englishCommon } from './locales';

/**
 * The table of PR 3.12 (design of the editor's phase 3, §5.1), in a browser: a paste from a spreadsheet through the real
 * clipboard, the grid in its wide dialog, one step to undo, and the three styles of the rows on the public site.
 *
 * In a browser because the clipboard is the browser's — jsdom has none, and its permission is asked of the page — and
 * because a style of the rows is a line or a band that only a browser draws.
 */

const editor = englishCommon.content.editor;
const words = englishCommon.blocks.table;
const both = (value: string) => ({ en: value, it: value });

const table = (id: string, props: Record<string, unknown>) => ({ id, type: 'table', version: 1, props });

const hours = {
  caption: both('Opening hours'),
  columns: [
    { label: both('Day'), align: 'left' },
    { label: both('From'), align: 'right' },
  ],
  rows: ['Monday', 'Tuesday', 'Wednesday'].map((day) => ({
    cells: [{ text: both(day) }, { text: both('18:00z') }],
  })),
};

const section = (blocks: unknown[]) => ({
  id: 's_tables',
  layout: 'stacked',
  background: 'none',
  padding: 'md',
  width: 'default',
  blocks,
  sections: [],
});

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the rows are told apart by a line, a band or nothing, as the table says', async ({ page }) => {
  await stubThePublishedPage(page, 'tables', {
    schemaVersion: 1,
    sections: [
      section([
        // Written before PR 3.12: no style, the line it always had.
        table('b_old', { ...hours, caption: both('Lines') }),
        table('b_striped', { ...hours, caption: both('Bands'), rowStyle: 'striped' }),
        table('b_plain', { ...hours, caption: both('Plain'), rowStyle: 'plain' }),
      ]),
    ],
  });
  await page.goto('/tables');

  const rowsOf = async (caption: string) => {
    const found = page.getByRole('table', { name: caption });
    await expect(found).toBeVisible();
    return found.locator('tbody tr').evaluateAll((rows) =>
      rows.map((row) => {
        const style = getComputedStyle(row);
        return { line: style.borderBottomWidth, band: style.backgroundColor };
      }),
    );
  };
  const none = 'rgba(0, 0, 0, 0)';

  // Atmosphere takes the line off the last row of every table: under it is the end of the table.
  const lines = await rowsOf('Lines');
  expect(lines.map((row) => row.line)).toEqual(['1px', '1px', '0px']);
  expect(lines.every((row) => row.band === none)).toBe(true);

  const bands = await rowsOf('Bands');
  expect(bands.every((row) => row.line === '0px')).toBe(true);
  expect(bands[0]?.band).toBe(none);
  expect(bands[1]?.band).not.toBe(none);
  expect(bands[2]?.band).toBe(none);

  const plain = await rowsOf('Plain');
  expect(plain.every((row) => row.line === '0px' && row.band === none)).toBe(true);
});

test.describe('in the editor', () => {
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
    rowVersion: '2026-11-14T10:00:00',
    body: { schemaVersion: 1, sections: [section([table('b_table', hours)])] },
  };

  interface Written {
    body: { sections: { blocks: { id: string; props: Record<string, unknown> }[] }[] };
  }

  /** The row as the server holds it: a save is answered with what it wrote. */
  async function stubTheRow(page: Page) {
    const writes: Written[] = [];
    let current: Record<string, unknown> = aPage;

    await page.route('**/api/content/7', async (route) => {
      if (route.request().method() === 'PUT') {
        const written = route.request().postDataJSON() as Written;
        writes.push(written);
        current = { ...current, ...written, rowVersion: `2026-11-14T10:00:0${writes.length}` };
      }

      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(current) });
    });

    return writes;
  }

  const thePage = (page: Page) => page.getByRole('region', { name: editor.preview });

  test.beforeEach(async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await stubTheApiAsStaff(page);
    await page.setViewportSize({ width: 1440, height: 900 });
  });

  test('a copy from a spreadsheet is pasted as the table, the grid writes a cell, and undo takes each back', async ({
    page,
  }) => {
    const writes = await stubTheRow(page);
    await page.goto('/staff/content/7');

    // Picked by a click on the page; again if the first came before the editor was listening.
    await expect(async () => {
      await thePage(page).getByRole('cell', { name: 'Monday' }).click();
      await expect(page.getByRole('button', { name: words.grid.open })).toBeVisible({ timeout: 1000 });
    }).toPass();

    // What Excel puts on the clipboard: tabs, CRLF, a quoted cell with a line break in it.
    await page.evaluate(() =>
      navigator.clipboard.writeText(
        'Station\tFrequency\r\nLIMM_CTR\t125.500\r\n"Milano\nRadar"\t126.750\r\n',
      ),
    );
    await page.getByRole('button', { name: words.tools.paste }).click();

    const preview = thePage(page).getByRole('table', { name: 'Opening hours' });
    await expect(preview.getByRole('columnheader', { name: 'Station' })).toBeVisible();
    await expect(preview.getByRole('cell', { name: 'LIMM_CTR' })).toBeVisible();
    // A line break inside a copied cell is a space, as the page would draw it anyway.
    await expect(preview.getByRole('cell', { name: 'Milano Radar', exact: true })).toBeVisible();
    await expect(preview.getByRole('cell', { name: 'Monday' })).toHaveCount(0);
    await expect(page.getByRole('status').filter({ hasText: '2 columns and 2 rows' })).toBeVisible();

    // The grid: wider than the panel it opens from, a box per cell.
    await page.getByRole('button', { name: words.grid.open }).click();
    const dialog = page.getByRole('dialog', { name: words.grid.title });
    expect((await dialog.boundingBox())!.width).toBeGreaterThan(700);
    const cell = dialog.getByLabel('Row 1, column 2');
    await expect(cell).toHaveValue('125.500');
    await cell.fill('125.505');
    await dialog.getByRole('button', { name: words.grid.apply }).click();
    await expect(dialog).toHaveCount(0);
    await expect(preview.getByRole('cell', { name: '125.505' })).toBeVisible();

    // The grid was one step; the paste before it another.
    await page.getByRole('button', { name: editor.undo }).click();
    await expect(preview.getByRole('cell', { name: '125.500' })).toBeVisible();
    await page.getByRole('button', { name: editor.redo }).click();
    await expect(preview.getByRole('cell', { name: '125.505' })).toBeVisible();

    await page.getByRole('button', { name: editor.saveDraft }).click();
    await expect
      .poll(
        () =>
          writes
            .at(-1)
            ?.body.sections.flatMap((one) => one.blocks)
            .find((block) => block.id === 'b_table')?.props.rows,
      )
      .toEqual([
        { cells: [{ text: { en: 'LIMM_CTR', it: '' } }, { text: { en: '125.505', it: '' } }] },
        { cells: [{ text: { en: 'Milano Radar', it: '' } }, { text: { en: '126.750', it: '' } }] },
      ]);
  });
});
