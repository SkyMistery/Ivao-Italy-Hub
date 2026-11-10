import { readFile } from 'node:fs/promises';

import { expect, test } from '@playwright/test';

import { createContent, readContent, readInEnglish, signIn } from './bench';

/**
 * «Import from Claude» against the real API (PR 3.0b; note 2026-10-30-una-pagina-scritta-con-claude §3, §4): the road
 * the suite with the stubs cannot walk. The version in the header comes from one answer of the server, the download,
 * and is compared after a reload with another, the read of the row; the store takes the file with its own checks; and
 * what a block of the draft carries is read back from the database.
 */

const slug = `claude-import-${Date.now()}`;

test('a file written from the downloaded instructions goes in the draft, over a reload, and nothing is published', async ({
  page,
  context,
}) => {
  await signIn(context);
  await readInEnglish(context);

  const row = await createContent(context, {
    slug,
    visibility: 'Public',
    title: { en: 'Before', it: 'Prima' },
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 'sec-news',
          layout: 'stacked',
          background: 'muted',
          padding: 'md',
          width: 'default',
          blocks: [
            {
              id: 'blk-news',
              type: 'newsList',
              version: 1,
              props: { category: 'updates', limit: 3 },
              renderMode: 'frozen',
              frozen: { items: [] },
              span: 6,
            },
          ],
          sections: [],
        },
      ],
    },
  });

  await page.goto(`/staff/content/${row.id}`);

  const [download] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: 'Download for Claude' }).click(),
  ]);
  const markdown = await readFile(await download.path(), 'utf8');
  const fence = /```json\n([\s\S]*?)\n```/.exec(
    markdown.slice(markdown.lastIndexOf('## The header to copy')),
  );
  const header = JSON.parse(fence?.[1] ?? '{}') as Record<string, unknown>;

  // The instructions lie on a desk for a while: the editor is opened again from what the server holds.
  await page.reload();

  await page.getByLabel('Import from Claude').setInputFiles({
    name: `${slug}.json`,
    mimeType: 'application/json',
    buffer: Buffer.from(
      JSON.stringify({
        ...header,
        title: { en: 'After', it: 'Dopo' },
        summary: { en: 'Written by Claude', it: 'Scritta da Claude' },
        body: {
          schemaVersion: 1,
          sections: [
            {
              layout: 'stacked',
              blocks: [
                {
                  type: 'text',
                  version: 1,
                  props: { markdown: { en: 'From the file', it: 'Dal file' } },
                },
                {
                  type: 'image',
                  version: 1,
                  props: { mediaId: 987654, alt: { en: 'A tower', it: 'Una torre' } },
                },
              ],
            },
            {
              id: 'sec-news',
              layout: 'stacked',
              background: 'muted',
              blocks: [
                // What an assistant might well do to a block of the draft: only its question was its to change.
                { id: 'blk-news', type: 'newsList', version: 1, props: { category: 'updates', limit: 5 } },
              ],
            },
          ],
        },
      }),
    ),
  });

  await expect(page.getByText('From the file')).toBeVisible();
  await expect(page.getByText('One picture to choose')).toBeVisible();
  await expect(page.locator('input[name="title.en"]')).toHaveValue('After');

  const stored = await readContent(context, row.id);
  expect(stored.title).toEqual({ en: 'After', it: 'Dopo' });
  const said = stored as unknown as { publishedAt: string | null; draftFromFile: boolean };
  expect(said.publishedAt).toBeNull();
  expect(said.draftFromFile).toBe(true);

  const [written, news] = (stored.body as { sections: { blocks: Record<string, unknown>[] }[] }).sections;
  expect(written?.blocks[0]?.['id']).toMatch(/^b_/);
  // A number the draft did not have is not a picture.
  expect(written?.blocks[1]?.['props']).not.toHaveProperty('mediaId');

  expect(news?.blocks[0]).toMatchObject({
    id: 'blk-news',
    renderMode: 'frozen',
    span: 6,
    frozen: { items: [] },
    props: { limit: 5 },
  });
});
