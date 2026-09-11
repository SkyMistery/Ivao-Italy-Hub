import { expect, test } from '@playwright/test';

import { englishCommon } from '../locales';
import {
  createContent,
  deleteContent,
  department,
  pageFromTemplate,
  properties,
  publishContent,
  readContent,
  readInEnglish,
  saveDraft,
  selectSection,
  signIn,
  type ContentRow,
  whileWaitingFor,
  writeContent,
  writeInBothLanguages,
} from './bench';

/**
 * A template that moves on after pages have been made from it (design M1 §9.1, plan G11).
 *
 * The rule being guarded is the one M1 refuses to bend: **a template never rewrites a page by
 * itself**. So this is two assertions that have to hold at the same time — the editor *says* a
 * section was added, and the page a visitor reads has not changed by one word, not when the
 * template changed, and not even after somebody accepted the change into the draft. Only publishing
 * moves what the public sees, and nothing here publishes twice.
 *
 * The scene is set through the API on purpose: what is being tested is what the editor says once a
 * template has moved on, and four forms on the way in would be four more ways to fail for a reason
 * that is not the question.
 */

const words = englishCommon.content.editor;
const sectionFields = englishCommon.content.section.fields;
const blocks = englishCommon.blocks;
const stamp = Date.now().toString(36);

const first = { en: 'What the template said first', it: 'Quello che il template diceva prima' };
const later = { en: 'Added to the template afterwards', it: 'Aggiunta al template dopo' };

/** A section of a template: a key, so a copy can be matched back to it, and one heading to read. */
const section = (key: string, title: string, heading: { en: string; it: string }) => ({
  id: `s_${key}`,
  key,
  title: { en: title, it: title },
  layout: 'stacked',
  background: 'none',
  padding: 'md',
  width: 'default',
  blocks: [{ id: `b_${key}`, type: 'heading', version: 1, props: { level: 2, text: heading } }],
  sections: [],
});

test('a template that moves on is said in the editor, and changes nothing a visitor reads', async ({
  page,
  context,
  browser,
}) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- a template, and a page from it
  const template = await createContent(context, {
    slug: `bench-template-${stamp}`,
    isTemplate: true,
    title: { en: 'Bench template', it: 'Template del banco' },
    body: { schemaVersion: 1, sections: [section('opening', 'Opening', first)] },
  });

  const born = await pageFromTemplate(context, template.id, `bench-from-template-${stamp}`);

  try {
    await writeContent(context, born, { visibility: 'Public' });
    await publishContent(context, born.id);

    const visitor = await browser.newContext();
    await readInEnglish(visitor);
    const publicPage = await visitor.newPage();

    await publicPage.goto(`/${born.slug}`);
    await expect(publicPage.getByRole('heading', { name: first.en })).toBeVisible();

    // ---------------------------------------------------------------- the template gains a section
    const before: ContentRow = await readContent(context, template.id);
    await writeContent(context, before, {
      body: {
        schemaVersion: 1,
        sections: [...before.body.sections, section('closing', 'Closing', later)],
      },
    });

    // ---------------------------------------------------------------- the editor says so
    await page.goto(`/staff/${department}/content/${born.id}`);

    await expect(page.getByText(words.template.differences)).toBeVisible();
    await expect(page.getByText(words.template.added.replace('{{section}}', 'Closing'))).toBeVisible();

    // And nothing has happened to the page while it said so.
    await publicPage.reload();
    await expect(publicPage.getByRole('heading', { name: first.en })).toBeVisible();
    await expect(publicPage.getByRole('heading', { name: later.en })).toHaveCount(0);

    // ---------------------------------------------------------------- accepting it, one difference
    await page.getByRole('button', { name: words.template.apply.added }).click();

    // In the outline, where the section now is; and the panel has nothing left to report. The
    // middle column opens on the page, so the outline is asked for.
    await page.getByRole('button', { name: words.outline, exact: true }).click();
    await expect(page.getByRole('button', { name: 'Closing', exact: true })).toBeVisible();
    await expect(page.getByText(words.template.differences)).toHaveCount(0);

    await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
      await saveDraft(page, words.saveDraft).click();
    });

    // The assertion the whole rule rests on: a draft that has accepted the change is still a
    // draft. What a visitor reads moved the day somebody published it and has not moved since.
    await publicPage.reload();
    await expect(publicPage.getByRole('heading', { name: first.en })).toBeVisible();
    await expect(publicPage.getByRole('heading', { name: later.en })).toHaveCount(0);

    await visitor.close();
  } finally {
    // Taken back, page first and template after: a template left behind is a row in the picker of
    // every run that follows (`deleteContent`). Away from the editor first, or the draft the page
    // holds would be stored on the way out, onto a row that is being deleted.
    await page.goto('/');
    await deleteContent(context, born.id);
    await deleteContent(context, template.id);
  }
});

