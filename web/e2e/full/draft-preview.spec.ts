import { expect } from '@playwright/test';

import { englishCommon } from '../locales';

import { createContent, deleteContent, readContent, readInEnglish, signIn, test } from './bench';

/**
 * «Open the preview» against the real server (design of phase 3, §4.7): the save goes through the real `PUT`, the tab is a
 * real popup under the site's own `Cross-Origin-Opener-Policy`, and what it reads is the real back office read of the draft.
 * The stubbed suite (`e2e/draft-preview.spec.ts`) says the rest: the order, the links, «refresh», the refusals.
 */

const editor = englishCommon.content.editor;
const words = englishCommon.content.draftPreview;
const stamp = Date.now().toString(36);

test('the draft written in the editor is the one the new tab shows, in the frame of the site', async ({
  page,
  context,
  afterwards,
}) => {
  await readInEnglish(context);
  await signIn(context);

  const born = await createContent(context, {
    slug: `bench-draft-preview-${stamp}`,
    title: { en: 'Bench draft preview', it: 'Anteprima della bozza del banco' },
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 's_only',
          layout: 'stacked',
          background: 'none',
          padding: 'md',
          width: 'default',
          blocks: [
            {
              id: 'b_text',
              type: 'text',
              version: 1,
              props: {
                markdown: { en: 'Read [the team](/about/team).', it: 'Leggi [la squadra](/about/team).' },
              },
            },
          ],
          sections: [],
        },
      ],
    },
  });
  afterwards(() => deleteContent(context, born.id));

  await page.goto(`/staff/content/${born.id}`);
  await page.locator('input[name="title.en"]').fill('Bench draft preview, written by hand');
  await expect(page.getByText(editor.autosave.unsaved)).toBeVisible();

  const opening = page.waitForEvent('popup');
  await page.getByRole('button', { name: editor.openPreview.label }).click();
  const tab = await opening;

  await expect(tab).toHaveURL(new RegExp(`/staff/content/${born.id}/preview\\?lang=en$`));
  await expect(tab.getByText(words.notice)).toBeVisible();
  await expect(tab.getByText('Read')).toBeVisible();
  await expect(tab.getByRole('contentinfo')).toBeVisible();

  // Stored before the tab read it: the row says so, and so does the tab.
  expect((await readContent(context, born.id)).title.en).toBe('Bench draft preview, written by hand');
  await expect(tab).toHaveTitle(/^Preview: Bench draft preview, written by hand/);

  // A link of the page goes nowhere.
  const here = tab.url();
  await tab.getByRole('link', { name: 'the team' }).click();
  expect(tab.url()).toBe(here);
});
