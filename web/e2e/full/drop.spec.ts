import { expect, type Locator, type Page } from '@playwright/test';

import { englishCommon } from '../locales';

import {
  createContent,
  deleteContent,
  properties,
  readInEnglish,
  signIn,
  test,
  writeInBothLanguages,
} from './bench';

/**
 * The page being composed stays drawn: when the editor opens, after a component is dropped into a
 * section, and after the draft has saved itself (issue #351).
 *
 * Found by the staff trying the editor on 22 October 2026: the page on screen went blank, above all
 * after a drag from the palette, and came back on a reload, on going to the outline and back, or
 * after publishing. The draft on the server was whole. So what is asserted is what a person sees —
 * every block **drawn**, with a box: in the document is not enough, the sections were all there
 * with none.
 */

const words = englishCommon.content.editor;
const blocks = englishCommon.blocks;
const stamp = Date.now().toString(36);

const heading = (id: string, en: string) => ({
  id,
  type: 'heading',
  version: 1,
  props: { level: 2, text: { en, it: en } },
});

const text = (id: string, en: string) => ({
  id,
  type: 'text',
  version: 1,
  props: { markdown: { en, it: en } },
});

const section = (id: string, layout: string, content: object[]) => ({
  id,
  title: { en: id, it: id },
  layout,
  background: 'none',
  padding: 'md',
  width: 'default',
  blocks: content,
  sections: [],
});

/** Every one of these is on screen with a box, and so is every section of the page. */
async function expectDrawn(frame: Locator, texts: readonly string[], when: string): Promise<void> {
  for (const shown of texts) {
    await expect(frame.getByText(shown, { exact: true }), `${shown}, ${when}`).toBeVisible();
  }

  for (const drawn of await frame.locator('[data-pickable="section"]').all()) {
    const box = await drawn.boundingBox();
    expect(box?.height ?? 0, `a section's height, ${when}`).toBeGreaterThan(0);
  }
}

/** A palette entry carried onto a slot of the page, the way a pointer does it. */
async function drag(page: Page, entry: Locator, slot: Locator): Promise<void> {
  const from = (await entry.boundingBox())!;
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  // Past the distance a click may travel, which is what makes it a drag.
  await page.mouse.move(from.x + from.width / 2 + 16, from.y + from.height / 2 + 16, { steps: 4 });

  await expect(slot).toBeVisible();
  const to = (await slot.boundingBox())!;
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 10 });
  await page.mouse.up();
}

test.beforeEach(async ({ page, context }) => {
  await readInEnglish(context);
  await signIn(context);
  await page.setViewportSize({ width: 1920, height: 1000 });

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('a page with prose in it is drawn when the editor opens', async ({ page, context, afterwards }) => {
  const shown = ['Above the prose', 'A paragraph somebody wrote.'];
  const born = await createContent(context, {
    slug: `bench-drawn-${stamp}`,
    title: { en: 'Bench drawn', it: 'Disegnata del banco' },
    body: {
      schemaVersion: 1,
      sections: [section('s_only', 'stacked', [heading('b_title', shown[0]!), text('b_prose', shown[1]!)])],
    },
  });
  afterwards(() => deleteContent(context, born.id));

  await page.goto(`/staff/content/${born.id}`);

  await expectDrawn(page.getByRole('region', { name: words.preview }), shown, 'once opened');
});

test('a component dropped into a section leaves every block drawn, the new one too', async ({
  page,
  context,
  afterwards,
}) => {
  // Prose on the page from the start: a page of headings alone never went blank.
  const before = [
    'Above the drop',
    'Some prose above the drop.',
    'First of the body',
    'Last of the body',
    'Left column',
    'Right column',
  ];
  const born = await createContent(context, {
    slug: `bench-drop-${stamp}`,
    title: { en: 'Bench drop', it: 'Rilascio del banco' },
    body: {
      schemaVersion: 1,
      sections: [
        section('s_top', 'stacked', [heading('b_top', before[0]!), text('b_prose', before[1]!)]),
        section('s_body', 'stacked', [heading('b_first', before[2]!), heading('b_last', before[3]!)]),
        section('s_pair', '1/2+1/2', [
          { ...heading('b_left', before[4]!), column: 0 },
          { ...heading('b_right', before[5]!), column: 1 },
        ]),
      ],
    },
  });
  afterwards(() => deleteContent(context, born.id));

  await page.goto(`/staff/content/${born.id}`);

  const frame = page.getByRole('region', { name: words.preview });
  await expectDrawn(frame, before, 'once opened');

  // A block of the body picked, so that the palette offers what may go there.
  await frame.getByRole('heading', { name: before[2]! }).click();

  const entry = page
    .getByLabel(blocks.subgroups.text)
    .getByRole('button', { name: blocks.text.label, exact: true });
  await expect(entry).toBeEnabled();

  // Between the two blocks of the body. ⚠️ Before the fix the page went blank the moment the drag
  // began, and this slot had no box to be dropped on.
  await drag(page, entry, frame.locator('[data-pickable="section"]').nth(1).locator('[data-drop-index="1"]'));

  // Dropped and picked: its fields are in the panel, and the slots are gone again.
  await expect(frame.getByLabel(words.dropHere).filter({ visible: true })).toHaveCount(0);
  await expect(frame.locator('[data-pickable="block"]')).toHaveCount(before.length + 1);
  await expectDrawn(frame, before, 'after the drop');

  // Written into, so the page has prose where it landed.
  const prose = 'Dropped between the two.';
  await writeInBothLanguages(properties(page), blocks.text.fields.markdown, 'markdown', {
    en: prose,
    it: 'Lasciato fra i due.',
  });
  await expectDrawn(frame, [...before, prose], 'once the new block is written into');

  // And once the draft has saved itself and the row it answered has come back.
  await expect(page.getByRole('status', { name: words.autosave.title })).toHaveText(/^Saved at/, {
    timeout: 30_000,
  });
  await expectDrawn(frame, [...before, prose], 'after the autosave');
  await expect(frame.locator('h2, p')).toHaveText([...before.slice(0, 3), prose, ...before.slice(3)]);
});