test('the preview is three widths of the same page, and the narrow one is really narrow', async ({
  page,
  context,
}) => {
  await readInEnglish(context);
  await signIn(context);

  // ⚠️ The window is pinned, and it has to be now that the editor is three columns: the frame is
  // the middle one, so how wide it draws is a fact about the window as much as about the preview.
  // What this test is about is that the three widths differ from one another — and, since G15,
  // that the page lays out by the width it is given: at 1600 the middle column is under 768 pixels
  // and a two column section honestly stands in **one**, so the window is wider than that here.
  await page.setViewportSize({ width: 1920, height: 900 });

  // A section in two columns, because that is what a narrow preview has to be seen to fold.
  const twoColumns = {
    ...section('pair', 'Pair', first),
    layout: '1/2+1/2',
    blocks: [
      { id: 'b_left', type: 'heading', version: 1, props: { level: 2, text: first }, column: 0 },
      { id: 'b_right', type: 'heading', version: 1, props: { level: 2, text: later }, column: 1 },
    ],
  };

  const born = await createContent(context, {
    slug: `bench-preview-${stamp}`,
    title: { en: 'Bench preview', it: 'Anteprima del banco' },
    body: { schemaVersion: 1, sections: [twoColumns] },
  });

  await page.goto(`/staff/${department}/content/${born.id}`);
  // No press to get here any more: the middle column opens on the page itself.

  const frame = page.getByRole('region', { name: words.preview });
  await expect(frame).toBeVisible();

  // How many columns the section is standing in: the grid's own computed tracks, which is the one
  // fact a screenshot of a narrow preview cannot be trusted about.
  const grid = frame.locator('section > div > div.grid').first();
  const columnsOf = () =>
    grid.evaluate((element) => getComputedStyle(element).gridTemplateColumns.trim().split(/\s+/u).length);

  // A measure, because this is the fault no assertion about text can see: three buttons that all
  // draw the same page at the same width would look exactly like a working preview in a
  // screenshot, and every word asserted about it would still be there.
  const wide = await frame.boundingBox();
  expect(await columnsOf()).toBe(2);

  await page.getByRole('button', { name: words.previewWidths.phone }).click();
  const narrow = await frame.boundingBox();

  expect(narrow).not.toBeNull();
  expect(wide).not.toBeNull();
  expect(narrow!.width).toBeLessThanOrEqual(390);
  expect(wide!.width - narrow!.width).toBeGreaterThan(100);

  // ⚠️ And the section **folded**. Until G15 this was two columns of 167 pixels inside a frame of
  // 390 — the preview looked like a phone and laid out like a desktop, because the renderer asked
  // the window — and the width assertion above stayed green throughout. This is the line that
  // would have caught it.
  await expect.poll(columnsOf).toBe(1);

  // And it is the same renderer, not a picture of one: the page is still in there.
  await expect(frame.getByRole('heading', { name: first.en })).toBeVisible();
});
test('a template written in the editor is obeyed by the pages made from it', async ({ page, context }) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // An empty template, and everything that makes it a template written from the screens. Until
  // G11a this could only be done with a seed or a hand written PUT, which is the debt G11 left.
  const template = await createContent(context, {
    slug: `bench-authored-${stamp}`,
    isTemplate: true,
    title: { en: 'Authored template', it: 'Template scritto a mano' },
    body: { schemaVersion: 1, sections: [] },
  });

  let born: ContentRow | null = null;

  try {
    await page.goto(`/staff/${department}/content/${template.id}`);
    // The outline is where a section is added; the middle column opens on the page.
    await page.getByRole('button', { name: words.outline, exact: true }).click();
    await page.getByRole('button', { name: words.addSection }).click();

    // The four fields a template has and a page does not. `key` is the one everything else hangs
    // from: without it the section imposes nothing on anybody.
    const form = properties(page);
    await form.getByLabel(sectionFields.key).fill('intro');
    await form.getByRole('checkbox', { name: blocks.heading.label }).check();
    // The one setting of a section that does **not** apply as it is typed: a key is written once
    // and then fixed, so it is set with a button — or "in" would be the key of a section meant to
    // be "intro". The line under the form saying which key it is comes the moment it is set, and
    // is what the save below waits for.
    await form.getByRole('button', { name: words.setKey }).click();
    await expect(page.getByText(`Key: intro`, { exact: false })).toBeVisible();

    await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
      await saveDraft(page, words.saveDraft).click();
    });

    // Written once, then shown: the field is gone and the key is a line, because changing it would
    // silently detach every page already made from this template.
    await page.reload();
    await page.getByRole('button', { name: words.outline, exact: true }).click();
    await page.getByRole('button', { name: 'intro', exact: true }).click();
    await expect(properties(page).getByLabel(sectionFields.key)).toHaveCount(0);
    await expect(page.getByText(`Key: intro`, { exact: false })).toBeVisible();

    // ---------------------------------------------------------------- and a page obeys it
    born = await pageFromTemplate(context, template.id, `bench-obeys-${stamp}`);
    await page.goto(`/staff/${department}/content/${born.id}`);

    // The assertion the four fields exist for: with that section selected, the bar of components
    // offers the one block the template allows and refuses the twenty-six others. Nothing of this
    // travelled in the copy — the editor read it off the template, by key.
    //
    // ⚠️ Refused means **disabled and still shown**, not filtered out, since the palette moved to
    // the left on 10 September 2026: it is beside the page and its target changes as you click
    // around, so a list that changed shape each time would be one nobody could learn. Asserted on
    // both halves, because a bar that had quietly disabled everything would pass on the second
    // line alone.
    await page.getByRole('button', { name: words.outline, exact: true }).click();
    await selectSection(page, 'intro');

    const palette = page.getByLabel(blocks.subgroups.text);
    await expect(palette.getByRole('button', { name: blocks.heading.label, exact: true })).toBeEnabled();
    await expect(palette.getByRole('button', { name: blocks.text.label, exact: true })).toBeDisabled();
  } finally {
    // Taken back (`deleteContent`), away from the editor first so nothing is stored on the way out.
    await page.goto('/');
    if (born !== null) {
      await deleteContent(context, born.id);
    }
    await deleteContent(context, template.id);
  }
});

