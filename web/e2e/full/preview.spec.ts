import { expect } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, createContent, deleteContent, readInEnglish, signIn, test } from './bench';

/**
 * The preview of the editor draws the page on a machine slower than a developer's
 * (`decisions/2026-10-06-l-anteprima-vuota-dell-editor.md`).
 *
 * ⚠️ Found from a test of something else: on 6 October 2026 the dashboard's own spec stopped twice in
 * CI on a click in a preview that was empty. With the invitation to add a section inside the
 * container the sections measure, the typefaces finishing a moment after the first draw left every
 * section without a box, for good. It never showed on a fast machine — the typefaces were there
 * before the page was — which is why the processor is slowed here, three ways, since which slowness
 * opens the window depends on the machine: on a developer's, four times was 8 loads out of 8.
 *
 * It is a guard and not a proof: on a runner whose pace misses the window at all three it would pass
 * with the fault back. Where the invitation stands is kept by `picking.test.tsx` as well.
 */

const words = englishCommon.content.editor;
const stamp = Date.now().toString(36);

test('the preview of the editor draws the page however slowly the browser gets there', async ({
  browser,
  context,
  afterwards,
}, testInfo) => {
  test.setTimeout(120_000);
  await readInEnglish(context);
  await signIn(context);

  const born = await createContent(context, {
    slug: `bench-slow-preview-${stamp}-${testInfo.repeatEachIndex}`,
    title: { en: 'Bench slow preview', it: 'Anteprima lenta del banco' },
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
              id: 'b_title',
              type: 'heading',
              version: 1,
              props: { level: 2, text: { en: 'Drawn all the same', it: 'Disegnata lo stesso' } },
            },
          ],
          sections: [],
        },
      ],
    },
  });
  afterwards(() => deleteContent(context, born.id));

  for (const rate of [2, 4, 8]) {
    // A browser that has never been here, each time: from the second visit on the typefaces come
    // out of the cache before the page is drawn, and the window this is about never opens.
    const fresh = await browser.newContext({ baseURL: benchUrl });

    afterwards(async () => {
      await fresh.close();
    });

    await readInEnglish(fresh);
    await signIn(fresh);

    const page = await fresh.newPage();
    const slowed = await fresh.newCDPSession(page);
    await slowed.send('Emulation.setCPUThrottlingRate', { rate });
    await page.goto(`/staff/content/${born.id}`);

    // In the document is not drawn: the sections were all there, with no box.
    await expect(
      page.getByRole('region', { name: words.preview }).getByRole('heading', { name: 'Drawn all the same' }),
      `slowed ${rate} times`,
    ).toBeVisible({ timeout: 30_000 });
  }
});
