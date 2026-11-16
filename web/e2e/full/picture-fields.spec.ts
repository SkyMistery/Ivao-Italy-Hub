import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect } from '@playwright/test';

import {
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
 * A document where a picture is drawn (#488, note 2026-11-15-un-documento-al-posto-di-una-foto): the editor says so in
 * «What is missing before this can be published», on the field that holds it, and the picture beside it is not named.
 */

const english = (name: string): unknown =>
  JSON.parse(
    readFileSync(fileURLToPath(new URL(`../../../locales/en/${name}.json`, import.meta.url)), 'utf8'),
  );
const common = english('common') as {
  content: { editor: { publishMissing: string; outline: string } };
  media: { picker: { label: string; search: string } };
  blocks: { image: { label: string } };
};
const editor = common.content.editor;
const picker = common.media.picker;
const errors = (english('errors') as { errors: { content: { mediaNotPicture: string } } }).errors;

const stamp = Date.now().toString(36);

/** An eight by eight PNG, and a mark of this run after its end: the library answers the same bytes with the same file. */
const picture = Buffer.concat([
  Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAFElEQVR42mP8z8BQz0AEYBxVSF+FANqkA/8ZBEwuAAAAAElFTkSuQmCC',
    'base64',
  ),
  Buffer.from(`${stamp}-picture`),
]);

/** The smallest PDF the library recognises by its first bytes, marked with this run. */
const document = Buffer.from(`%PDF-1.4\n% ${stamp}\n%%EOF\n`);

test('a document chosen for a picture is named before publishing, and the picture beside it is not', async ({
  page,
  context,
  afterwards,
}) => {
  await signIn(context);
  await readInEnglish(context);

  const pictureId = await uploadMedia(context, 'WD', `fields-${stamp}.png`, picture);
  const documentId = await uploadMedia(context, 'WD', `fields-${stamp}.pdf`, document);
  afterwards(async () => {
    await deleteMedia(context, pictureId);
    await deleteMedia(context, documentId);
  });

  const row = await createContent(context, {
    slug: `fields-${stamp}`,
    title: { en: 'Picture fields', it: 'Campi foto' },
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 'sec-pictures',
          layout: 'stacked',
          background: 'muted',
          padding: 'md',
          width: 'default',
          blocks: [
            {
              id: 'blk-image',
              type: 'image',
              version: 1,
              props: { mediaId: documentId, width: 'full', rounded: true },
            },
            {
              id: 'blk-gallery',
              type: 'gallery',
              version: 1,
              props: { images: [{ mediaId: pictureId }, { mediaId: documentId }] },
            },
          ],
          sections: [],
        },
      ],
    },
  });
  afterwards(() => deleteContent(context, row.id));

  await page.goto(`/staff/content/${row.id}`);

  await expect(page.getByText(editor.publishMissing)).toBeVisible();
  const notPicture = page.getByRole('listitem').filter({ hasText: errors.content.mediaNotPicture });
  // The image, and the second picture of the gallery; the first picture of the gallery is a picture.
  await expect(notPicture).toHaveCount(2);

  // And the picker of a picture offers the picture, not the document (#488, part A): searched by the name of this run,
  // which the two files share.
  await openOutline(page, editor.outline);
  await selectBlock(page, common.blocks.image.label);
  const panel = properties(page);
  const grid = panel.getByRole('list', { name: picker.label });
  const answered = page.waitForResponse(
    (response) => response.url().includes('/api/media?') && response.url().includes(`q=fields-${stamp}`),
  );
  await panel.getByRole('searchbox', { name: picker.search }).fill(`fields-${stamp}`);
  await answered;
  await expect(grid.getByRole('button', { name: new RegExp(`fields-${stamp}[.]png`) })).toBeVisible();
  await expect(grid.getByRole('button', { name: new RegExp(`fields-${stamp}[.]pdf`) })).toHaveCount(0);
});