test('the preview is where a page is composed: a block picked there opens its own fields', async ({
  page,
  context,
}) => {
  // ⚠️ Road (A) of `decisions/2026-09-09-comporre-una-pagina-guardandola.md`, asked for by Carmine:
  // "an idea of how the document is coming out and of the space things take, **without going back
  // and forth to the preview**". So the preview stopped being a place you go to and come back from:
  // the panel is beside it, and clicking the page is clicking the outline.
  //
  // It is a round of the full suite because the point is the **real** renderer: the preview is the
  // very same component a visitor gets, and what is being asserted is that it became clickable
  // there and nowhere else.
  await readInEnglish(context);
  await signIn(context);

  const born = await createContent(context, {
    slug: `bench-picking-${stamp}`,
    title: { en: 'Bench picking', it: 'Composizione del banco' },
    body: { schemaVersion: 1, sections: [section('opening', 'Opening', first)] },
  });

  await page.goto(`/staff/${department}/content/${born.id}`);
  // Nothing to press: since 10 September 2026 the middle column *is* the page, which is the second
  // half of the same decision — the road chosen on 9 September was behind a button, so it was the
  // road nobody took.

  const frame = page.getByRole('region', { name: words.preview });
  const heading = frame.getByRole('heading', { name: first.en });
  await expect(heading).toBeVisible();

  // ⚠️ The page itself is what the panel opens on, and that is the second half of the same
  // decision: the metadata used to be a form above the editor, 1182 pixels tall in a window of 950,
  // so the page being composed started below the fold. Now they are the page's own properties, in
  // the panel a section and a block already use.
  await expect(page.getByLabel(englishCommon.content.fields.slug, { exact: true })).toBeVisible();

  await heading.click();

  // The fields of that block, beside the page it belongs to — and the page is still on screen,
  // which is the whole of the request: no going back and forth. A **group** and not a labelled box:
  // the text of a heading is translated, so what the generator draws is the language tabs.
  await expect(page.getByRole('group', { name: englishCommon.blocks.heading.fields.text })).toBeVisible();
  await expect(heading).toBeVisible();

  // And the page's own fields have made way for the block's: one panel, one thing at a time.
  //
  // ⚠️ Hidden and **not** removed, which is the subtle half: `Save draft` lives in the toolbar and
  // submits by `form=`, and a button cannot submit a form that has left the document. So the page's
  // form is always there and merely out of sight — asserted on visibility, because a count would
  // pass for the wrong reason the day somebody unmounts it.
  await expect(page.getByLabel(englishCommon.content.fields.slug, { exact: true })).not.toBeVisible();

  // The way back, which is the panel's own header — there is no outline to return to in here.
  await page.getByRole('button', { name: words.page, exact: true }).click();
  await expect(page.getByLabel(englishCommon.content.fields.slug, { exact: true })).toBeVisible();
});

