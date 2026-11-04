import { readFileSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { oneTemplate, staffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * «Import from Claude» (PR 3.0b; note 2026-10-30-una-pagina-scritta-con-claude §3, §4). The checks of the file are
 * unit tests (`claudeFile.test.ts`); what is proved here is the road: the header is the one a press on «Download for
 * Claude» really gave, the file goes through the browser's own file input, what passes is saved over the version it
 * worked on and replaces the draft on screen, title included, and Undo takes all of it back.
 */

/** The server's own words: a refusal names a key of this file. */
const serverWords = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/errors.json', import.meta.url)), 'utf8'),
) as { errors: { body: { blockTypeUnknown: string } } };

const words = englishCommon.content.import;
const download = englishCommon.content.claude.download;

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
  rowVersion: '2026-11-04T10:00:00',
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
          { id: 'blk-hello', type: 'text', version: 1, props: { markdown: { en: 'Hello', it: 'Ciao' } } },
        ],
        sections: [],
      },
    ],
  },
};

interface Written {
  rowVersion: string;
  draftFromFile?: boolean;
  title: Record<string, string>;
  body: { sections: { id: string; blocks: { id: string; props: Record<string, unknown> }[] }[] };
}

/** The row as the server holds it, so that a save is answered with what it wrote and a version of its own. */
async function stubTheRow(page: Page, row: Record<string, unknown> = aPage) {
  const writes: Written[] = [];
  let current = row;

  await page.route('**/api/content/7', async (route) => {
    if (route.request().method() === 'PUT') {
      const written = route.request().postDataJSON() as Written;
      writes.push(written);
      current = { ...current, ...written, rowVersion: `2026-11-04T10:00:0${writes.length}` };
    }

    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(current) });
  });

  return writes;
}

/** What the download reads besides the row. After the staff's own routes, so that these win. */
async function stubTheSite(page: Page) {
  await page.route('**/api/content/addresses', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: 20,
          kind: 'Page',
          url: '/about/team',
          title: { en: 'Team', it: 'Squadra' },
          ownerDepartment: 'WD',
          visibility: 'Public',
        },
      ]),
    }),
  );

  await page.route('**/api/content/settings', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ stylePageId: null, stylePagePath: null }),
    }),
  );
}

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page);
  await stubTheSite(page);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.setViewportSize({ width: 1440, height: 900 });
});

/** The header a press on the download gives, read out of the file that arrives: the last fence of the instructions. */
async function downloadTheHeader(page: Page): Promise<Record<string, unknown>> {
  const [file] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: download.label }).click(),
  ]);

  const markdown = await readFile(await file.path(), 'utf8');
  const tail = markdown.slice(markdown.lastIndexOf('## The header to copy'));
  const fence = /```json\n([\s\S]*?)\n```/.exec(tail);
  if (fence?.[1] === undefined) {
    throw new Error('No header in the instructions.');
  }

  return JSON.parse(fence[1]) as Record<string, unknown>;
}

const importFile = (page: Page, contents: unknown) =>
  page.getByLabel(words.label).setInputFiles({
    name: 'welcome.json',
    mimeType: 'application/json',
    buffer: Buffer.from(typeof contents === 'string' ? contents : JSON.stringify(contents)),
  });

/** What Claude would return: the header, the block of the draft rewritten, and a new one with a picture to choose. */
const answer = (header: Record<string, unknown>) => ({
  ...header,
  title: { en: 'Written by Claude', it: 'Scritta da Claude' },
  summary: { en: 'A summary', it: 'Un riassunto' },
  body: {
    schemaVersion: 1,
    sections: [
      {
        id: 'sec-intro',
        layout: 'stacked',
        blocks: [
          {
            id: 'blk-hello',
            type: 'text',
            version: 1,
            props: { markdown: { en: 'Hello from the file', it: 'Ciao dal file' } },
          },
          {
            type: 'image',
            version: 1,
            props: { alt: { en: 'The tower', it: 'La torre' } },
          },
        ],
      },
    ],
  },
});

