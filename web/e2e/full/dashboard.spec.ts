import { expect, test } from '@playwright/test';

import { englishCommon } from '../locales';

import { createContent, deleteContent, readContent, readInEnglish, signIn } from './bench';

/**
 * A dashboard is composed as the grid of tiles it is read as (D2, note
 * 2026-09-13-le-dashboard-a-tutto-schermo §3.5): a tile is made narrower by dragging the handle on
 * its edge, and wider again from the select in the panel, and what is saved is the width.
 *
 * Here and not only in the smoke suite because the handle measures the grid in real pixels, which
 * jsdom does not have; the note asks for exactly this.
 */

const words = englishCommon.content.editor;
const stamp = Date.now().toString(36);

const heading = (id: string, text: string) => ({
  id,
  type: 'heading',
  version: 1,
  props: { level: 2, text: { en: text, it: text } },
});

test('a tile of a dashboard is narrowed with its handle and widened from the panel', async ({
  page,
  context,
}) => {
  test.setTimeout(60_000);
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.setViewportSize({ width: 1920, height: 1000 });

  const born = await createContent(context, {
    kind: 'Dashboard',
    slug: `bench-board-${stamp}`,
    title: { en: 'Bench board', it: 'Bacheca del banco' },
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 's_board',
          layout: 'stacked',
          background: 'none',
          padding: 'md',
          width: 'default',
          blocks: [heading('b_first', 'First tile'), heading('b_second', 'Second tile')],
          sections: [],
        },
      ],
    },
  });

  try {
    await page.goto(`/staff/content/${born.id}`);

    const frame = page.getByRole('region', { name: words.preview });
    const first = frame.locator('[data-tile="b_first"]');
    await expect(first).toHaveAttribute('data-span', '12');

    await frame.getByRole('heading', { name: 'First tile' }).click();
    const handle = first.locator('[data-span-handle]');
    await expect(handle).toBeVisible();

    // From the right edge to about a quarter of the row: the tile snaps to a quarter.
    const box = (await first.boundingBox())!;
    const grip = (await handle.boundingBox())!;
    await page.mouse.move(grip.x + grip.width / 2, grip.y + grip.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width / 4, grip.y + grip.height / 2, { steps: 10 });
    await page.mouse.up();

    await expect(first).toHaveAttribute('data-span', '3');

    // And from the panel, to half.
    await page.getByLabel(words.tileWidth.label, { exact: true }).click();
    await page.getByRole('option', { name: words.tileWidth.options['6'], exact: true }).click();
    await expect(first).toHaveAttribute('data-span', '6');

    // Stored by the autosave after the pause (G15): the row says half.
    await expect
      .poll(
        async () => {
          const stored = await readContent(context, born.id);
          const tiles = (stored.body.sections[0] as { blocks: { id: string; span: number | null }[] }).blocks;
          return tiles.find((block) => block.id === 'b_first')?.span;
        },
        { timeout: 15_000 },
      )
      .toBe(6);
  } finally {
    await deleteContent(context, born.id);
  }
});