test('a component dragged from the palette lands between two blocks', async ({ page, context }) => {
  // The third thing the page builder of va.ivao.aero has (G15, session 3), and the one the note of
  // 10 September said had to be measured in a browser: the drop **between** two blocks is where
  // this is got wrong. So: two headings, "Text" dragged onto the slot between them, and the order
  // of what the page then draws.
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.setViewportSize({ width: 1920, height: 1000 });

  const pair = {
    ...section('pair', 'Pair', first),
    blocks: [
      { id: 'b_top', type: 'heading', version: 1, props: { level: 2, text: first } },
      { id: 'b_bottom', type: 'heading', version: 1, props: { level: 2, text: later } },
    ],
  };

  const born = await createContent(context, {
    slug: `bench-drag-${stamp}`,
    title: { en: 'Bench drag', it: 'Trascinamento del banco' },
    body: { schemaVersion: 1, sections: [pair] },
  });

  await page.goto(`/staff/${department}/content/${born.id}`);

  const frame = page.getByRole('region', { name: words.preview });
  await expect(frame.getByRole('heading', { name: later.en })).toBeVisible();

  // The palette only offers what has somewhere to go: picking a block on the page picks its section.
  await frame.getByRole('heading', { name: first.en }).click();

  const entry = page
    .getByLabel(blocks.subgroups.text)
    .getByRole('button', { name: blocks.text.label, exact: true });
  await expect(entry).toBeEnabled();

  // Nothing to drop onto until a drag is under way: the slots would otherwise be air between the
  // blocks that a visitor does not get. They are in the document and hidden — registered before
  // the drag that needs them — so what is counted is what can be seen.
  const slots = frame.getByLabel(words.dropHere).filter({ visible: true });
  await expect(slots).toHaveCount(0);

  const from = (await entry.boundingBox())!;
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  // Past the distance a click may travel, which is what makes it a drag and not a click.
  await page.mouse.move(from.x + from.width / 2 + 16, from.y + from.height / 2 + 16, { steps: 4 });

  // One before each block and one after the last: three, for two blocks.
  await expect(slots).toHaveCount(3);
  const between = frame.locator('[data-drop-index="1"]');
  const to = (await between.boundingBox())!;
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 10 });
  await page.mouse.up();

  // Dropped and selected: its fields are in the panel, and the slots are gone again.
  const markdown = page.getByRole('group', { name: blocks.text.fields.markdown });
  await expect(markdown).toBeVisible();
  await expect(slots).toHaveCount(0);

  // Written into, so the page has something to show where it landed — between the two.
  await writeInBothLanguages(properties(page), blocks.text.fields.markdown, 'markdown', {
    en: 'Dropped between',
    it: 'Lasciato in mezzo',
  });
  await expect(frame.getByText('Dropped between')).toBeVisible();
  await expect(frame.locator('h2, p')).toHaveText([first.en, 'Dropped between', later.en]);
});