test('a file written from the instructions replaces the draft, title included, and Undo takes it back', async ({
  page,
}) => {
  const writes = await stubTheRow(page);
  await page.goto('/staff/content/7');

  const header = await downloadTheHeader(page);
  await importFile(page, answer(header));

  // Saved over the version it worked on, with the block of the draft under its own identifier.
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0]?.rowVersion).toBe('2026-11-04T10:00:00');
  expect(writes[0]?.draftFromFile).toBe(true);
  expect(writes[0]?.title).toEqual({ en: 'Written by Claude', it: 'Scritta da Claude' });
  const blocks = writes[0]?.body.sections[0]?.blocks ?? [];
  expect(blocks[0]?.id).toBe('blk-hello');
  expect(blocks[1]?.id).toMatch(/^b_/);

  // On screen: the page, the row's own fields, and what is left to do.
  await expect(page.getByText('Hello from the file')).toBeVisible();
  await expect(page.locator('input[name="title.en"]')).toHaveValue('Written by Claude');
  const left = page.getByText(words.pending.pictureCount_one);
  await expect(left).toBeVisible();

  // Nothing published, and the way back: the body, the fields and the list.
  await page.getByRole('button', { name: englishCommon.content.editor.undo }).click();
  await expect(page.getByText('Hello from the file')).toHaveCount(0);
  await expect(page.getByText('Hello', { exact: true })).toBeVisible();
  await expect(page.locator('input[name="title.en"]')).toHaveValue('Welcome');
  await expect(left).toHaveCount(0);

  // And it is the old draft that is stored, over the version the import left: the server hears the way back too.
  await page.getByRole('button', { name: englishCommon.content.editor.saveDraft }).click();
  await expect.poll(() => writes.length).toBe(2);
  expect(writes[1]?.rowVersion).toBe('2026-11-04T10:00:01');
  expect(writes[1]?.draftFromFile).toBe(false);
  expect(writes[1]?.title).toEqual({ en: 'Welcome', it: 'Benvenuti' });
  expect(writes[1]?.body.sections[0]?.blocks.map((block) => block.id)).toEqual(['blk-hello']);

  // And forth.
  await page.getByRole('button', { name: englishCommon.content.editor.redo }).click();
  await expect(page.getByText('Hello from the file')).toBeVisible();
  await expect(page.locator('input[name="title.en"]')).toHaveValue('Written by Claude');
  await expect(left).toBeVisible();
});

test('the line of the version starts from the import', async ({ page }) => {
  // Whoever may approve publishes straight away: the dialog of the line is the toolbar's.
  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: [
      ...staffBootstrap.permissions,
      ...staffBootstrap.permissions
        .filter((permission) => permission.name === 'Content.Edit')
        .map((permission) => ({ ...permission, name: 'Content.Approve' })),
    ],
  });
  await stubTheSite(page);
  await stubTheRow(page);
  await page.route('**/api/content/7/review', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        firstPublication: true,
        sections: [],
        blocks: { added: 0, changed: 0, removed: 0 },
        fields: [],
      }),
    }),
  );
  await page.goto('/staff/content/7');

  await importFile(page, answer(await downloadTheHeader(page)));
  await expect(page.getByText('Hello from the file')).toBeVisible();

  await page.getByRole('button', { name: englishCommon.content.editor.publish, exact: true }).click();
  await expect(page.getByLabel(englishCommon.content.editor.publishDialog.changelog)).toHaveValue(
    `${words.versionLine} ${englishCommon.content.changes.first}`,
  );
});

