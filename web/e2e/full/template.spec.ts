import { expect, test } from '@playwright/test';

import { englishCommon } from '../locales';
import {
  createContent,
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

  // In the outline, where the section now is; and the panel has nothing left to report. The middle
  // column opens on the page, so the outline is asked for.
  await page.getByRole('button', { name: words.outline, exact: true }).click();
  await expect(page.getByRole('button', { name: 'Closing', exact: true })).toBeVisible();
  await expect(page.getByText(words.template.differences)).toHaveCount(0);

  await whileWaitingFor(page, 'PUT', '/api/content/', async () => {
    await saveDraft(page, words.saveDraft).click();
  });

  // The assertion the whole rule rests on: a draft that has accepted the change is still a draft.
  // What a visitor reads moved the day somebody published it and has not moved since.
  await publicPage.reload();
  await expect(publicPage.getByRole('heading', { name: first.en })).toBeVisible();
  await expect(publicPage.getByRole('heading', { name: later.en })).toHaveCount(0);

  await visitor.close();
});

test('the preview is three widths of the same page, and the narrow one is really narrow', async ({
  page,
  context,
}) => {
  await readInEnglish(context);
  await signIn(context);

  // ⚠️ The window is pinned, and it has to be now that the editor is three columns: the frame is
  // the middle one, so how wide it draws is a fact about the window as much as about the preview.
  // What this test is about is that the three widths differ from one another.
  await page.setViewportSize({ width: 1600, height: 900 });

  const born = await createContent(context, {
    slug: `bench-preview-${stamp}`,
    title: { en: 'Bench preview', it: 'Anteprima del banco' },
    body: { schemaVersion: 1, sections: [section('opening', 'Opening', first)] },
  });

  await page.goto(`/staff/${department}/content/${born.id}`);
  // No press to get here any more: the middle column opens on the page itself.

  const frame = page.getByRole('region', { name: words.preview });
  await expect(frame).toBeVisible();

  // A measure, because this is the fault no assertion about text can see: three buttons that all
  // draw the same page at the same width would look exactly like a working preview in a
  // screenshot, and every word asserted about it would still be there.
  const wide = await frame.boundingBox();
  await page.getByRole('button', { name: words.previewWidths.phone }).click();
  const narrow = await frame.boundingBox();

  expect(narrow).not.toBeNull();
  expect(wide).not.toBeNull();
  expect(narrow!.width).toBeLessThanOrEqual(390);
  expect(wide!.width - narrow!.width).toBeGreaterThan(100);

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

  await page.goto(`/staff/${department}/content/${template.id}`);
  // The outline is where a section is added; the middle column opens on the page.
  await page.getByRole('button', { name: words.outline, exact: true }).click();
  await page.getByRole('button', { name: words.addSection }).click();

  // The four fields a template has and a page does not. `key` is the one everything else hangs
  // from: without it the section imposes nothing on anybody.
  const form = properties(page);
  await form.getByLabel(sectionFields.key).fill('intro');
  await form.getByRole('checkbox', { name: blocks.heading.label }).check();
  await form.getByRole('button', { name: words.applySection }).click();

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
  const born = await pageFromTemplate(context, template.id, `bench-obeys-${stamp}`);
  await page.goto(`/staff/${department}/content/${born.id}`);

  // The assertion the four fields exist for: with that section selected, the bar of components
  // offers the one block the template allows and refuses the twenty-six others. Nothing of this
  // travelled in the copy — the editor read it off the template, by key.
  //
  // ⚠️ Refused means **disabled and still shown**, not filtered out, since the palette moved to the
  // left on 10 September 2026: it is beside the page and its target changes as you click around, so
  // a list that changed shape each time would be one nobody could learn. Asserted on both halves,
  // because a bar that had quietly disabled everything would pass on the second line alone.
  await page.getByRole('button', { name: words.outline, exact: true }).click();
  await selectSection(page, 'intro');

  const palette = page.getByLabel(blocks.subgroups.text);
  await expect(palette.getByRole('button', { name: blocks.heading.label, exact: true })).toBeEnabled();
  await expect(palette.getByRole('button', { name: blocks.text.label, exact: true })).toBeDisabled();
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