test('a section is dragged above another on the page itself', async ({ page, context }) => {
  // Carmine, 11 September 2026: "by hand, meaning draggable, on the page". A section is picked,
  // and then dragged by the grip on its bar; the order of what the page draws is the assertion.
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.setViewportSize({ width: 1920, height: 1000 });

  const born = await createContent(context, {
    slug: `bench-sections-${stamp}`,
    title: { en: 'Bench sections', it: 'Sezioni del banco' },
    body: {
      schemaVersion: 1,
      sections: [section('upper', 'Upper', first), section('lower', 'Lower', later)],
    },
  });

  await page.goto(`/staff/${department}/content/${born.id}`);

  const frame = page.getByRole('region', { name: words.preview });
  await expect(frame.locator('h2')).toHaveText([first.en, later.en]);

  // A section is picked by its own air: a click on the section's padding, above its heading.
  const lower = frame.locator('[data-pickable="section"]').nth(1);
  const box = (await lower.boundingBox())!;
  await page.mouse.click(box.x + 8, box.y + 8);

  const grip = frame.getByRole('button', { name: words.reorder });
  await expect(grip).toBeVisible();

  const from = (await grip.boundingBox())!;
  const upper = (await frame.locator('[data-pickable="section"]').first().boundingBox())!;
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(from.x + from.width / 2, from.y - 20, { steps: 4 });
  await page.mouse.move(upper.x + upper.width / 2, upper.y + 8, { steps: 12 });
  await page.mouse.up();

  await expect(frame.locator('h2')).toHaveText([later.en, first.en]);
});

test('a block is dragged from one section into another on the page itself', async ({ page, context }) => {
  // Carmine, 11 September 2026: "the elements in a section too, and between sections". The block
  // is picked, grabbed by the grip on its bar, and dropped on the slot at the top of the other
  // section; the order of the headings on the page is the assertion.
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.setViewportSize({ width: 1920, height: 1000 });

  const born = await createContent(context, {
    slug: `bench-blocks-${stamp}`,
    title: { en: 'Bench blocks', it: 'Blocchi del banco' },
    body: {
      schemaVersion: 1,
      sections: [section('upper', 'Upper', first), section('lower', 'Lower', later)],
    },
  });

  await page.goto(`/staff/${department}/content/${born.id}`);

  const frame = page.getByRole('region', { name: words.preview });
  await expect(frame.locator('h2')).toHaveText([first.en, later.en]);

  await frame.getByRole('heading', { name: later.en }).click();
  const grip = frame.getByRole('button', { name: words.reorder });
  await expect(grip).toBeVisible();

  const from = (await grip.boundingBox())!;
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(from.x + from.width / 2, from.y - 20, { steps: 4 });

  // The slots of every section are offered, the dragged block's own included: two in the upper
  // section, two in the lower.
  const slots = frame.getByLabel(words.dropHere).filter({ visible: true });
  await expect(slots).toHaveCount(4);

  const to = (await slots.first().boundingBox())!;
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 12 });
  await page.mouse.up();

  await expect(frame.locator('h2')).toHaveText([later.en, first.en]);
  // And it is the upper section that holds both now: the lower one is empty and invites a block.
  await expect(
    frame.locator('[data-pickable="section"]').nth(1).getByRole('button', { name: '+ Add here' }),
  ).toBeVisible();
});