test.describe('a file that is refused leaves the draft where it is', () => {
  test('one that does not come from the instructions', async ({ page }) => {
    const writes = await stubTheRow(page);
    await page.goto('/staff/content/7');

    await importFile(page, 'Here is your page!');

    await expect(page.getByRole('dialog')).toContainText(words.notFromInstructions);
    expect(writes).toEqual([]);
  });

  test('one for a draft that has changed since, without a save of its own', async ({ page }) => {
    const writes = await stubTheRow(page);
    await page.goto('/staff/content/7');
    const header = await downloadTheHeader(page);

    await page.locator('input[name="title.en"]').fill('Written by hand');
    await expect(page.getByText(englishCommon.content.editor.autosave.unsaved)).toBeVisible();
    await importFile(page, answer(header));

    await expect(page.getByRole('dialog')).toContainText(words.draftChanged);
    expect(writes).toEqual([]);
  });

  test('one with a block its schema refuses, named as the editor names it', async ({ page }) => {
    const writes = await stubTheRow(page);
    await page.goto('/staff/content/7');
    const header = await downloadTheHeader(page);

    await importFile(page, {
      ...answer(header),
      body: {
        schemaVersion: 1,
        sections: [
          {
            id: 'sec-intro',
            layout: 'stacked',
            blocks: [{ id: 'blk-hello', type: 'text', version: 1, props: { markdown: 12 } }],
          },
        ],
      },
    });

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText(words.refused);
    await expect(dialog).toContainText(`${englishCommon.blocks.text.label} › markdown`);
    expect(writes).toEqual([]);
  });

  test('one the server refuses, with the server’s own words', async ({ page }) => {
    await stubTheRow(page);
    await page.goto('/staff/content/7');
    const header = await downloadTheHeader(page);

    await page.route('**/api/content/7', async (route) => {
      if (route.request().method() !== 'PUT') {
        await route.fallback();
        return;
      }
      await route.fulfill({
        status: 400,
        contentType: 'application/problem+json',
        body: JSON.stringify({
          status: 400,
          errors: { 'body.sections[0].blocks[0]': ['errors.body.blockTypeUnknown'] },
        }),
      });
    });

    await importFile(page, answer(header));

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText(words.refused);
    await expect(dialog).toContainText(serverWords.errors.body.blockTypeUnknown);
    // What was on screen stays.
    await expect(page.getByText('Hello from the file')).toHaveCount(0);
    await expect(page.locator('input[name="title.en"]')).toHaveValue('Welcome');
  });
});

test('a page started over is asked about, and goes ahead only on a yes', async ({ page }) => {
  const writes = await stubTheRow(page);
  await page.goto('/staff/content/7');
  const header = await downloadTheHeader(page);

  await importFile(page, {
    ...answer(header),
    body: {
      schemaVersion: 1,
      sections: [
        {
          layout: 'stacked',
          blocks: [{ type: 'text', version: 1, props: { markdown: { en: 'All new', it: 'Tutto nuovo' } } }],
        },
      ],
    },
  });

  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(words.startedOver);
  expect(writes).toEqual([]);

  await dialog.getByRole('button', { name: words.startedOverConfirm }).click();
  await expect(page.getByText('All new')).toBeVisible();
  expect(writes).toHaveLength(1);
});

test('a template has no import, and neither has whoever may not edit the page', async ({ page }) => {
  await stubTheRow(page);

  // Row 5 of the fixture is a template.
  await page.goto('/staff/content/5');
  await expect(page.getByRole('button', { name: englishCommon.content.editor.saveDraft })).toBeVisible();
  await expect(page.getByRole('button', { name: words.label })).toHaveCount(0);

  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: staffBootstrap.permissions.filter((permission) => permission.name !== 'Content.Edit'),
  });
  await stubTheRow(page);
  await page.goto('/staff/content/7');
  await expect(page.getByRole('heading', { name: englishCommon.content.editor.properties })).toBeVisible();
  await expect(page.getByRole('button', { name: words.label })).toHaveCount(0);
});

test('a save somebody else got in before stops the editor, and says to reload', async ({ page }) => {
  await stubTheRow(page);
  await page.goto('/staff/content/7');
  const header = await downloadTheHeader(page);

  await page.route('**/api/content/7', async (route) => {
    if (route.request().method() !== 'PUT') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 409,
      contentType: 'application/problem+json',
      body: JSON.stringify({ status: 409 }),
    });
  });

  await importFile(page, answer(header));

  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(words.conflict);
  await dialog.getByRole('button', { name: englishCommon.common.close }).click();

  // As when a save of the editor's own hears it: nothing more is written, and no new file can walk into the same wall.
  await expect(page.getByText(englishCommon.content.editor.autosave.stopped)).toBeVisible();
  await expect(page.getByRole('button', { name: words.label })).toBeDisabled();
  await expect(page.getByRole('button', { name: download.label })).toBeDisabled();
  await expect(page.getByText('Hello from the file')).toHaveCount(0);
});

