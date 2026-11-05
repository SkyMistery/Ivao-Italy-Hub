import { expect } from '@playwright/test';

import { englishCommon } from '../locales';

import {
  choose,
  createContent,
  deleteContent,
  deleteMedia,
  openOutline,
  properties,
  readInEnglish,
  selectBlock,
  signIn,
  test,
  uploadMedia,
} from './bench';

/**
 * Looking for a file in the editor's picker, against the real library (design of the editor's phase 3, §4.4): the search
 * and the next page are the server's, and a file uploaded from the top of the picker, by the button or by dragging it on
 * the grid, comes back from the server chosen, with nobody having written its alternative text. The stubs of the other
 * suite could say all this and prove none of it.
 */

const picker = englishCommon.media.picker;
const editor = englishCommon.content.editor;
const blocks = englishCommon.blocks;

/** A name of this run, the one the search looks for. */
const stamp = Date.now().toString(36);
const named = (mark: string) => `picker-${stamp}-${mark}.png`;

/** An eight by eight PNG, and a mark of this run after its end: the library answers the same bytes with the same file. */
const picture = (mark: string) =>
  Buffer.concat([
    Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAFElEQVR42mP8z8BQz0AEYBxVSF+FANqkA/8ZBEwuAAAAAElFTkSuQmCC',
      'base64',
    ),
    Buffer.from(`${stamp}-${mark}`),
  ]);

test('a file is searched, paged, uploaded from the top and dragged on the grid', async ({
  page,
  context,
  afterwards,
}) => {
  await signIn(context);
  await readInEnglish(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // Every file of the run, whichever way it went in: the search finds them all when the test is over.
  afterwards(async () => {
    const found = (await (await context.request.get(`/api/media?q=picker-${stamp}&pageSize=100`)).json()) as {
      items: { id: number }[];
    };
    for (const { id } of found.items) {
      await deleteMedia(context, id);
    }
  });

  // One more than a page of the picker, and the oldest of them is what the block shows.
  const first = await uploadMedia(context, 'WD', named('file-00'), picture('00'));
  for (let index = 1; index <= 24; index += 1) {
    const mark = String(index).padStart(2, '0');
    await uploadMedia(context, 'WD', named(`file-${mark}`), picture(mark));
  }

  const row = await createContent(context, {
    slug: `picker-${stamp}`,
    title: { en: 'The picker', it: 'Il selettore' },
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 'sec-picture',
          layout: 'stacked',
          background: 'muted',
          padding: 'md',
          width: 'default',
          blocks: [
            {
              id: 'blk-picture',
              type: 'image',
              version: 1,
              props: { mediaId: first, width: 'full', rounded: true },
              span: 6,
            },
          ],
          sections: [],
        },
      ],
    },
  });
  afterwards(() => deleteContent(context, row.id));

  await page.goto(`/staff/content/${row.id}`);
  await openOutline(page, editor.outline);
  await selectBlock(page, blocks.image.label);

  const panel = properties(page);
  const grid = panel.getByRole('list', { name: picker.label });
  const tile = (name: string) => grid.getByRole('button', { name: new RegExp(name.replace(/\./g, '\\.')) });
  const ofThisRun = grid.getByRole('button', { name: new RegExp(`picker-${stamp}`) });

  // ---------------------------------------------------------------- the search and the next page
  const search = panel.getByRole('searchbox', { name: picker.search });
  // The answer waited for, and not the count: the files of this run are also the newest of the whole library, so the
  // first page holds 24 of them before the box has stopped waiting for the typing.
  const answered = page.waitForResponse(
    (response) => response.url().includes('/api/media?') && response.url().includes(`q=picker-${stamp}`),
  );
  await search.fill(`picker-${stamp}`);
  await answered;

  await expect(ofThisRun).toHaveCount(24);
  await panel.getByRole('button', { name: picker.more, exact: true }).click();
  await expect(ofThisRun).toHaveCount(25);
  await expect(panel.getByRole('button', { name: picker.more, exact: true })).toBeHidden();

  await search.fill(named('file-07'));
  await expect(ofThisRun).toHaveCount(1);
  await expect(tile(named('file-07'))).toBeVisible();

  // ---------------------------------------------------------------- a category with none of them
  await choose(
    page,
    englishCommon.media.fields.category,
    englishCommon.media.options.category.Document,
    panel,
  );
  await expect(panel.getByText(picker.noMatch)).toBeVisible();
  // Under a search that finds nothing the way back stays: the box, and the upload.
  await expect(search).toBeVisible();
  await expect(panel.getByRole('button', { name: picker.upload, exact: true })).toBeVisible();

  // ---------------------------------------------------------------- the upload at the top
  await panel.getByLabel(picker.upload).setInputFiles({
    name: named('uploaded'),
    mimeType: 'image/png',
    buffer: picture('uploaded'),
  });

  // Chosen, and in sight of the search that was hiding it: the picker went back to the newest files.
  await expect(tile(named('uploaded'))).toHaveAttribute('aria-pressed', 'true');
  await expect(search).toHaveValue('');

  // Nobody has written its alternative text, and the way to write it is its page in the library.
  const uploaded = grid.getByRole('listitem').filter({ hasText: named('uploaded') });
  const write = uploaded.getByRole('link', { name: picker.altToWrite });
  await expect(write).toHaveAttribute('href', /\/staff\/media\/\d+$/);
  await expect(write).toHaveAttribute('target', '_blank');

  // ---------------------------------------------------------------- the file dragged on the grid
  // What a browser hands a drop, built in the page: Playwright's own drag moves elements, not files from the desktop.
  const dragged = await page.evaluateHandle((name) => {
    const transfer = new DataTransfer();
    transfer.items.add(new File([`${name}`], name, { type: 'image/png' }));
    return transfer;
  }, 'not-a-picture.png');
  await grid.dispatchEvent('dragover', { dataTransfer: dragged });
  await grid.dispatchEvent('drop', { dataTransfer: dragged });

  // Those bytes are not a picture: the library refuses them, and the picker says so where the upload is.
  await expect(panel.getByRole('alert').filter({ hasText: picker.uploadRefused })).toBeVisible();

  const real = await page.evaluateHandle(
    ({ name, base64 }) => {
      const bytes = Uint8Array.from(atob(base64), (character) => character.charCodeAt(0));
      const transfer = new DataTransfer();
      transfer.items.add(new File([bytes], name, { type: 'image/png' }));
      return transfer;
    },
    { name: named('dragged'), base64: picture('dragged').toString('base64') },
  );
  // From the bottom of the grid, which scrolls by itself: the new file is its first row, and it has to be in sight.
  await grid.evaluate((list) => {
    list.scrollTop = list.scrollHeight;
  });
  await grid.dispatchEvent('dragover', { dataTransfer: real });
  await grid.dispatchEvent('drop', { dataTransfer: real });

  await expect(tile(named('dragged'))).toHaveAttribute('aria-pressed', 'true');
  expect(await grid.evaluate((list) => list.scrollTop)).toBe(0);
  await expect(panel.getByRole('alert').filter({ hasText: picker.uploadRefused })).toBeHidden();
});