test('a refusal with nothing for Claude to mend does not ask for Claude', async ({ page }) => {
  await stubTheRow(page);
  await page.goto('/staff/content/7');
  const header = await downloadTheHeader(page);

  await page.route('**/api/content/7', async (route) => {
    if (route.request().method() !== 'PUT') {
      await route.fallback();
      return;
    }
    await route.fulfill({ status: 403, body: '' });
  });

  await importFile(page, answer(header));

  await expect(page.getByText(words.failed, { exact: true })).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByText('Hello from the file')).toHaveCount(0);
});

test('a save after the import says nothing about where the draft came from', async ({ page }) => {
  const writes = await stubTheRow(page);
  await page.goto('/staff/content/7');

  await importFile(page, answer(await downloadTheHeader(page)));
  await expect.poll(() => writes.length).toBe(1);

  await page.locator('input[name="title.en"]').fill('Corrected by hand');
  await expect(page.getByText(englishCommon.content.editor.autosave.unsaved)).toBeVisible();
  await page.getByRole('button', { name: englishCommon.content.editor.saveDraft }).click();

  await expect.poll(() => writes.length).toBe(2);
  expect(writes[1]).not.toHaveProperty('draftFromFile');
});

test('the approver, who imported nothing, reads the line from the row', async ({ page }) => {
  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: [
      ...staffBootstrap.permissions,
      ...staffBootstrap.permissions
        .filter((permission) => permission.name === 'Content.Edit')
        .map((permission) => ({ ...permission, name: 'Content.Approve' })),
    ],
  });
  await stubTheSite(page);
  await stubTheRow(page, { ...aPage, status: 'Ready', readyBy: 704798, draftFromFile: true });
  await page.route('**/api/content/7/review', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        firstPublication: true,
        sections: [],
        blocks: { added: 0, changed: 0, removed: 0 },
        fields: [],
        menu: null,
        note: null,
      }),
    }),
  );

  // Another browser: nothing here remembers any import.
  await page.goto('/staff/content/7');

  await page.getByRole('button', { name: englishCommon.content.review.approve, exact: true }).click();
  await expect(page.getByLabel(englishCommon.content.editor.publishDialog.changelog)).toHaveValue(
    `${words.versionLine} ${englishCommon.content.changes.first}`,
  );
});

test('two imports are undone one at a time, each with its own title', async ({ page }) => {
  await stubTheRow(page);
  await page.goto('/staff/content/7');
  const title = page.locator('input[name="title.en"]');
  const undo = page.getByRole('button', { name: englishCommon.content.editor.undo });

  await importFile(page, answer(await downloadTheHeader(page)));
  await expect(title).toHaveValue('Written by Claude');

  await importFile(page, {
    ...answer(await downloadTheHeader(page)),
    title: { en: 'Written again', it: 'Scritta di nuovo' },
  });
  await expect(title).toHaveValue('Written again');

  await undo.click();
  await expect(title).toHaveValue('Written by Claude');

  await undo.click();
  await expect(title).toHaveValue('Welcome');
  await expect(page.getByText(words.pending.pictureCount_one)).toHaveCount(0);
});

test('what is left to do is still said after a reload', async ({ page }) => {
  await stubTheRow(page);
  await page.goto('/staff/content/7');

  await importFile(page, answer(await downloadTheHeader(page)));
  await expect(page.getByText(words.pending.pictureCount_one)).toBeVisible();

  await page.reload();
  await expect(page.getByText('Hello from the file')).toBeVisible();
  await expect(page.getByText(words.pending.pictureCount_one)).toBeVisible();
});
